using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record PasswordRequest([Required] string CurrentPassword,[Required,MinLength(8),MaxLength(100)] string NewPassword);
