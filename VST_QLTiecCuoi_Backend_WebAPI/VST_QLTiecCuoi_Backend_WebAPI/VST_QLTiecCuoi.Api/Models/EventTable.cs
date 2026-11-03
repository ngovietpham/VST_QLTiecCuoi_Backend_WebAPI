using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class EventTable {
 public int Id {get;set;}
 public int BookingId {get;set;}
 public Booking? Booking {get;set;}
 public int Number {get;set;}
 public int Capacity {get;set;}
}
