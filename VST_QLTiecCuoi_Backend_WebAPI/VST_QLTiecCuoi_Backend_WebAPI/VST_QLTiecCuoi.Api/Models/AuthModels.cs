using System.ComponentModel.DataAnnotations;
namespace VST_QLTiecCuoi.Models;
public class EmailChallenge {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string UserId {get;set;}="";
 public string Purpose {get;set;}="";
 public string CodeHash {get;set;}="";
 public DateTime CreatedUtc {get;set;}
 public DateTime ExpiresUtc {get;set;}
 public int Attempts {get;set;}
 public bool Consumed {get;set;}
}
public record RegistrationModel([Required,StringLength(50,MinimumLength=3),RegularExpression(@"^[a-zA-Z0-9_.-]+$")] string Username,[Required,EmailAddress,StringLength(200)] string Email,[Required,MinLength(8),MaxLength(100)] string Password,[Required,StringLength(100,MinimumLength=2)] string FullName);
public record LoginModel([Required] string Username,[Required] string Password);
