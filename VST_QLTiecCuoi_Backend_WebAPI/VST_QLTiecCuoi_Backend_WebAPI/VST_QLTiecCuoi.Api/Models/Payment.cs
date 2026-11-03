using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class Payment {
 public int Id {get;set;}
 public int BookingId {get;set;}
 public Booking? Booking {get;set;}
 public decimal Amount {get;set;}
 public string Method {get;set;}="Cash";
 public string Reference {get;set;}="";
 public string CreatedBy {get;set;}="";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
