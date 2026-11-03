using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record GuestRequest([Required,StringLength(100)] string Name,[Required,EmailAddress,StringLength(256)] string Email,[RegularExpression(@"^\+?[0-9]{9,15}$")] string? Phone,[Range(1,30)] int PartySize,int? EventTableId);
