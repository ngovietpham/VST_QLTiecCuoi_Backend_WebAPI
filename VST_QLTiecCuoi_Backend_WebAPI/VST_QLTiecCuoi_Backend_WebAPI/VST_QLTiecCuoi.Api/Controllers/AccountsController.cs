using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/accounts"),Authorize]
public class AccountsController(ApplicationDbContext db,UserManager<User> users):ControllerBase {
 private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("me")]
 public async Task<IActionResult> Me() {
  var u=await users.FindByIdAsync(Actor);return u==null?NotFound():Ok(new{u.Id,u.UserName,u.Email,u.FullName,u.PhoneNumber,roles=await users.GetRolesAsync(u)});
 }
 [HttpPut("me")]
 public async Task<IActionResult> Profile(ProfileRequest r) {
  var u=await users.FindByIdAsync(Actor);if(u==null)return NotFound();u.FullName=r.FullName.Trim();u.PhoneNumber=r.Phone?.Trim();
  var result=await users.UpdateAsync(u);return result.Succeeded?Ok(new{message="Đã cập nhật thông tin."}):BadRequest(new{message="Không lưu được thông tin."});
 }
 [HttpPut("me/password")]
 public async Task<IActionResult> Password(PasswordRequest r) {
  var u=await users.FindByIdAsync(Actor);if(u==null)return NotFound();var result=await users.ChangePasswordAsync(u,r.CurrentPassword,r.NewPassword);
  if(!result.Succeeded)return BadRequest(new{message=string.Join("; ",result.Errors.Select(e=>e.Description))});
  u.SessionVersion++;await users.UpdateAsync(u);return Ok(new{message="Đã đổi mật khẩu. Hãy đăng nhập lại."});
 }
 [HttpGet,Authorize(Roles="Admin")]
 public async Task<IActionResult> List([FromQuery]string? q) {
  var query=users.Users.AsNoTracking();if(!string.IsNullOrWhiteSpace(q))query=query.Where(u=>u.UserName!.Contains(q)||u.Email!.Contains(q)||u.FullName.Contains(q));
  var list=await query.OrderBy(u=>u.UserName).Take(500).ToListAsync();var result=new List<object>();
  foreach(var u in list)result.Add(new{u.Id,u.UserName,u.Email,u.FullName,u.Disabled,u.EmailConfirmed,roles=await users.GetRolesAsync(u)});return Ok(result);
 }
 [HttpPut("{id}/role"),Authorize(Roles="Admin")]
 public async Task<IActionResult> SetRole(string id,AccountRoleRequest r) {
  if(id==Actor)return BadRequest(new{message="Không tự đổi quyền tài khoản đang đăng nhập."});
  var u=await users.FindByIdAsync(id);if(u==null)return NotFound();var roles=await users.GetRolesAsync(u);
  if(roles.Contains("Admin"))return BadRequest(new{message="Không đổi quyền Admin qua màn hình này."});
  await using var tx=await db.Database.BeginTransactionAsync();
  if(!(await users.RemoveFromRolesAsync(u,roles)).Succeeded||!(await users.AddToRoleAsync(u,r.Role)).Succeeded)return BadRequest();
  u.SessionVersion++;await users.UpdateAsync(u);db.AuditLogs.Add(new(){ActorId=Actor,Action="AccountRole",Detail=$"{id}: {r.Role}"});await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{message="Đã đổi quyền và kết thúc phiên cũ."});
 }
 [HttpPut("{id}/lock"),Authorize(Roles="Admin")]
 public async Task<IActionResult> Lock(string id,LockRequest r) {
  var u=await users.FindByIdAsync(id);if(u==null)return NotFound();
  if(id==Actor||await users.IsInRoleAsync(u,"Admin"))return BadRequest(new{message="Không khóa tài khoản Admin."});
  u.Disabled=r.Disabled;u.SessionVersion++;if(!r.Disabled){u.LockoutEnd=null;u.AccessFailedCount=0;}
  await users.UpdateAsync(u);db.AuditLogs.Add(new(){ActorId=Actor,Action="AccountLock",Detail=$"{id}: {r.Disabled}"});await db.SaveChangesAsync();return Ok(new{message="Đã cập nhật trạng thái tài khoản."});
 }
 [HttpDelete("{id}"),Authorize(Roles="Admin")]
 public async Task<IActionResult> Delete(string id) {
  var u=await users.FindByIdAsync(id);if(u==null)return NotFound();
  if(id==Actor||await users.IsInRoleAsync(u,"Admin"))return BadRequest(new{message="Không xóa tài khoản Admin."});
  if(await db.Bookings.AnyAsync(b=>b.UserId==id))return Conflict(new{message="Tài khoản đã có đơn đặt tiệc. Hãy khóa để bảo toàn lịch sử."});
  await using var tx=await db.Database.BeginTransactionAsync();
  await db.Set<EmailChallenge>().Where(c=>c.UserId==id).ExecuteDeleteAsync();
  var result=await users.DeleteAsync(u);if(!result.Succeeded)return BadRequest(new{message="Không thể xóa tài khoản."});
  db.AuditLogs.Add(new(){ActorId=Actor,Action="AccountDelete",Detail=id});await db.SaveChangesAsync();await tx.CommitAsync();return NoContent();
 }
}
public record AccountRoleRequest([System.ComponentModel.DataAnnotations.Required,System.ComponentModel.DataAnnotations.RegularExpression("^(User|Manager)$")] string Role);
