using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record DishRequest([Required,StringLength(100)] string Name,[Required,StringLength(50)] string Category,[StringLength(1000)] string Description,bool Active);
