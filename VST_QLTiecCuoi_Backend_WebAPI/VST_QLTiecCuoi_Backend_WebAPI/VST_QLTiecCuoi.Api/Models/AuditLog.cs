using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class AuditLog {
 public int Id {get;set;}
 public string ActorId {get;set;}="";
 public string Action {get;set;}="";
 public string Detail {get;set;}="";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
