using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class User : IdentityUser {
 public string FullName {get;set;}="";
 public int SessionVersion {get;set;}
 public bool Disabled {get;set;}
}
