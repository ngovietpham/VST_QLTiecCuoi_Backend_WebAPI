using Microsoft.AspNetCore.Identity;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;

// Run explicitly on the server. Never seeds a public default password.
public static class ProductionAdmin {
 public static async Task CreateAsync(UserManager<User> users, ApplicationDbContext db, IConfiguration config) {
  if((await users.GetUsersInRoleAsync("Admin")).Count>0)
   throw new InvalidOperationException("Đã có quản trị viên. Không tạo hoặc đổi mật khẩu tự động.");
  var email=config["BootstrapAdmin:Email"]?.Trim();
  var password=config["BootstrapAdmin:Password"];
  if(string.IsNullOrWhiteSpace(email)||string.IsNullOrWhiteSpace(password))
   throw new InvalidOperationException("Cần BootstrapAdmin__Email và BootstrapAdmin__Password.");
  // A transaction prevents leaving a partially created account if assigning the role fails.
  await using var transaction=await db.Database.BeginTransactionAsync();
  var user=new User{UserName=email,Email=email,FullName="Quản trị VST",EmailConfirmed=true};
  var created=await users.CreateAsync(user,password);
  if(!created.Succeeded)throw new InvalidOperationException(string.Join("; ",created.Errors.Select(e=>e.Description)));
  var assigned=await users.AddToRoleAsync(user,"Admin");
  if(!assigned.Succeeded)throw new InvalidOperationException("Không thể gán quyền quản trị.");
  await transaction.CommitAsync();
  Console.WriteLine("Đã tạo quản trị viên. Đăng nhập vẫn yêu cầu OTP gửi đến email đã cấu hình.");
 }
}
