using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class Booking {
 public int Id {get;set;}
 public string UserId {get;set;}="";
 public User? User {get;set;}
 public int HallId {get;set;}
 public Hall? Hall {get;set;}
 public int MenuId {get;set;}
 public Menu? Menu {get;set;}
 public string BrideName {get;set;}="";
 public string GroomName {get;set;}="";
 public string Phone {get;set;}="";
 public string EventDate {get;set;}="";
 public string Slot {get;set;}="Lunch";
 public int TableCount {get;set;}
 public decimal PricePerTable {get;set;}
 public string MenuSnapshot {get;set;}="";
 public string Status {get;set;}="Pending";
 public string Note {get;set;}="";
 public string DecisionNote {get;set;}="";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
// Unique primary key is the final arbiter when simultaneous approvals race.
