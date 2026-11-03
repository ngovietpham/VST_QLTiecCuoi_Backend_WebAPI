using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record ResetConfirmRequest([Required] string ChallengeId,[Required,RegularExpression(@"^\d{6}$")] string Code,[Required,MinLength(8),MaxLength(100)] string Password);
