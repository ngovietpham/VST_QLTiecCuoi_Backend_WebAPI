using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/reports"),Authorize(Roles="Admin")]
public class ReportsController(ApplicationDbContext db):ControllerBase {
 [HttpGet("summary")]
 public async Task<IActionResult> Summary([FromQuery]int year=0) {
  if(year==0)year=DateTime.UtcNow.AddHours(7).Year;if(year<2020||year>2100)return BadRequest(new{message="Năm không hợp lệ."});
  var prefix=$"{year:D4}-";var bookings=await db.Bookings.AsNoTracking().Where(b=>b.EventDate.StartsWith(prefix)).ToListAsync();
  var valid=bookings.Where(b=>b.Status is "Approved" or "Completed").ToList();
  var ids=valid.Select(b=>b.Id).ToArray();var payments=await db.Payments.AsNoTracking().Where(p=>p.CreatedAt>=new DateTime(year,1,1).AddHours(-7)&&p.CreatedAt<new DateTime(year+1,1,1).AddHours(-7)||ids.Contains(p.BookingId)).ToListAsync();
  var cash=payments.Where(p=>p.CreatedAt.AddHours(7).Year==year).ToList();
  var guests=await db.Guests.AsNoTracking().Where(g=>ids.Contains(g.BookingId)).ToListAsync();
  return Ok(new{year,bookingCount=bookings.Count,pendingCount=bookings.Count(b=>b.Status=="Pending"),approvedCount=valid.Count,contractValue=valid.Sum(b=>b.PricePerTable*b.TableCount),received=cash.Sum(p=>p.Amount),outstanding=valid.Sum(b=>b.PricePerTable*b.TableCount-payments.Where(p=>p.BookingId==b.Id).Sum(p=>p.Amount)),invited=guests.Sum(g=>g.PartySize),checkedIn=guests.Where(g=>g.CheckedInAt!=null).Sum(g=>g.PartySize),monthly=Enumerable.Range(1,12).Select(m=>new{month=m,bookingCount=bookings.Count(b=>int.Parse(b.EventDate.Substring(5,2))==m),contractValue=valid.Where(b=>int.Parse(b.EventDate.Substring(5,2))==m).Sum(b=>b.PricePerTable*b.TableCount),received=cash.Where(p=>p.CreatedAt.AddHours(7).Month==m).Sum(p=>p.Amount)})});
 }
 [HttpGet("bookings.csv")]
 public async Task<IActionResult> Export([FromQuery]int year=0) {
  if(year==0)year=DateTime.UtcNow.AddHours(7).Year;if(year<2020||year>2100)return BadRequest();var prefix=$"{year:D4}-";
  var bookings=await db.Bookings.Include(b=>b.Hall).AsNoTracking().Where(b=>b.EventDate.StartsWith(prefix)).OrderBy(b=>b.EventDate).ToListAsync();
  static string Safe(string value){if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
  var sb=new StringBuilder("Ma don,Co dau,Chu re,Ngay, Ca,Sanh,So ban,Don gia,Tong tien,Trang thai\r\n");
  foreach(var b in bookings)sb.AppendLine(string.Join(',',b.Id,Safe(b.BrideName),Safe(b.GroomName),b.EventDate,b.Slot,Safe(b.Hall!.Name),b.TableCount,b.PricePerTable.ToString(System.Globalization.CultureInfo.InvariantCulture),(b.PricePerTable*b.TableCount).ToString(System.Globalization.CultureInfo.InvariantCulture),b.Status));
  return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),"text/csv; charset=utf-8",$"VST-tiec-cuoi-{year}.csv");
 }
 [HttpGet("audit"),Authorize(Roles="Admin")]
 public async Task<IActionResult> Audit()=>Ok(await db.AuditLogs.AsNoTracking().OrderByDescending(a=>a.Id).Take(300).ToListAsync());
}
