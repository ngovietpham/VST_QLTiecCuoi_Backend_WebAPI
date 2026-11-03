using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;
public class BusinessException(int status,string message):Exception(message) { public int Status {get;}=status; }
public class WeddingService(ApplicationDbContext db) {
 public static DateOnly Today=>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
 public static void Require(bool condition,string message,int status=400) {if(!condition)throw new BusinessException(status,message);}
 public static bool Staff(System.Security.Claims.ClaimsPrincipal user)=>user.IsInRole("Manager");
 public async Task ApplyAsync(Booking b,BookingRequest r) {
  Require(DateOnly.TryParseExact(r.EventDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)&&date>=Today,"Ngày tổ chức phải từ hôm nay, định dạng yyyy-MM-dd.");
  Require(date<=Today.AddYears(3),"Chỉ nhận đặt tiệc trong vòng 3 năm.");
  Require(r.Slot is "Lunch" or "Dinner","Ca tiệc phải là Lunch hoặc Dinner.");
  var hall=await db.Halls.FindAsync(r.HallId);Require(hall!=null&&hall.Active,"Sảnh không tồn tại hoặc đang ngừng hoạt động.");
  var menu=await db.Menus.Include(m=>m.Items).ThenInclude(i=>i.Dish).SingleOrDefaultAsync(m=>m.Id==r.MenuId);
  Require(menu!=null&&menu.Active&&menu.Items.Count>0&&menu.Items.All(i=>i.Dish!.Active),"Thực đơn không tồn tại hoặc có món ngừng phục vụ.");
  Require(r.TableCount<=await db.HallTables.CountAsync(t=>t.HallId==r.HallId),"Số bàn vượt số bàn hiện có của sảnh.");
  Require(!await db.Reservations.AnyAsync(x=>x.HallId==r.HallId&&x.EventDate==r.EventDate&&x.Slot==r.Slot&&x.BookingId!=b.Id),"Sảnh đã có tiệc được duyệt trong ca này.",409);
  b.HallId=r.HallId;b.MenuId=r.MenuId;b.BrideName=r.BrideName.Trim();b.GroomName=r.GroomName.Trim();b.Phone=r.Phone.Trim();
  b.EventDate=r.EventDate;b.Slot=r.Slot;b.TableCount=r.TableCount;b.Note=r.Note?.Trim()??"";b.PricePerTable=menu!.PricePerTable;
  b.MenuSnapshot=menu.Name+": "+string.Join(", ",menu.Items.OrderBy(i=>i.Position).Select(i=>i.Dish!.Name));
 }
 public void Audit(string actor,string action,string detail)=>db.AuditLogs.Add(new(){ActorId=actor,Action=action,Detail=detail});
}
