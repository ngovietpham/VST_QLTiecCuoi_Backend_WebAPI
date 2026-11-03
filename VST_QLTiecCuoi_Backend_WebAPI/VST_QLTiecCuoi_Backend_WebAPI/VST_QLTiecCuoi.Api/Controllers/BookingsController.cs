using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Repositories;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/bookings"),Authorize(Roles="Manager")]
public class BookingsController(ApplicationDbContext db,IWeddingRepository repo,WeddingService service):ControllerBase {
 private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 private bool Staff=>WeddingService.Staff(User);
 private bool Manage=>User.IsInRole("Admin")||User.IsInRole("Manager");
 private async Task<Booking?> Accessible(int id)=>await repo.Bookings.SingleOrDefaultAsync(b=>b.Id==id&&(Staff||b.UserId==Actor));
 [HttpGet]
 public async Task<IActionResult> List([FromQuery]string? q,[FromQuery]string? status,[FromQuery]string? date) {
  var query=repo.Bookings.AsNoTracking().Where(b=>Staff||b.UserId==Actor);
  if(!string.IsNullOrWhiteSpace(q))query=query.Where(b=>b.BrideName.Contains(q)||b.GroomName.Contains(q)||b.Phone.Contains(q));
  if(!string.IsNullOrWhiteSpace(status))query=query.Where(b=>b.Status==status);
  if(!string.IsNullOrWhiteSpace(date))query=query.Where(b=>b.EventDate==date);
  return Ok(await query.OrderByDescending(b=>b.Id).Take(500).Select(b=>new{b.Id,b.BrideName,b.GroomName,b.Phone,b.EventDate,b.Slot,b.TableCount,b.Status,b.PricePerTable,total=b.PricePerTable*b.TableCount,hallName=b.Hall!.Name,menuName=b.Menu!.Name,b.CreatedAt}).ToListAsync());
 }
 [HttpGet("{id:int}")]
 public async Task<IActionResult> Detail(int id) {
  var b=await Accessible(id);if(b==null)return NotFound();
  var payments=await db.Payments.AsNoTracking().Where(p=>p.BookingId==id).OrderByDescending(p=>p.Id).Select(p=>new{p.Id,p.Amount,p.Method,p.Reference,p.CreatedAt}).ToListAsync();
  return Ok(new{b.Id,b.UserId,customerName=b.User!.FullName,b.HallId,hallName=b.Hall!.Name,b.MenuId,menuName=b.Menu!.Name,b.MenuSnapshot,b.BrideName,b.GroomName,b.Phone,b.EventDate,b.Slot,b.TableCount,b.PricePerTable,total=b.PricePerTable*b.TableCount,paid=payments.Sum(p=>p.Amount),b.Status,b.Note,b.DecisionNote,b.CreatedAt,payments});
 }
 [HttpPost,Authorize(Roles="Manager")]
 public async Task<IActionResult> Create(BookingRequest r) {
  var b=new Booking{UserId=Actor};await service.ApplyAsync(b,r);db.Add(b);service.Audit(Actor,"BookingCreate",$"{b.BrideName} & {b.GroomName}");await repo.SaveAsync();return Created($"/api/bookings/{b.Id}",new{b.Id,b.Status});
 }
 [HttpPut("{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Update(int id,BookingRequest r) {
  var b=await Accessible(id);if(b==null)return NotFound();
  if(b.Status!="Pending")return Conflict(new{message="Chỉ sửa đơn đang chờ duyệt."});
  await service.ApplyAsync(b,r);service.Audit(Actor,"BookingUpdate",id.ToString());await repo.SaveAsync();return Ok(new{b.Id,b.Status});
 }
 [HttpDelete("{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Delete(int id) {
  var b=await Accessible(id);if(b==null)return NotFound();
  if(b.Status is not ("Pending" or "Rejected" or "Cancelled"))return Conflict(new{message="Không xóa đơn đã duyệt hoặc đã hoàn tất."});
  if(await db.Payments.AnyAsync(p=>p.BookingId==id)||await db.Guests.AnyAsync(g=>g.BookingId==id)||await db.EventTables.AnyAsync(t=>t.BookingId==id)||await db.WeddingPhotos.AnyAsync(p=>p.BookingId==id))return Conflict(new{message="Đơn có dữ liệu liên quan. Giữ đơn đã hủy để bảo toàn lịch sử."});
  db.Remove(b);service.Audit(Actor,"BookingDelete",id.ToString());await repo.SaveAsync();return NoContent();
 }
 [HttpPost("{id:int}/approve"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Approve(int id) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var b=await db.Bookings.FindAsync(id);if(b==null)return NotFound();
  if(b.Status!="Pending")return Conflict(new{message="Chỉ duyệt đơn đang chờ."});
  if(DateOnly.Parse(b.EventDate)<WeddingService.Today)return Conflict(new{message="Ngày tổ chức đã qua."});
  if(!await db.Halls.AnyAsync(h=>h.Id==b.HallId&&h.Active)||!await db.Menus.AnyAsync(m=>m.Id==b.MenuId&&m.Active&&m.Items.All(i=>i.Dish!.Active)))return Conflict(new{message="Sảnh hoặc thực đơn đã ngừng hoạt động."});
  var tables=await db.HallTables.Where(t=>t.HallId==b.HallId).OrderBy(t=>t.Number).Take(b.TableCount).ToListAsync();
  if(tables.Count!=b.TableCount)return Conflict(new{message="Sảnh không còn đủ số bàn."});
  if(await db.Reservations.AnyAsync(r=>r.HallId==b.HallId&&r.EventDate==b.EventDate&&r.Slot==b.Slot))return Conflict(new{message="Sảnh đã được duyệt cho tiệc khác trong ca này."});
  db.Reservations.Add(new(){HallId=b.HallId,EventDate=b.EventDate,Slot=b.Slot,BookingId=id});
  db.EventTables.AddRange(tables.Select(t=>new EventTable{BookingId=id,Number=t.Number,Capacity=t.Capacity}));
  b.Status="Approved";service.Audit(Actor,"BookingApprove",id.ToString());
  try{await repo.SaveAsync();await tx.CommitAsync();}catch(DbUpdateException){return Conflict(new{message="Sảnh vừa được người khác đặt. Hãy tải lại lịch."});}
  return Ok(new{b.Id,b.Status});
 }
 [HttpPost("{id:int}/reject"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Reject(int id,DecisionRequest r) {
  var b=await db.Bookings.FindAsync(id);if(b==null)return NotFound();if(b.Status!="Pending")return Conflict(new{message="Chỉ từ chối đơn đang chờ."});
  b.Status="Rejected";b.DecisionNote=r.Note?.Trim()??"";service.Audit(Actor,"BookingReject",id.ToString());await repo.SaveAsync();return Ok(new{b.Status});
 }
 [HttpPost("{id:int}/cancel"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Cancel(int id,DecisionRequest r) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var b=await Accessible(id);if(b==null)return NotFound();
  if(b.Status is not ("Pending" or "Approved"))return Conflict(new{message="Trạng thái hiện tại không thể hủy."});
  if(b.Status=="Approved"&&!Manage)return Conflict(new{message="Tiệc đã duyệt. Vui lòng liên hệ quản lý để hủy."});
  if(await db.Payments.AnyAsync(p=>p.BookingId==id)||await db.Guests.AnyAsync(g=>g.BookingId==id&&g.CheckedInAt!=null))return Conflict(new{message="Đơn đã thu tiền hoặc có khách check-in. Cần xử lý nghiệp vụ trước khi hủy."});
  await db.Reservations.Where(r=>r.BookingId==id).ExecuteDeleteAsync();b.Status="Cancelled";b.DecisionNote=r.Note?.Trim()??"";service.Audit(Actor,"BookingCancel",id.ToString());await repo.SaveAsync();await tx.CommitAsync();return Ok(new{b.Status});
 }
 [HttpPost("{id:int}/complete"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Complete(int id) {
  var b=await db.Bookings.FindAsync(id);if(b==null)return NotFound();if(b.Status!="Approved")return Conflict(new{message="Chỉ hoàn tất tiệc đã duyệt."});
  if(DateOnly.Parse(b.EventDate)>WeddingService.Today)return Conflict(new{message="Chưa đến ngày tổ chức."});
  var paid=(await db.Payments.Where(p=>p.BookingId==id).Select(p=>p.Amount).ToListAsync()).Sum();
  if(paid<b.PricePerTable*b.TableCount)return Conflict(new{message="Chưa thanh toán đủ tổng tiền."});
  b.Status="Completed";service.Audit(Actor,"BookingComplete",id.ToString());await repo.SaveAsync();return Ok(new{b.Status});
 }
 [HttpGet("{id:int}/tables")]
 public async Task<IActionResult> Tables(int id) {
  if(await Accessible(id)==null)return NotFound();
  var tables=await db.EventTables.AsNoTracking().Where(t=>t.BookingId==id).OrderBy(t=>t.Number).ToListAsync();var guests=await db.Guests.AsNoTracking().Where(g=>g.BookingId==id).ToListAsync();
  return Ok(tables.Select(t=>new{t.Id,t.Number,t.Capacity,reserved=guests.Where(g=>g.EventTableId==t.Id).Sum(g=>g.PartySize),currentGuest=guests.Where(g=>g.EventTableId==t.Id&&g.CheckedInAt!=null).Sum(g=>g.PartySize),isFull=guests.Where(g=>g.EventTableId==t.Id&&g.CheckedInAt!=null).Sum(g=>g.PartySize)>=t.Capacity}));
 }
}
