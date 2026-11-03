using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;
[ApiController,Route("api"),Authorize(Roles="Manager")]
public class CatalogController(ApplicationDbContext db):ControllerBase {
 private bool Manage=>User.IsInRole("Admin")||User.IsInRole("Manager");
 private object MenuView(Menu m)=>new{m.Id,m.Name,m.Description,m.PricePerTable,m.Active,items=m.Items.OrderBy(i=>i.Position).Select(i=>new{i.DishId,name=i.Dish!.Name,i.Dish.Category,i.Position})};
 [HttpGet("halls")]
 public async Task<IActionResult> Halls([FromQuery]string? q) {
  var query=db.Halls.AsNoTracking().Where(h=>Manage||h.Active);
  if(!string.IsNullOrWhiteSpace(q))query=query.Where(h=>h.Name.Contains(q));
  var halls=await query.OrderBy(h=>h.Name).ToListAsync();var tables=await db.HallTables.AsNoTracking().ToListAsync();
  return Ok(halls.Select(h=>new{h.Id,h.Name,h.Description,h.Active,tableCount=tables.Count(t=>t.HallId==h.Id),guestCapacity=tables.Where(t=>t.HallId==h.Id).Sum(t=>t.Capacity)}));
 }
 [HttpPost("halls"),Authorize(Roles="Manager")]
 public async Task<IActionResult> CreateHall(HallRequest r) {
  if(await db.Halls.AnyAsync(h=>h.Name==r.Name.Trim()))return Conflict(new{message="Tên sảnh đã tồn tại."});
  var h=new Hall{Name=r.Name.Trim(),Description=r.Description?.Trim()??"",Active=r.Active};db.Add(h);await db.SaveChangesAsync();return Created($"/api/halls/{h.Id}",h);
 }
 [HttpPut("halls/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> UpdateHall(int id,HallRequest r) {
  var h=await db.Halls.FindAsync(id);if(h==null)return NotFound();
  if(await db.Halls.AnyAsync(x=>x.Id!=id&&x.Name==r.Name.Trim()))return Conflict(new{message="Tên sảnh đã tồn tại."});
  h.Name=r.Name.Trim();h.Description=r.Description?.Trim()??"";h.Active=r.Active;await db.SaveChangesAsync();return Ok(h);
 }
 [HttpDelete("halls/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> DeleteHall(int id) {
  var h=await db.Halls.FindAsync(id);if(h==null)return NotFound();
  if(await db.Bookings.AnyAsync(x=>x.HallId==id)||await db.HallTables.AnyAsync(x=>x.HallId==id))return Conflict(new{message="Sảnh đang có bàn hoặc đơn đặt tiệc. Hãy ngừng hoạt động thay vì xóa."});
  var venue=await db.VenueLocations.FindAsync(id);if(venue!=null)db.Remove(venue);
  db.Remove(h);await db.SaveChangesAsync();return NoContent();
 }
 [HttpGet("halls/{hallId:int}/tables")]
 public async Task<IActionResult> Tables(int hallId) {
  if(!await db.Halls.AnyAsync(h=>h.Id==hallId&&(Manage||h.Active)))return NotFound();
  return Ok(await db.HallTables.AsNoTracking().Where(t=>t.HallId==hallId).OrderBy(t=>t.Number).Select(t=>new{t.Id,t.HallId,t.Number,t.Capacity}).ToListAsync());
 }
 [HttpPost("halls/{hallId:int}/tables"),Authorize(Roles="Manager")]
 public async Task<IActionResult> CreateTable(int hallId,HallTableRequest r) {
  if(!await db.Halls.AnyAsync(h=>h.Id==hallId))return NotFound();
  if(await db.HallTables.AnyAsync(t=>t.HallId==hallId&&t.Number==r.Number))return Conflict(new{message="Số bàn đã tồn tại trong sảnh."});
  var t=new HallTable{HallId=hallId,Number=r.Number,Capacity=r.Capacity};db.Add(t);await db.SaveChangesAsync();return Created($"/api/tables/{t.Id}",new{t.Id,t.HallId,t.Number,t.Capacity});
 }
 [HttpPut("tables/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> UpdateTable(int id,HallTableRequest r) {
  var t=await db.HallTables.FindAsync(id);if(t==null)return NotFound();
  if(await db.HallTables.AnyAsync(x=>x.HallId==t.HallId&&x.Number==r.Number&&x.Id!=id))return Conflict(new{message="Số bàn đã tồn tại."});
  t.Number=r.Number;t.Capacity=r.Capacity;await db.SaveChangesAsync();return Ok(new{t.Id,t.HallId,t.Number,t.Capacity});
 }
 [HttpDelete("tables/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> DeleteTable(int id) {
  var t=await db.HallTables.FindAsync(id);if(t==null)return NotFound();db.Remove(t);await db.SaveChangesAsync();return NoContent();
 }
 [HttpGet("dishes")]
 public async Task<IActionResult> Dishes([FromQuery]string? q,[FromQuery]string? category) {
  var query=db.Dishes.AsNoTracking().Where(d=>Manage||d.Active);
  if(!string.IsNullOrWhiteSpace(q))query=query.Where(d=>d.Name.Contains(q));
  if(!string.IsNullOrWhiteSpace(category))query=query.Where(d=>d.Category==category);
  return Ok(await query.OrderBy(d=>d.Category).ThenBy(d=>d.Name).ToListAsync());
 }
 [HttpPost("dishes"),Authorize(Roles="Manager")]
 public async Task<IActionResult> CreateDish(DishRequest r) {
  if(await db.Dishes.AnyAsync(d=>d.Name==r.Name.Trim()))return Conflict(new{message="Tên món đã tồn tại."});
  var d=new Dish{Name=r.Name.Trim(),Category=r.Category.Trim(),Description=r.Description?.Trim()??"",Active=r.Active};db.Add(d);await db.SaveChangesAsync();return Created($"/api/dishes/{d.Id}",d);
 }
 [HttpPut("dishes/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> UpdateDish(int id,DishRequest r) {
  var d=await db.Dishes.FindAsync(id);if(d==null)return NotFound();
  if(await db.Dishes.AnyAsync(x=>x.Id!=id&&x.Name==r.Name.Trim()))return Conflict(new{message="Tên món đã tồn tại."});
  d.Name=r.Name.Trim();d.Category=r.Category.Trim();d.Description=r.Description?.Trim()??"";d.Active=r.Active;await db.SaveChangesAsync();return Ok(d);
 }
 [HttpDelete("dishes/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> DeleteDish(int id) {
  var d=await db.Dishes.FindAsync(id);if(d==null)return NotFound();
  if(await db.MenuItems.AnyAsync(i=>i.DishId==id))return Conflict(new{message="Món đang nằm trong thực đơn. Gỡ khỏi thực đơn hoặc ngừng phục vụ."});
  db.Remove(d);await db.SaveChangesAsync();return NoContent();
 }
 [HttpGet("menus")]
 public async Task<IActionResult> Menus([FromQuery]string? q) {
  var query=db.Menus.AsNoTracking().Include(m=>m.Items).ThenInclude(i=>i.Dish).Where(m=>Manage||(m.Active&&m.Items.All(i=>i.Dish!.Active)));
  if(!string.IsNullOrWhiteSpace(q))query=query.Where(m=>m.Name.Contains(q));
  return Ok((await query.OrderBy(m=>m.Name).ToListAsync()).Select(MenuView));
 }
 private async Task<string?> CheckMenu(MenuRequest r,int id=0) {
  if(r.DishIds.Distinct().Count()!=r.DishIds.Length)return "Không chọn trùng món.";
  if(await db.Dishes.CountAsync(d=>r.DishIds.Contains(d.Id)&&(!r.Active||d.Active))!=r.DishIds.Length)return "Món không tồn tại hoặc đã ngừng phục vụ.";
  if(await db.Menus.AnyAsync(m=>m.Name==r.Name.Trim()&&m.Id!=id))return "Tên thực đơn đã tồn tại.";return null;
 }
 [HttpPost("menus"),Authorize(Roles="Manager")]
 public async Task<IActionResult> CreateMenu(MenuRequest r) {
  var error=await CheckMenu(r);if(error!=null)return BadRequest(new{message=error});
  var m=new Menu{Name=r.Name.Trim(),Description=r.Description?.Trim()??"",PricePerTable=r.PricePerTable,Active=r.Active,Items=r.DishIds.Select((id,p)=>new MenuItem{DishId=id,Position=p}).ToList()};
  db.Add(m);await db.SaveChangesAsync();return Created($"/api/menus/{m.Id}",new{m.Id,m.Name,m.PricePerTable,m.Active});
 }
 [HttpPut("menus/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> UpdateMenu(int id,MenuRequest r) {
  var m=await db.Menus.Include(m=>m.Items).SingleOrDefaultAsync(m=>m.Id==id);if(m==null)return NotFound();
  var error=await CheckMenu(r,id);if(error!=null)return BadRequest(new{message=error});
  await using var tx=await db.Database.BeginTransactionAsync();
  db.MenuItems.RemoveRange(m.Items);await db.SaveChangesAsync();m.Items.Clear();
  m.Name=r.Name.Trim();m.Description=r.Description?.Trim()??"";m.PricePerTable=r.PricePerTable;m.Active=r.Active;
  m.Items=r.DishIds.Select((dish,p)=>new MenuItem{MenuId=id,DishId=dish,Position=p}).ToList();await db.SaveChangesAsync();await tx.CommitAsync();return Ok(new{m.Id,m.Name,m.PricePerTable,m.Active});
 }
 [HttpDelete("menus/{id:int}"),Authorize(Roles="Manager")]
 public async Task<IActionResult> DeleteMenu(int id) {
  var m=await db.Menus.Include(m=>m.Items).SingleOrDefaultAsync(m=>m.Id==id);if(m==null)return NotFound();
  if(await db.Bookings.AnyAsync(b=>b.MenuId==id))return Conflict(new{message="Thực đơn đã có đơn đặt. Hãy ngừng phục vụ để giữ lịch sử."});
  db.MenuItems.RemoveRange(m.Items);db.Remove(m);await db.SaveChangesAsync();return NoContent();
 }
 [HttpGet("availability")]
 public async Task<IActionResult> Availability([FromQuery]string date) {
  if(!DateOnly.TryParseExact(date,"yyyy-MM-dd",out _))return BadRequest(new{message="Ngày không hợp lệ."});
  var halls=await db.Halls.AsNoTracking().Where(h=>h.Active).ToListAsync();var reserved=await db.Reservations.Where(r=>r.EventDate==date).ToListAsync();
  return Ok(halls.SelectMany(h=>new[]{"Lunch","Dinner"}.Select(slot=>new{hallId=h.Id,hallName=h.Name,date,slot,available=!reserved.Any(r=>r.HallId==h.Id&&r.Slot==slot)})));
 }
}
