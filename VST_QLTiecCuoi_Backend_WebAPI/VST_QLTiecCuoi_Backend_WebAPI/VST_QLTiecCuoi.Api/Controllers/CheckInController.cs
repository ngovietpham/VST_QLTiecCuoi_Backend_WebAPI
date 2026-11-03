using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api"),Authorize(Roles="Manager")]
public class CheckInController(ApplicationDbContext db,EmailOtpService mail,WeddingService service,IConfiguration config):ControllerBase {
 private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 private static byte[] Png(string payload) {using var data=QRCodeGenerator.GenerateQrCode(payload,QRCodeGenerator.ECCLevel.M);using var code=new PngByteQRCode(data);return code.GetGraphic(8);}
 private async Task<Guest> FromToken(string token) {
  WeddingService.Require(Regex.IsMatch(token,@"^VST:[A-F0-9]{48}$"),"Mã QR không đúng định dạng.");
  var value=token[4..];var g=await db.Guests.Include(g=>g.Booking).ThenInclude(b=>b!.Hall).Include(g=>g.EventTable).SingleOrDefaultAsync(g=>g.Token==value);
  WeddingService.Require(g!=null,"Vé không tồn tại, đã bị xóa hoặc đã đổi mã.",404);return g!;
 }
 [HttpPost("check-in/preview"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Preview(ScanRequest r) {
  var g=await FromToken(r.Token);var b=g.Booking!;
  return Ok(new{g.Id,g.Name,g.Phone,g.PartySize,g.CheckedInAt,bookingId=b.Id,b.BrideName,b.GroomName,b.EventDate,b.Slot,b.Status,hallName=b.Hall!.Name,tableNumber=g.EventTable?.Number,canCheckIn=b.Status=="Approved"&&b.EventDate==WeddingService.Today.ToString("yyyy-MM-dd")&&g.CheckedInAt==null});
 }
 [HttpPost("check-in/confirm"),Authorize(Roles="Manager")]
 public async Task<IActionResult> CheckIn(ScanRequest r) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var g=await FromToken(r.Token);var b=g.Booking!;
  if(b.Status!="Approved")return Conflict(new{message="Tiệc chưa được duyệt, đã hủy hoặc đã kết thúc."});
  if(b.EventDate!=WeddingService.Today.ToString("yyyy-MM-dd"))return Conflict(new{message="Chỉ check-in trong ngày tổ chức tiệc (giờ Việt Nam)."});
  if(g.CheckedInAt!=null)return Conflict(new{message="Vé đã được check-in. Không ghi nhận lần thứ hai."});
  if(g.EventTable==null)return Conflict(new{message="Khách chưa được xếp bàn."});
  var current=(await db.Guests.Where(x=>x.EventTableId==g.EventTableId&&x.CheckedInAt!=null).Select(x=>x.PartySize).ToListAsync()).Sum();
  if(current+g.PartySize>g.EventTable.Capacity)return Conflict(new{message="Bàn không còn đủ chỗ."});
  g.CheckedInAt=DateTime.UtcNow;g.CheckedInBy=Actor;service.Audit(Actor,"GuestCheckIn",$"Guest {g.Id}, booking {b.Id}");await db.SaveChangesAsync();await tx.CommitAsync();
  return Ok(new{message="Check-in thành công.",g.Name,g.PartySize,tableNumber=g.EventTable.Number,g.CheckedInAt});
 }
 [HttpPost("bookings/{bookingId:int}/guests/{id:int}/undo-check-in"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Undo(int bookingId,int id) {
  var g=await db.Guests.Include(g=>g.Booking).SingleOrDefaultAsync(g=>g.Id==id&&g.BookingId==bookingId);if(g==null)return NotFound();
  if(g.Booking!.Status!="Approved")return Conflict(new{message="Tiệc đã kết thúc hoặc không còn hoạt động."});
  if(g.CheckedInAt==null)return Conflict(new{message="Khách chưa check-in."});
  g.CheckedInAt=null;g.CheckedInBy=null;service.Audit(Actor,"GuestUndoCheckIn",id.ToString());await db.SaveChangesAsync();return Ok(new{message="Đã hoàn tác check-in."});
 }
 private Task<Guest?> Owned(int bid,int id)=>db.Guests.Include(g=>g.Booking).ThenInclude(b=>b!.Hall).Include(g=>g.EventTable)
  .SingleOrDefaultAsync(g=>g.Id==id&&g.BookingId==bid&&(g.Booking!.UserId==Actor||WeddingService.Staff(User)));
 [HttpGet("bookings/{bookingId:int}/guests/{id:int}/ticket")]
 public async Task<IActionResult> Ticket(int bookingId,int id) {
  var g=await Owned(bookingId,id);if(g==null)return NotFound();if(g.Booking!.Status!="Approved")return Conflict(new{message="Vé chỉ có hiệu lực khi tiệc đang được duyệt."});
  return File(Png($"VST:{g.Token}"),"image/png",$"VST-ve-{id}.png");
 }
 [HttpPost("bookings/{bookingId:int}/guests/{id:int}/send-invitation"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Invitation(int bookingId,int id) {
  var g=await Owned(bookingId,id);if(g==null)return NotFound();var b=g.Booking!;
  if(b.Status!="Approved")return Conflict(new{message="Chỉ gửi thư mời cho tiệc đã duyệt."});
  if(string.IsNullOrWhiteSpace(g.Email))return BadRequest(new{message="Khách chưa có địa chỉ email."});
  if(await db.AuditLogs.AnyAsync(a=>a.Action=="InvitationSend"&&a.Detail==id.ToString()&&a.CreatedAt>DateTime.UtcNow.AddMinutes(-1)))return StatusCode(429,new{message="Chờ một phút trước khi gửi lại thư mời."});
  var slot=b.Slot=="Lunch"?"11:00–15:00":"17:00–22:00";
  try{await mail.DeliverAsync(g.Email,$"Thiệp mời: {b.BrideName} & {b.GroomName}",$"Trân trọng kính mời {g.Name} cùng {g.PartySize} người (tính cả bạn) đến dự tiệc cưới {b.BrideName} & {b.GroomName}.\nNgày: {b.EventDate}, giờ: {slot}\nSảnh: {b.Hall!.Name}, bàn: {g.EventTable?.Number}\nVui lòng xuất trình ảnh QR đính kèm khi đến. Mỗi vé dùng một lần cho cả nhóm.\nVST Quản lý tiệc cưới",Png($"VST:{g.Token}"));}
  catch(InvalidOperationException ex){return StatusCode(503,new{message=ex.Message});}
  service.Audit(Actor,"InvitationSend",id.ToString());await db.SaveChangesAsync();return Ok(new{message=config["Smtp:Mode"]=="File"?"Thiệp mời đã được lưu trong hộp thư demo.":"Đã gửi thiệp mời kèm mã QR."});
 }
 [HttpPost("bookings/{bookingId:int}/guests/{id:int}/rotate-qr"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Rotate(int bookingId,int id) {
  var g=await Owned(bookingId,id);if(g==null)return NotFound();if(g.CheckedInAt!=null||g.Booking!.Status!="Approved")return Conflict(new{message="Không đổi mã của vé đã check-in hoặc tiệc không hoạt động."});
  g.Token=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));service.Audit(Actor,"GuestRotateQR",id.ToString());await db.SaveChangesAsync();return Ok(new{qrPayload=$"VST:{g.Token}"});
 }
}
