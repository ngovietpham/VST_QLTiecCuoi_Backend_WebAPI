using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Repositories;
public class WeddingRepository(ApplicationDbContext db):IWeddingRepository {
 public IQueryable<Booking> Bookings=>db.Bookings.Include(b=>b.Hall).Include(b=>b.Menu).Include(b=>b.User);
 public Task<Booking?> FindAsync(int id)=>Bookings.SingleOrDefaultAsync(b=>b.Id==id);
 public Task<int> SaveAsync()=>db.SaveChangesAsync();
}
