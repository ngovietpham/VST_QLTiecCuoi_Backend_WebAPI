using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record MenuRequest([Required,StringLength(100)] string Name,[StringLength(1000)] string Description,[Range(typeof(decimal),"1","1000000000")] decimal PricePerTable,bool Active,[Required,MinLength(1),MaxLength(30)] int[] DishIds);
