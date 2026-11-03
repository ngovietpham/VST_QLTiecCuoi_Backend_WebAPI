using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record BookingRequest([Required,StringLength(100)] string BrideName,[Required,StringLength(100)] string GroomName,[Required,RegularExpression(@"^\+?[0-9]{9,15}$")] string Phone,int HallId,int MenuId,[Required] string EventDate,[Required] string Slot,[Range(1,500)] int TableCount,[StringLength(2000)] string Note);
