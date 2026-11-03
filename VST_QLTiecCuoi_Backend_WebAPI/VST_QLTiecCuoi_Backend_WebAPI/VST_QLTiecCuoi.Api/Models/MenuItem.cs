using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public class MenuItem {
 public int MenuId {get;set;}
 public Menu? Menu {get;set;}
 public int DishId {get;set;}
 public Dish? Dish {get;set;}
 public int Position {get;set;}
}
