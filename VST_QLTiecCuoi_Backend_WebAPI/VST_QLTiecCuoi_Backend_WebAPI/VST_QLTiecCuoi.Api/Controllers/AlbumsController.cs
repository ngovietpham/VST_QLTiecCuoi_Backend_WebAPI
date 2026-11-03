using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;

namespace VST_QLTiecCuoi.Controllers;

[ApiController, Route("api/albums"), Authorize(Roles="Manager,User")]
public class AlbumsController(ApplicationDbContext db,UserManager<User> users,IWebHostEnvironment environment):ControllerBase {
 private const long MaxPhotoBytes=4*1024*1024;
 private string Folder=>Path.Combine(environment.ContentRootPath,"Data","albums");

 private async Task<string?> GuestEmail() {
  if(User.IsInRole("Manager"))return null;
  var actor=User.FindFirstValue(ClaimTypes.NameIdentifier);
  var account=actor==null?null:await users.FindByIdAsync(actor);
  return account?.EmailConfirmed==true?account.Email?.Trim().ToUpperInvariant():null;
 }

 private async Task<bool> MayRead(int bookingId) {
  if(User.IsInRole("Manager"))return await db.Bookings.AnyAsync(b=>b.Id==bookingId&&b.Status=="Completed");
  var email=await GuestEmail();
  return email!=null&&await db.Guests.AnyAsync(g=>g.BookingId==bookingId&&g.Booking!.Status=="Completed"&&g.Email.Trim().ToUpper()==email);
 }

 [HttpGet]
 public async Task<IActionResult> ListAlbums() {
  if(User.IsInRole("Manager"))return Ok(await db.Bookings.AsNoTracking().Where(b=>b.Status=="Completed")
   .OrderByDescending(b=>b.EventDate).Select(b=>new{bookingId=b.Id,b.BrideName,b.GroomName,b.EventDate,
    photoCount=db.WeddingPhotos.Count(p=>p.BookingId==b.Id)}).ToListAsync());
  var email=await GuestEmail();if(email==null)return Forbid();
  return Ok(await db.Bookings.AsNoTracking().Where(b=>b.Status=="Completed"&&
   db.Guests.Any(g=>g.BookingId==b.Id&&g.Email.Trim().ToUpper()==email))
   .OrderByDescending(b=>b.EventDate).Select(b=>new{bookingId=b.Id,b.BrideName,b.GroomName,b.EventDate,
    photoCount=db.WeddingPhotos.Count(p=>p.BookingId==b.Id)}).ToListAsync());
 }

 [HttpGet("{bookingId:int}/photos")]
 public async Task<IActionResult> ListPhotos(int bookingId) {
  if(!await MayRead(bookingId))return NotFound();
  return Ok(await db.WeddingPhotos.AsNoTracking().Where(p=>p.BookingId==bookingId)
   .OrderByDescending(p=>p.UploadedUtc).Select(p=>new{p.Id,p.FileName,p.ContentType,p.Length,p.UploadedUtc})
   .ToListAsync());
 }

 [HttpPost("{bookingId:int}/photos"),Authorize(Roles="Manager"),RequestSizeLimit(MaxPhotoBytes+1024*64)]
 public async Task<IActionResult> Upload(int bookingId,[FromForm] IFormFile? image) {
  if(!await db.Bookings.AnyAsync(b=>b.Id==bookingId&&b.Status=="Completed"))return NotFound();
  if(image==null||image.Length<12||image.Length>MaxPhotoBytes)
   return BadRequest(new{message="Chọn ảnh JPEG, PNG hoặc WebP dưới 4 MB."});
  if(await db.WeddingPhotos.CountAsync(p=>p.BookingId==bookingId)>=30)
   return BadRequest(new{message="Mỗi album tối đa 30 ảnh."});
  var head=new byte[12];
  await using(var stream=image.OpenReadStream())await stream.ReadExactlyAsync(head);
  var jpeg=head[0]==0xff&&head[1]==0xd8&&head[2]==0xff;
  var png=head.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
  var webp=head.AsSpan(0,4).SequenceEqual("RIFF"u8)&&head.AsSpan(8,4).SequenceEqual("WEBP"u8);
  var mime=jpeg?"image/jpeg":png?"image/png":webp?"image/webp":null;
  if(mime==null)return BadRequest(new{message="Tệp không phải ảnh JPEG, PNG hoặc WebP."});
  var extension=jpeg?".jpg":png?".png":".webp";
  var name=Path.GetFileName(image.FileName).Trim();
  if(name.Length==0)name="anh-cuoi"+extension;
  if(name.Length>200)name=name[..200];
  var photo=new WeddingPhoto{BookingId=bookingId,FileName=name,ContentType=mime,Length=image.Length};
  Directory.CreateDirectory(Folder);
  var path=Path.Combine(Folder,photo.Id+extension);
  try {
   await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
    await image.CopyToAsync(output);
   db.WeddingPhotos.Add(photo);
   await db.SaveChangesAsync();
  } catch {if(System.IO.File.Exists(path))System.IO.File.Delete(path);throw;}
  return Created($"/api/albums/{bookingId}/photos/{photo.Id}/file",new{photo.Id,photo.FileName,photo.ContentType,photo.Length,photo.UploadedUtc});
 }

 [HttpGet("{bookingId:int}/photos/{id}/file")]
 public async Task<IActionResult> Download(int bookingId,string id) {
  if(!await MayRead(bookingId))return NotFound();
  var photo=await db.WeddingPhotos.AsNoTracking().SingleOrDefaultAsync(p=>p.BookingId==bookingId&&p.Id==id);
  if(photo==null)return NotFound();
  var extension=photo.ContentType switch{"image/jpeg"=>".jpg","image/png"=>".png","image/webp"=>".webp",_=>null};
  if(extension==null)return NotFound();
  var path=Path.Combine(Folder,photo.Id+extension);
  if(!System.IO.File.Exists(path))return NotFound();
  return PhysicalFile(path,photo.ContentType,photo.FileName,enableRangeProcessing:true);
 }

 [HttpDelete("{bookingId:int}/photos/{id}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> Delete(int bookingId,string id) {
  var photo=await db.WeddingPhotos.SingleOrDefaultAsync(p=>p.BookingId==bookingId&&p.Id==id);
  if(photo==null)return NotFound();
  db.WeddingPhotos.Remove(photo);await db.SaveChangesAsync();
  var extension=photo.ContentType switch{"image/jpeg"=>".jpg","image/png"=>".png","image/webp"=>".webp",_=>""};
  var path=Path.Combine(Folder,photo.Id+extension);
  if(System.IO.File.Exists(path))System.IO.File.Delete(path);
  return NoContent();
 }
}
