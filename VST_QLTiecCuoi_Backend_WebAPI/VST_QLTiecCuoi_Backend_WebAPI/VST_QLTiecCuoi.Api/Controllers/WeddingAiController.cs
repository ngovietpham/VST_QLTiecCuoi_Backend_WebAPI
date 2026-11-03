using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Services;
namespace VST_QLTiecCuoi.Controllers;

[ApiController, Route("api/ai"), Authorize(Roles="User")]
public class WeddingAiController(WeddingAiService ai, ApplicationDbContext db, UserManager<User> users):ControllerBase {
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled=ai.Available, provider="Gemini" });

    [HttpPost("chat"), EnableRateLimiting("wedding-ai"), RequestSizeLimit(20000)]
    public async Task<IActionResult> Chat(AiQuestion question, CancellationToken ct) {
        string? context=null;
        if (question.InvitationId != null) {
            var user=await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (user==null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email)) return Forbid();
            var email=user.Email.Trim().ToUpperInvariant();
            // Never accept context, booking IDs, names, QR tokens or permissions from the client.
            var own=await db.Guests.AsNoTracking().Where(g=>g.Id==question.InvitationId && g.Email.Trim().ToUpper()==email)
                .Select(g=>new { groupSize=g.PartySize, tableNumber=g.EventTable==null?(int?)null:g.EventTable.Number,
                    tableCapacity=g.EventTable==null?(int?)null:g.EventTable.Capacity,
                    menu=g.Booking!.MenuSnapshot, status=g.Booking.Status }).SingleOrDefaultAsync(ct);
            if (own==null) return NotFound(new { message="Lời mời không tồn tại hoặc không thuộc tài khoản của bạn." });
            context=JsonSerializer.Serialize(new { own.groupSize, own.tableNumber, own.tableCapacity,
                menu=own.menu.Length>4000?own.menu[..4000]:own.menu, own.status });
        }
        return Ok(new { reply=await ai.AskAsync(question,context,ct), provider="Gemini" });
    }
}
