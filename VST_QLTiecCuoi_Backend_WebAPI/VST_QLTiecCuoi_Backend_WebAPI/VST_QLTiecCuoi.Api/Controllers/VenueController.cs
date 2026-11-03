using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Controllers;
[ApiController, Route("api/halls"), Authorize(Roles="Manager")]
public class VenueController(ApplicationDbContext db):ControllerBase {
 public static bool MapUrl(string? value) {
  if(string.IsNullOrWhiteSpace(value))return true;
  if(!Uri.TryCreate(value,UriKind.Absolute,out var u)||u.Scheme!="https"||!string.IsNullOrEmpty(u.UserInfo))return false;
  return u.Host is "maps.app.goo.gl" or "maps.google.com" || (u.Host is "google.com" or "www.google.com" && u.AbsolutePath.StartsWith("/maps")) || (u.Host=="goo.gl" && u.AbsolutePath.StartsWith("/maps"));
 }
 [HttpGet("{id:int}/location")]
 public async Task<IActionResult> Location(int id)=>!await db.Halls.AnyAsync(h=>h.Id==id)?NotFound():Ok(await db.VenueLocations.FindAsync(id));
 [HttpPut("{id:int}/location")]
 public async Task<IActionResult> SetLocation(int id,VenueRequest r) {
  if(!await db.Halls.AnyAsync(h=>h.Id==id))return NotFound();
  if(!MapUrl(r.MapsUrl))return BadRequest(new{message="Chỉ dùng liên kết HTTPS chính thức Google Maps."});
  var v=await db.VenueLocations.FindAsync(id);if(v==null){v=new(){HallId=id};db.Add(v);}
  v.RestaurantName=r.RestaurantName.Trim();v.Address=r.Address.Trim();
  v.MapsUrl=string.IsNullOrWhiteSpace(r.MapsUrl)?"https://www.google.com/maps/search/?api=1&query="+Uri.EscapeDataString(v.RestaurantName+" "+v.Address):r.MapsUrl.Trim();
  await db.SaveChangesAsync();return Ok(v);
 }
}
