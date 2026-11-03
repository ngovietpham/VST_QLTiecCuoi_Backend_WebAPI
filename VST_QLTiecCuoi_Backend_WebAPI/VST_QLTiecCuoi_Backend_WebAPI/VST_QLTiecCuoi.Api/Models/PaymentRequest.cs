using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace VST_QLTiecCuoi.Models;
public record PaymentRequest([Range(typeof(decimal),"1","500000000000")] decimal Amount,[Required,RegularExpression("^(Cash|BankTransfer)$")] string Method,[Required,StringLength(100)] string Reference);
