using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class Dish {
 public int Id {get;set;}
 public string Name {get;set;}="";
 public string Category {get;set;}="";
 public string Description {get;set;}="";
 public bool Active {get;set;}=true;
}
