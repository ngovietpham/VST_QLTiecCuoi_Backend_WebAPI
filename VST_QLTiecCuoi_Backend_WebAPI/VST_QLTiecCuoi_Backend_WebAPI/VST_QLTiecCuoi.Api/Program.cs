using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VST_QLTiecCuoi.Models;
using VST_QLTiecCuoi.Repositories;
using VST_QLTiecCuoi.Services;
var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json",optional:true,reloadOnChange:false).AddEnvironmentVariables();
Directory.CreateDirectory("Data");
var secret=builder.Configuration["JWTKey:Secret"];
if(string.IsNullOrWhiteSpace(secret)&&builder.Environment.IsDevelopment()) {
 var path="Data/jwt.key";
 if(!File.Exists(path))await File.WriteAllTextAsync(path,Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
 secret=await File.ReadAllTextAsync(path);builder.Configuration["JWTKey:Secret"]=secret;
}
if(string.IsNullOrWhiteSpace(secret)||Encoding.UTF8.GetByteCount(secret)<32)throw new InvalidOperationException("Cần JWTKey__Secret ít nhất 32 byte.");
if(!builder.Environment.IsDevelopment()) {
 if(builder.Configuration["DatabaseProvider"]!="SqlServer")throw new InvalidOperationException("Bản triển khai cần cấu hình SQL Server làm CSDL chính.");
 if(builder.Configuration["Smtp:Mode"]!="Smtp" || new[]{"Host","From","Username","Password"}.Any(k=>string.IsNullOrWhiteSpace(builder.Configuration[$"Smtp:{k}"])))
  throw new InvalidOperationException("Cần cấu hình SMTP thật trước khi chạy bản triển khai.");
}
builder.Services.AddDbContext<ApplicationDbContext>(o=> {
 var connection=builder.Configuration.GetConnectionString("Default")!;
 if(builder.Configuration["DatabaseProvider"]=="SqlServer")o.UseSqlServer(connection);else o.UseSqlite(connection);
});
builder.Services.AddIdentityCore<User>(o=> {
 o.User.RequireUniqueEmail=true;o.Password.RequiredLength=8;o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(5);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("Data/keys"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=> {
 o.TokenValidationParameters=new(){ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,ValidateIssuerSigningKey=true,ValidIssuer=builder.Configuration["JWTKey:ValidIssuer"],ValidAudience=builder.Configuration["JWTKey:ValidAudience"],IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.FromSeconds(5)};
 o.Events=new JwtBearerEvents{OnTokenValidated=async c=> {
  var users=c.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();var id=c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);var u=id==null?null:await users.FindByIdAsync(id);
  if(u==null||u.Disabled||await users.IsLockedOutAsync(u)||u.SessionVersion.ToString()!=c.Principal!.FindFirstValue("version")){c.Fail("Phiên hết hiệu lực.");return;}
  var current=(await users.GetRolesAsync(u)).Order().ToArray();var claims=c.Principal!.FindAll(ClaimTypes.Role).Select(x=>x.Value).Order().ToArray();if(!current.SequenceEqual(claims))c.Fail("Quyền đã thay đổi.");
 }};
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<WeddingAiService>();
builder.Services.AddRateLimiter(o => o.AddPolicy("wedding-ai", c => RateLimitPartition.GetFixedWindowLimiter(
 c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
 _ => new FixedWindowRateLimiterOptions { PermitLimit=6, Window=TimeSpan.FromMinutes(1), QueueLimit=0 })));
builder.Services.AddControllers().AddJsonOptions(o=>o.JsonSerializerOptions.ReferenceHandler=System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddScoped<IWeddingRepository,WeddingRepository>();builder.Services.AddScoped<WeddingService>();builder.Services.AddScoped<EmailOtpService>();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("AuthRateLimit",30),Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddCors(o=>o.AddDefaultPolicy(p=> {
 var allowed=builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()??[];
 p.SetIsOriginAllowed(origin=>allowed.Contains(origin,StringComparer.OrdinalIgnoreCase)||
  (builder.Environment.IsDevelopment()&&Uri.TryCreate(origin,UriKind.Absolute,out var uri)&&
   (uri.Scheme=="http"||uri.Scheme=="https")&&
   (uri.Host=="localhost"||uri.Host=="127.0.0.1"||uri.Host=="[::1]")))
  .AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen(o=> {
 o.SwaggerDoc("v1",new(){Title="VST_QLTiecCuoi API",Version="v1"});
 o.AddSecurityDefinition("Bearer",new(){Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT"});
 o.AddSecurityRequirement(new(){[new OpenApiSecurityScheme{Reference=new(){Type=ReferenceType.SecurityScheme,Id="Bearer"}}]=Array.Empty<string>()});
});
var app=builder.Build();
app.Use(async(c,next)=> {
 try{await next();}
 catch(BusinessException ex){c.Response.StatusCode=ex.Status;await c.Response.WriteAsJsonAsync(new{message=ex.Message});}
 catch(DbUpdateException ex){app.Logger.LogWarning(ex,"Database update rejected");c.Response.StatusCode=409;await c.Response.WriteAsJsonAsync(new{message="Dữ liệu đang được sử dụng hoặc vừa thay đổi. Hãy tải lại và thử lại."});}
 catch(Exception ex){app.Logger.LogError(ex,"Request failed");c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{message="Máy chủ gặp lỗi. Vui lòng thử lại hoặc liên hệ quản trị."});}
});
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}else{app.UseHsts();app.UseHttpsRedirection();}
var hasWebClient=File.Exists(Path.Combine(app.Environment.ContentRootPath,"wwwroot","index.html"));
if(hasWebClient){app.UseDefaultFiles();app.UseStaticFiles();}
app.UseRouting();app.UseCors();app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();
app.MapGet("/health",()=>Results.Ok(new{status="ok",application="VST_QLTiecCuoi"}));
app.MapGet("/health/ready",async(ApplicationDbContext db)=> {
 try {return await db.Database.CanConnectAsync()?Results.Ok(new{status="ready"}):Results.StatusCode(503);}
 catch {return Results.StatusCode(503);}
});
using(var scope=app.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
 // Schema is created from the current EF model, in a dedicated new database.
 if(args.Contains("--export-schema")){await File.WriteAllTextAsync("Data/schema.sql",db.Database.GenerateCreateScript());return;}
 await db.Database.EnsureCreatedAsync();
 await VenueSchema.EnsureAsync(db);
 await AlbumSchema.EnsureAsync(db);
 var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
 foreach(var role in new[]{"Admin","Manager","User"})if(!await roles.RoleExistsAsync(role))await roles.CreateAsync(new(role));
 await LegacyRoles.MergeStaffAsync(db,scope.ServiceProvider.GetRequiredService<UserManager<User>>(),roles);
 if(args.Contains("--bootstrap-admin")) {
  await ProductionAdmin.CreateAsync(scope.ServiceProvider.GetRequiredService<UserManager<User>>(),db,app.Configuration);
  return;
 }
 if(app.Environment.IsDevelopment()&&app.Configuration.GetValue<bool>("Demo:Seed"))await DemoSeeder.SeedAsync(db,scope.ServiceProvider.GetRequiredService<UserManager<User>>());
}
app.Map("/api/{**path}",()=>Results.NotFound(new{message="API không tồn tại."}));
if(hasWebClient)app.MapFallbackToFile("index.html");
else app.MapGet("/",()=>Results.Redirect(app.Environment.IsDevelopment()?"/swagger":"/health"));
app.Run();
