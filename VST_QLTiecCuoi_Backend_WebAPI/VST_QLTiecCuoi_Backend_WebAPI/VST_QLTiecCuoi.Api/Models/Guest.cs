using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class Guest {
 public int Id {get;set;}
 public int BookingId {get;set;}
 public Booking? Booking {get;set;}
 public string Name {get;set;}="";
 public string Email {get;set;}="";
 public string Phone {get;set;}="";
 public int PartySize {get;set;}=1;
 public int? EventTableId {get;set;}
 public EventTable? EventTable {get;set;}
 public string Token {get;set;}="";
 public DateTime? CheckedInAt {get;set;}
 public string? CheckedInBy {get;set;}
}
