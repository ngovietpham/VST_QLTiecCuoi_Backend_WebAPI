using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;
public static class VenueSchema {
 // Additive upgrade: existing hall, booking and guest data is never rewritten.
 public static async Task EnsureAsync(ApplicationDbContext db) {
  var statement=db.Database.IsSqlServer()?
   "IF OBJECT_ID(N'dbo.VenueLocations',N'U') IS NULL CREATE TABLE [dbo].[VenueLocations] (HallId int NOT NULL PRIMARY KEY, RestaurantName nvarchar(150) NOT NULL, Address nvarchar(500) NOT NULL, MapsUrl nvarchar(1000) NOT NULL, FOREIGN KEY(HallId) REFERENCES Halls(Id));":
   "CREATE TABLE IF NOT EXISTS VenueLocations (HallId INTEGER NOT NULL PRIMARY KEY, RestaurantName TEXT NOT NULL, Address TEXT NOT NULL, MapsUrl TEXT NOT NULL, FOREIGN KEY(HallId) REFERENCES Halls(Id));";
  await db.Database.ExecuteSqlRawAsync(statement);
 }
}
