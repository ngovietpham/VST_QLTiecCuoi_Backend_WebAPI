using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/bookings/{bookingId:int}/guests"),Authorize(Roles="Manager")]
public class GuestsController(ApplicationDbContext db):ControllerBase {
 private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 private bool Staff=>WeddingService.Staff(User);
 private Task<Booking?> Accessible(int id)=>db.Bookings.SingleOrDefaultAsync(b=>b.Id==id&&(Staff||b.UserId==Actor));
 private static object View(Guest g)=>new{g.Id,g.BookingId,g.Name,g.Email,g.Phone,g.PartySize,g.EventTableId,g.CheckedInAt,qrPayload=$"VST:{g.Token}"};
 [HttpGet]
 public async Task<IActionResult> List(int bookingId,[FromQuery]string? q) {
  if(await Accessible(bookingId)==null)return NotFound();var query=db.Guests.AsNoTracking().Where(g=>g.BookingId==bookingId);
  if(!string.IsNullOrWhiteSpace(q))query=query.Where(g=>g.Name.Contains(q)||g.Phone.Contains(q));
  return Ok((await query.OrderBy(g=>g.Name).ToListAsync()).Select(View));
 }
 private async Task<string?> Validate(int bookingId,int guestId,GuestRequest r) {
  if(string.IsNullOrWhiteSpace(r.Email))return "Cần email đã đăng ký của khách để hiển thị lời mời trong ứng dụng.";
  if(r.EventTableId==null)return "Hãy chọn bàn cho khách mời.";
  var table=await db.EventTables.SingleOrDefaultAsync(t=>t.Id==r.EventTableId&&t.BookingId==bookingId);if(table==null)return "Bàn không thuộc tiệc này.";
  var reserved=(await db.Guests.Where(g=>g.BookingId==bookingId&&g.EventTableId==r.EventTableId&&g.Id!=guestId).Select(g=>g.PartySize).ToListAsync()).Sum();
  if(reserved+r.PartySize>table.Capacity)return "Số người mời vượt số chỗ còn lại của bàn.";return null;
 }
 [HttpPost,Authorize(Roles="Manager")]
 public async Task<IActionResult> Create(int bookingId,GuestRequest r) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var b=await Accessible(bookingId);if(b==null)return NotFound();if(b.Status!="Approved")return Conflict(new{message="Chỉ thêm khách cho tiệc đã được duyệt."});
  var error=await Validate(bookingId,0,r);if(error!=null)return BadRequest(new{message=error});
  var g=new Guest{BookingId=bookingId,Name=r.Name.Trim(),Email=r.Email?.Trim()??"",Phone=r.Phone?.Trim()??"",PartySize=r.PartySize,EventTableId=r.EventTableId,Token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24))};
  db.Add(g);await db.SaveChangesAsync();await tx.CommitAsync();return Created($"/api/bookings/{bookingId}/guests/{g.Id}",View(g));
 }
 [HttpPut("{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Update(int bookingId,int id,GuestRequest r) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var b=await Accessible(bookingId);if(b==null)return NotFound();if(b.Status!="Approved")return Conflict(new{message="Tiệc không ở trạng thái cho phép sửa khách."});
  var g=await db.Guests.SingleOrDefaultAsync(g=>g.Id==id&&g.BookingId==bookingId);if(g==null)return NotFound();
  if(g.CheckedInAt!=null)return Conflict(new{message="Khách đã check-in. Nhờ nhân viên hoàn tác trước khi sửa."});
  var error=await Validate(bookingId,id,r);if(error!=null)return BadRequest(new{message=error});
  g.Name=r.Name.Trim();g.Email=r.Email?.Trim()??"";g.Phone=r.Phone?.Trim()??"";g.PartySize=r.PartySize;g.EventTableId=r.EventTableId;
  await db.SaveChangesAsync();await tx.CommitAsync();return Ok(View(g));
 }
 [HttpDelete("{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Delete(int bookingId,int id) {
  var b=await Accessible(bookingId);if(b==null)return NotFound();if(b.Status!="Approved")return Conflict(new{message="Tiệc không ở trạng thái cho phép xóa khách."});
  var g=await db.Guests.SingleOrDefaultAsync(g=>g.Id==id&&g.BookingId==bookingId);if(g==null)return NotFound();
  if(g.CheckedInAt!=null)return Conflict(new{message="Không xóa khách đã check-in."});db.Remove(g);await db.SaveChangesAsync();return NoContent();
 }
}
