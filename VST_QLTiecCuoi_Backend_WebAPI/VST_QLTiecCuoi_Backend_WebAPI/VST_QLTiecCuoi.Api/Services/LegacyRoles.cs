using Microsoft.AspNetCore.Identity;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;
public static class LegacyRoles {
 public static async Task MergeStaffAsync(ApplicationDbContext db,UserManager<User> users,RoleManager<IdentityRole> roles) {
  var legacy=await roles.FindByNameAsync("Staff");if(legacy==null)return;
  await using var tx=await db.Database.BeginTransactionAsync();
  foreach(var user in await users.GetUsersInRoleAsync("Staff")) {
   var old=await users.GetRolesAsync(user);
   var target=old.Contains("Admin")?"Admin":"Manager";
   var removed=await users.RemoveFromRolesAsync(user,old.Where(r=>r!=target));
   if(!removed.Succeeded)throw new InvalidOperationException("Không chuyển được quyền Staff cũ.");
   if(!old.Contains(target)&&!(await users.AddToRoleAsync(user,target)).Succeeded)throw new InvalidOperationException("Không gán được quyền mới.");
   user.SessionVersion++;
   if(!(await users.UpdateAsync(user)).Succeeded)throw new InvalidOperationException("Không cập nhật được phiên đăng nhập cũ.");
  }
  if(!(await roles.DeleteAsync(legacy)).Succeeded)throw new InvalidOperationException("Không xóa được vai trò Staff sau chuyển đổi.");
  await tx.CommitAsync();
 }
}
