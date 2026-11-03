using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api/bookings/{bookingId:int}/payments"),Authorize(Roles="Manager")]
public class PaymentsController(ApplicationDbContext db,WeddingService service):ControllerBase {
 private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet]
 public async Task<IActionResult> List(int bookingId) {
  if(!await db.Bookings.AnyAsync(b=>b.Id==bookingId&&(b.UserId==Actor||WeddingService.Staff(User))))return NotFound();
  return Ok(await db.Payments.AsNoTracking().Where(p=>p.BookingId==bookingId).OrderByDescending(p=>p.Id).Select(p=>new{p.Id,p.Amount,p.Method,p.Reference,p.CreatedAt}).ToListAsync());
 }
 [HttpPost,Authorize(Roles="Manager")]
 public async Task<IActionResult> Record(int bookingId,PaymentRequest r) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var b=await db.Bookings.FindAsync(bookingId);if(b==null)return NotFound();if(b.Status is not ("Approved" or "Completed"))return Conflict(new{message="Chỉ thu tiền cho đơn đã được duyệt."});
  var existing=await db.Payments.SingleOrDefaultAsync(p=>p.BookingId==bookingId&&p.Reference==r.Reference.Trim());
  if(existing!=null)return existing.Amount==r.Amount&&existing.Method==r.Method?Ok(new{existing.Id,message="Phiếu thu này đã được ghi nhận."}):Conflict(new{message="Mã tham chiếu đã dùng với số tiền khác."});
  var paid=(await db.Payments.Where(p=>p.BookingId==bookingId).Select(p=>p.Amount).ToListAsync()).Sum();
  if(paid+r.Amount>b.PricePerTable*b.TableCount)return BadRequest(new{message="Số tiền vượt số còn phải thanh toán."});
  var payment=new Payment{BookingId=bookingId,Amount=r.Amount,Method=r.Method,Reference=r.Reference.Trim(),CreatedBy=Actor};db.Add(payment);service.Audit(Actor,"PaymentRecord",$"Booking {bookingId}: {r.Amount}");await db.SaveChangesAsync();await tx.CommitAsync();
  return Created($"/api/bookings/{bookingId}/payments/{payment.Id}",new{payment.Id,payment.Amount,payment.Method,payment.Reference,payment.CreatedAt});
 }
}
