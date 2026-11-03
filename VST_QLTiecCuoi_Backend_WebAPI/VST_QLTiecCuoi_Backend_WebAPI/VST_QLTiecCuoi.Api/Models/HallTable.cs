using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class HallTable {
 public int Id {get;set;}
 public int HallId {get;set;}
 public Hall? Hall {get;set;}
 public int Number {get;set;}
 public int Capacity {get;set;}=10;
}
