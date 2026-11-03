using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VST_QLTiecCuoi.Models;
namespace VST_QLTiecCuoi.Services;
public class EmailOtpService(ApplicationDbContext db,IConfiguration config,IWebHostEnvironment env) {
 public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
 public async Task DeliverAsync(string email,string subject,string body,byte[]? attachment=null) {
  var smtp=config.GetSection("Smtp");
  if(smtp["Mode"]=="File") {
   if(!env.IsDevelopment()) throw new InvalidOperationException("Chế độ thư mẫu chỉ dùng trong Development. Cần cấu hình SMTP.");
   var path=Path.GetFullPath(smtp["Outbox"]??"Data/outbox"); Directory.CreateDirectory(path);
   var prefix=Path.Combine(path,$"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
   await File.WriteAllTextAsync(prefix+".txt",$"To: {email}\nSubject: {subject}\n\n{body}");
   if(attachment!=null)await File.WriteAllBytesAsync(prefix+".png",attachment);
   return;
  }
  if(smtp["Mode"]!="Smtp")throw new InvalidOperationException("Chế độ gửi email không hợp lệ. Chọn File hoặc Smtp.");
  if(string.IsNullOrWhiteSpace(smtp["Host"])||string.IsNullOrWhiteSpace(smtp["From"])||string.IsNullOrWhiteSpace(smtp["Username"])||string.IsNullOrWhiteSpace(smtp["Password"]))
   throw new InvalidOperationException("Máy chủ chưa cấu hình đủ Gmail SMTP.");
  try {
   // Explicit development-only delivery for the seeded @example.com accounts.
   // Real registered email addresses always remain their own recipients.
   if(env.IsDevelopment()&&email.EndsWith("@example.com",StringComparison.OrdinalIgnoreCase)) {
    var recipient=smtp["DemoRecipient"];
    if(!string.IsNullOrWhiteSpace(recipient))email=new MailAddress(recipient).Address;
   }
   using var client=new SmtpClient(smtp["Host"],smtp.GetValue("Port",587)) { EnableSsl=true, Credentials=new NetworkCredential(smtp["Username"],smtp["Password"]),Timeout=15000 };
   using var message=new MailMessage(new MailAddress(smtp["From"]!,smtp["FromName"]??"VST"),new MailAddress(email)) {Subject=subject,Body=body,BodyEncoding=Encoding.UTF8,SubjectEncoding=Encoding.UTF8};
   if(attachment!=null)message.Attachments.Add(new Attachment(new MemoryStream(attachment),"VST-ve-moi.png","image/png"));
   using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
   await client.SendMailAsync(message,timeout.Token);
  } catch(OperationCanceledException) { throw new InvalidOperationException("Gửi email quá thời gian chờ. Kiểm tra Internet rồi gửi lại mã."); }
  catch(SmtpException) { throw new InvalidOperationException("Không gửi được email. Kiểm tra tài khoản gửi và mật khẩu ứng dụng Gmail."); }
 }
 public async Task<string> SendAsync(User user,string purpose) {
  var now=DateTime.UtcNow;
  if(await db.Set<EmailChallenge>().AnyAsync(c=>c.UserId==user.Id&&c.CreatedUtc>now.AddSeconds(-60)))
   throw new InvalidOperationException("Vui lòng chờ 60 giây trước khi gửi lại OTP.");
  var c=new EmailChallenge{UserId=user.Id,Purpose=purpose,CreatedUtc=now,ExpiresUtc=now.AddMinutes(5)};
  var code=RandomNumberGenerator.GetInt32(0,1000000).ToString("D6");
  c.CodeHash=Hash(c.Id+":"+code); db.Add(c); await db.SaveChangesAsync();
  try { await DeliverAsync(user.Email!,"VST - Mã xác minh email",$"Mã OTP: {code}\nMã có hiệu lực trong 5 phút, tối đa 5 lần thử. Không chia sẻ mã này.\nYêu cầu: {purpose}\nMã yêu cầu: {c.Id}"); }
  catch { db.Remove(c); await db.SaveChangesAsync(); throw; }
  await db.Set<EmailChallenge>().Where(x=>x.UserId==user.Id&&x.Purpose==purpose&&x.Id!=c.Id&&!x.Consumed).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Consumed,true));
  return c.Id;
 }
 // Increment attempts atomically before accepting a one-time challenge.
 public async Task<EmailChallenge?> ConsumeAsync(string id,string code,string purpose) {
  var now=DateTime.UtcNow;
  var eligible=db.Set<EmailChallenge>().Where(c=>c.Id==id&&c.Purpose==purpose&&!c.Consumed&&c.ExpiresUtc>now&&c.Attempts<5);
  if(await eligible.ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Attempts,c=>c.Attempts+1))!=1)return null;
  var hash=Hash(id+":"+code);
  if(await db.Set<EmailChallenge>().Where(c=>c.Id==id&&c.Purpose==purpose&&!c.Consumed&&c.ExpiresUtc>now&&c.Attempts<=5&&c.CodeHash==hash).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Consumed,true))!=1)return null;
  return await db.Set<EmailChallenge>().AsNoTracking().SingleAsync(c=>c.Id==id);
 }
}
