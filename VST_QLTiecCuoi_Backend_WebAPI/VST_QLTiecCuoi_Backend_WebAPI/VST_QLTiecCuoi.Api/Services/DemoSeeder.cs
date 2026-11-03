using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;
public static class DemoSeeder {
 public static async Task SeedAsync(ApplicationDbContext db,UserManager<User> users) {
  foreach(var (name,role,full) in new[]{("admin","Admin","Quản trị VST"),("manager","Manager","Quản lý kinh doanh"),("customer","User","Khách hàng mẫu")}) {
   if(await users.FindByNameAsync(name)!=null)continue;
   var u=new User{UserName=name,Email=$"{name}@example.com",FullName=full,EmailConfirmed=true};
   var result=await users.CreateAsync(u,"VstDemo@2026!");if(!result.Succeeded)throw new InvalidOperationException(string.Join(";",result.Errors.Select(x=>x.Description)));
   await users.AddToRoleAsync(u,role);
  }
  if(await db.Halls.AnyAsync())return;
  var halls=new[]{new Hall{Name="Sảnh Kim Cương",Description="Không gian trang trọng cho tiệc lớn"},new Hall{Name="Sảnh Hoa Hồng",Description="Không gian ấm cúng, ánh sáng tự nhiên"}};db.Halls.AddRange(halls);
  var dishes=new[]{new Dish{Name="Gỏi ngó sen tôm thịt",Category="Khai vị"},new Dish{Name="Súp hải sản",Category="Khai vị"},new Dish{Name="Gà quay mật ong",Category="Món chính"},new Dish{Name="Bò sốt tiêu đen",Category="Món chính"},new Dish{Name="Lẩu hải sản",Category="Món chính"},new Dish{Name="Chè hạt sen",Category="Tráng miệng"}};db.Dishes.AddRange(dishes);await db.SaveChangesAsync();
  foreach(var h in halls)for(var n=1;n<=20;n++)db.HallTables.Add(new(){HallId=h.Id,Number=n,Capacity=10});
  db.Menus.Add(new(){Name="Hạnh Phúc",Description="Thực đơn 5 món dành cho 10 khách mỗi bàn",PricePerTable=3500000,Items=dishes.Take(5).Select((d,p)=>new MenuItem{DishId=d.Id,Position=p}).ToList()});
  db.Menus.Add(new(){Name="Trọn Vẹn",Description="Thực đơn 6 món dành cho 10 khách mỗi bàn",PricePerTable=4200000,Items=dishes.Select((d,p)=>new MenuItem{DishId=d.Id,Position=p}).ToList()});
  await db.SaveChangesAsync();
 }
}
