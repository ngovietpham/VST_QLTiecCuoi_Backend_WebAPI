namespace VST_QLTiecCuoi.Models;

public class WeddingPhoto {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public int BookingId {get;set;}
 public Booking? Booking {get;set;}
 public string FileName {get;set;}="";
 public string ContentType {get;set;}="";
 public long Length {get;set;}
 public DateTime UploadedUtc {get;set;}=DateTime.UtcNow;
}
