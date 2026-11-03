using System.ComponentModel.DataAnnotations;
namespace VST_QLTiecCuoi.Models;
public class VenueLocation {
 public int HallId {get;set;}
 public string RestaurantName {get;set;}="";
 public string Address {get;set;}="";
 public string MapsUrl {get;set;}="";
}
public record VenueRequest([Required,StringLength(150)] string RestaurantName,
 [Required,StringLength(500)] string Address,[StringLength(1000)] string MapsUrl);
