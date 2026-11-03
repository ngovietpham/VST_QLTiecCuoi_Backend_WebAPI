using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class Reservation {
 public int HallId {get;set;}
 public string EventDate {get;set;}="";
 public string Slot {get;set;}="";
 public int BookingId {get;set;}
 public Booking? Booking {get;set;}
}
