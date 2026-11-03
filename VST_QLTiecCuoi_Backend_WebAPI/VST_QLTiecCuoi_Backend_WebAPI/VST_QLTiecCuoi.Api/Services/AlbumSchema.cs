using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;

namespace VST_QLTiecCuoi.Services;

public static class AlbumSchema {
 // Additive table for existing SQL Server / SQLite installations.
 public static async Task EnsureAsync(ApplicationDbContext db) {
  var statement=db.Database.IsSqlServer()?
   "IF OBJECT_ID(N'dbo.WeddingPhotos',N'U') IS NULL BEGIN CREATE TABLE [dbo].[WeddingPhotos] ([Id] nvarchar(32) NOT NULL PRIMARY KEY, [BookingId] int NOT NULL, [FileName] nvarchar(200) NOT NULL, [ContentType] nvarchar(30) NOT NULL, [Length] bigint NOT NULL, [UploadedUtc] datetime2 NOT NULL, CONSTRAINT [FK_WeddingPhotos_Bookings_BookingId] FOREIGN KEY([BookingId]) REFERENCES [dbo].[Bookings]([Id])); CREATE INDEX [IX_WeddingPhotos_BookingId] ON [dbo].[WeddingPhotos]([BookingId]); END":
   "CREATE TABLE IF NOT EXISTS WeddingPhotos (Id TEXT NOT NULL PRIMARY KEY, BookingId INTEGER NOT NULL, FileName TEXT NOT NULL, ContentType TEXT NOT NULL, Length INTEGER NOT NULL, UploadedUtc TEXT NOT NULL, FOREIGN KEY(BookingId) REFERENCES Bookings(Id)); CREATE INDEX IF NOT EXISTS IX_WeddingPhotos_BookingId ON WeddingPhotos(BookingId);";
  if(db.Database.IsSqlServer())await db.Database.ExecuteSqlRawAsync(statement);
  else foreach(var sql in statement.Split(';',StringSplitOptions.RemoveEmptyEntries))await db.Database.ExecuteSqlRawAsync(sql);
 }
}
