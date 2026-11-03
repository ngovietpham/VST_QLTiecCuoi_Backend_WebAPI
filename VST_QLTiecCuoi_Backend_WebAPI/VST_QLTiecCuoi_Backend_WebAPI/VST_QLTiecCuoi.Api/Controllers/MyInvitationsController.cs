using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Controllers;

[ApiController, Route("api/my-invitations"), Authorize(Roles="User")]
public class MyInvitationsController(ApplicationDbContext db, UserManager<User> users):ControllerBase {
 private async Task<IQueryable<Guest>?> Mine() {
  var user=await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  if(user==null||!user.EmailConfirmed||string.IsNullOrWhiteSpace(user.Email))return null;
  var email=user.Email.Trim().ToUpperInvariant();
  return db.Guests.AsNoTracking().Where(g=>g.Email.Trim().ToUpper()==email)
   .Include(g=>g.Booking).ThenInclude(b=>b!.Hall).Include(g=>g.EventTable);
 }
 [HttpGet]
 public async Task<IActionResult> List() {
  var mine=await Mine();if(mine==null)return Forbid();
  return Ok(await mine.OrderBy(g=>g.Booking!.EventDate).ThenBy(g=>g.Booking!.Slot).ThenBy(g=>g.Id)
   .Select(g=>new{invitationId=g.Id,bookingId=g.BookingId,guestName=g.Name,g.PartySize,
    g.Booking!.BrideName,g.Booking.GroomName,g.Booking.EventDate,g.Booking.Slot,g.Booking.Status,
    hallName=g.Booking.Hall!.Name,tableNumber=g.EventTable==null?(int?)null:g.EventTable.Number,g.CheckedInAt}).ToListAsync());
 }
 [HttpGet("{id:int}")]
 public async Task<IActionResult> Detail(int id) {
  var mine=await Mine();if(mine==null)return Forbid();
  var g=await mine.SingleOrDefaultAsync(g=>g.Id==id);if(g==null)return NotFound();
  var b=g.Booking!;
  var venue=await db.VenueLocations.AsNoTracking().SingleOrDefaultAsync(v=>v.HallId==b.HallId);
  return Ok(new{invitationId=g.Id,bookingId=g.BookingId,guestName=g.Name,g.PartySize,
   b.BrideName,b.GroomName,b.EventDate,b.Slot,b.Status,
   hallName=b.Hall!.Name,hallDescription=b.Hall.Description,
   venue, menuSnapshot=b.MenuSnapshot,tableNumber=g.EventTable?.Number,g.CheckedInAt,
   qrPayload=b.Status=="Approved"&&g.CheckedInAt==null?$"VST:{g.Token}":null});
 }
 [HttpGet("{id:int}/ticket")]
 public async Task<IActionResult> Ticket(int id) {
  var mine=await Mine();if(mine==null)return Forbid();
  var g=await mine.SingleOrDefaultAsync(g=>g.Id==id);if(g==null)return NotFound();
  if(g.Booking!.Status!="Approved"||g.CheckedInAt!=null)return Conflict(new{message="Vé đã dùng hoặc tiệc không còn hoạt động."});
  using var data=QRCodeGenerator.GenerateQrCode($"VST:{g.Token}",QRCodeGenerator.ECCLevel.M);
  using var qr=new PngByteQRCode(data);
  return File(qr.GetGraphic(8),"image/png",$"VST-ve-moi-{id}.png");
 }
}
