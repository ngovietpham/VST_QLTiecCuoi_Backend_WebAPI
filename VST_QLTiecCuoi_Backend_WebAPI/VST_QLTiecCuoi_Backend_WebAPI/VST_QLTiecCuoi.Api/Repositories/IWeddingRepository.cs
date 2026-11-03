using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Repositories;
public interface IWeddingRepository {
 IQueryable<Booking> Bookings {get;}
 Task<Booking?> FindAsync(int id);
 Task<int> SaveAsync();
}
