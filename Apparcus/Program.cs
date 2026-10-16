using Core.DbContext;
using Core.Models;
using Hangfire;
using Hangfire.SqlServer;
using Logic.Helpers;
using Logic.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.RegisterHelpers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DBConnectionString")));

builder.Services.AddHangfire(config =>
{
	config.UseSqlServerStorage(
		builder.Configuration.GetConnectionString("DBConnectionString"),
		new SqlServerStorageOptions
		{
			SchemaName = "hangfire",
			JobExpirationCheckInterval = TimeSpan.FromHours(1)
		}
	);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 3;
    options.Password.RequiredUniqueChars = 0;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
}).AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services
	   .AddAuthentication() // no DefaultScheme override — Identity's cookie stays default
	   .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
	   {
		   var jwt = builder.Configuration.GetSection("Jwt");
		   options.TokenValidationParameters = new TokenValidationParameters
		   {
			   ValidateIssuer = true,
			   ValidateAudience = true,
			   ValidateLifetime = true,
			   ValidateIssuerSigningKey = true,
			   ValidIssuer = jwt["Issuer"],
			   ValidAudience = jwt["Audience"],
			   IssuerSigningKey = new SymmetricSecurityKey(
				   Encoding.UTF8.GetBytes(jwt["Key"]!)),
		   };

		   options.Events = new JwtBearerEvents
		   {
			   // A valid signature is not enough: an account may only have one
			   // device signed in, and the signature alone cannot express that
			   // — a JWT is self-contained and cannot be recalled once issued.
			   // So every request also resolves the session the token was
			   // minted for. Signing in elsewhere revokes that row, and the
			   // old device stops being authenticated on its very next call
			   // rather than whenever its token happens to expire.
			   OnTokenValidated = async context =>
			   {
				   var db = context.HttpContext.RequestServices
					   .GetRequiredService<AppDbContext>();

				   var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
				   var sessionId = context.Principal?.FindFirstValue(UserSessions.SessionClaimType);

				   var check = await UserSessions.CheckAsync(db, userId, sessionId);

				   if (check != SessionCheck.Active)
				   {
					   context.Fail(check == SessionCheck.SignedInElsewhere
						   ? UserSessions.RevokedResponseCode
						   : UserSessions.EndedResponseCode);
				   }
			   },

			   // Without this the client gets a bare 401 with an empty body and
			   // cannot tell "your session was taken over" from "your token
			   // expired" — one needs an explanation, the other just a login
			   // screen. The shape matches ResponseHelper.JsonError so the app
			   // parses it like every other error.
			   OnChallenge = async context =>
			   {
				   var code = context.AuthenticateFailure?.Message;

				   var ours = code == UserSessions.RevokedResponseCode
					   || code == UserSessions.EndedResponseCode;

				   if (!ours || context.Response.HasStarted)
					   return;

				   context.HandleResponse();

				   context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				   context.Response.ContentType = "application/json";

				   await context.Response.WriteAsJsonAsync(new
				   {
					   isError = true,
					   code,
					   msg = code == UserSessions.RevokedResponseCode
						   ? "You have been signed out because your account was "
							   + "signed in on another device."
						   : "Your session has ended. Please sign in again.",
				   });
			   },
		   };
	   });

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Backs the failed-access-code limiter in GuestController.
builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(70000);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    try
    {
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        dbContext.Database.Migrate();

        CoreSeed.SeedData(dbContext, roleManager, userManager);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error occurred seeding the database: {ex.Message}");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
// Once, and after UseRouting: authentication was registered twice, and the
// first registration ran before routing had picked an endpoint, so any
// per-endpoint scheme it should have honoured was not yet known to it.
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireConfiguration();

app.MapHub<PaymentHub>("/paymentHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
