using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/Authenticate"),EnableRateLimiting("auth")]
public class AuthenticateController(UserManager<User> users,IConfiguration config,EmailOtpService otp) : ControllerBase {
 [HttpPost("register")]
 public async Task<IActionResult> Register(RegistrationModel model) {
  if(await users.FindByNameAsync(model.Username)!=null||await users.FindByEmailAsync(model.Email)!=null)return Conflict(new{message="Tên đăng nhập hoặc email đã tồn tại. Có thể gửi lại mã xác minh."});
  var user=new User{UserName=model.Username.Trim(),Email=model.Email.Trim(),FullName=model.FullName.Trim()};
  var result=await users.CreateAsync(user,model.Password);
  if(!result.Succeeded)return BadRequest(new{message=string.Join("; ",result.Errors.Select(e=>e.Description))});
  if(!(await users.AddToRoleAsync(user,"User")).Succeeded) {await users.DeleteAsync(user);return StatusCode(500);}
  return await Send(user,"register");
 }
 [HttpPost("resend-verification")]
 public async Task<IActionResult> Resend(LoginModel model) {
  var user=await Validate(model);
  if(user==null)return Unauthorized(new{message="Sai thông tin hoặc tài khoản đang khóa."});
  if(user.EmailConfirmed)return BadRequest(new{message="Email đã xác minh. Hãy đăng nhập."});
  return await Send(user,"register");
 }
 [HttpPost("verify-email")]
 public async Task<IActionResult> Verify(VerifyOtpModel model) {
  var c=await otp.ConsumeAsync(model.ChallengeId,model.Code,"register");
  if(c==null)return BadRequest(new{message="OTP sai, hết hạn hoặc đã dùng. Tối đa 5 lần thử."});
  var user=await users.FindByIdAsync(c.UserId);
  if(user==null||user.Disabled)return BadRequest();
  user.EmailConfirmed=true;
  return (await users.UpdateAsync(user)).Succeeded?Ok(new{message="Đã xác minh email. Hãy đăng nhập."}):BadRequest();
 }
 [HttpPost("login")]
 public async Task<IActionResult> Login(LoginModel model) {
  var user=await Validate(model);
  if(user==null)return Unauthorized(new{message="Sai thông tin hoặc tài khoản đang khóa."});
  if(!user.EmailConfirmed)return BadRequest(new{message="Hãy xác minh email đăng ký trước khi đăng nhập."});
  return await Send(user,"login");
 }
 [HttpPost("verify-login")]
 public async Task<IActionResult> VerifyLogin(VerifyOtpModel model) {
  var c=await otp.ConsumeAsync(model.ChallengeId,model.Code,"login");
  if(c==null)return BadRequest(new{message="OTP sai, hết hạn hoặc đã dùng. Tối đa 5 lần thử."});
  var user=await users.FindByIdAsync(c.UserId);
  if(user==null||user.Disabled||!user.EmailConfirmed||await users.IsLockedOutAsync(user))return Unauthorized();
  await users.ResetAccessFailedCountAsync(user);
  var roles=await users.GetRolesAsync(user);
  var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,user.Id),new(ClaimTypes.Name,user.UserName!),new("version",user.SessionVersion.ToString()),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())};
  claims.AddRange(roles.Select(r=>new Claim(ClaimTypes.Role,r)));
  var s=config.GetSection("JWTKey"); var expires=DateTime.UtcNow.AddMinutes(s.GetValue("Minutes",60));
  var token=new JwtSecurityToken(s["ValidIssuer"],s["ValidAudience"],claims,expires:expires,signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s["Secret"]!)),SecurityAlgorithms.HmacSha256));
  return Ok(new{token=new JwtSecurityTokenHandler().WriteToken(token),roles,expiresAt=expires,user=new{user.Id,user.UserName,user.Email,user.FullName}});
 }
 [HttpPost("forgot-password")]
 public async Task<IActionResult> Forgot(ResetRequest model) {
  var user=await users.FindByEmailAsync(model.Email.Trim());
  if(user==null||user.Disabled||!user.EmailConfirmed)return Ok(new{challengeId=Guid.NewGuid().ToString("N"),expiresIn=300,message="Nếu email hợp lệ, mã đã được gửi."});
  return await Send(user,"reset");
 }
 [HttpPost("reset-password")]
 public async Task<IActionResult> Reset(ResetConfirmRequest model) {
  var validation=users.PasswordValidators;
  foreach(var v in validation) {var r=await v.ValidateAsync(users,new User(),model.Password);if(!r.Succeeded)return BadRequest(new{message=string.Join("; ",r.Errors.Select(e=>e.Description))});}
  var c=await otp.ConsumeAsync(model.ChallengeId,model.Code,"reset");
  if(c==null)return BadRequest(new{message="Mã xác minh không hợp lệ hoặc hết hạn."});
  var user=await users.FindByIdAsync(c.UserId);if(user==null||user.Disabled)return BadRequest();
  var token=await users.GeneratePasswordResetTokenAsync(user);
  var result=await users.ResetPasswordAsync(user,token,model.Password);
  if(!result.Succeeded)return BadRequest(new{message=string.Join("; ",result.Errors.Select(e=>e.Description))});
  user.SessionVersion++;await users.UpdateAsync(user);
  return Ok(new{message="Đã đổi mật khẩu. Hãy đăng nhập lại."});
 }
 [HttpPost("logout"),Authorize]
 public async Task<IActionResult> Logout() {
  var user=await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  if(user!=null){user.SessionVersion++;await users.UpdateAsync(user);}return NoContent();
 }
 private async Task<User?> Validate(LoginModel model) {
  var input=model.Username.Trim();var user=input.Contains('@')?await users.FindByEmailAsync(input):await users.FindByNameAsync(input);
  if(user==null||user.Disabled||await users.IsLockedOutAsync(user))return null;
  if(await users.CheckPasswordAsync(user,model.Password))return user;
  await users.AccessFailedAsync(user);return null;
 }
 private async Task<IActionResult> Send(User user,string purpose) {
  try{return Ok(new{challengeId=await otp.SendAsync(user,purpose),expiresIn=300,deliveryMode=config["Smtp:Mode"],message=config["Smtp:Mode"]=="File"?"Mã được lưu trong hộp thư demo.":"Đã gửi mã xác minh qua email."});}
  catch(InvalidOperationException ex){return StatusCode(503,new{message=ex.Message});}
 }
}
public record VerifyOtpModel([Required] string ChallengeId,[Required,RegularExpression("^[0-9]{6}$")] string Code);
