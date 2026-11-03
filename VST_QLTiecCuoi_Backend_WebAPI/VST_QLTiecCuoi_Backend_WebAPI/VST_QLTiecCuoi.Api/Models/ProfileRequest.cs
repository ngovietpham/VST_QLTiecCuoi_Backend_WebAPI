using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record ProfileRequest([Required,StringLength(100,MinimumLength=2)] string FullName,[RegularExpression(@"^\+?[0-9]{9,15}$")] string? Phone);
