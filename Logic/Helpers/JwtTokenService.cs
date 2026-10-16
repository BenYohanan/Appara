using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.DbContext;
using Core.Models;
using Logic.IHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Logic.Helpers
{
	/// <summary>
	/// Issues JWTs for the mobile API. This is separate from the existing
	/// cookie/session login used by the Razor web app (AccountController) —
	/// registering this doesn't change how the website authenticates.
	/// Requires appsettings.json:
	///   "Jwt": {
	///     "Key": "<a long random secret, 32+ chars>",
	///     "Issuer": "Apparcus",
	///     "Audience": "ApparcusMobile",
	///     "ExpiryMinutes": "10080"
	///   }
	/// </summary>
	public class JwtTokenService : IJwtTokenService
	{
		private readonly IConfiguration _configuration;
		private readonly UserManager<ApplicationUser> _userManager;

		public JwtTokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager)
		{
			_configuration = configuration;
			_userManager = userManager;
		}

		public async Task<(string token, DateTime expiresAt)> GenerateTokenAsync(
			ApplicationUser user, Guid sessionId)
		{
			var jwtSection = _configuration.GetSection("Jwt");
			var key = jwtSection["Key"]
				?? throw new InvalidOperationException("Jwt:Key is not configured in appsettings.json");
			var issuer = jwtSection["Issuer"] ?? "Apparcus";
			var audience = jwtSection["Audience"] ?? "ApparcusMobile";
			var expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var m) ? m : 60 * 24 * 7; // 7 days default

			var roles = await _userManager.GetRolesAsync(user);
			var role = roles.Contains(Constants.SuperAdminRole) ? Constants.SuperAdminRole
				: roles.Contains(Constants.AdminRole) ? Constants.AdminRole
				: Constants.UserRole;

			var claims = new List<Claim>
			{
				new(ClaimTypes.NameIdentifier, user.Id),
				new(ClaimTypes.Email, user.Email ?? string.Empty),
				new(ClaimTypes.Role, role),
				new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

				// Which sign-in this token belongs to. An account has one live
				// session at a time, so this is what lets the API tell the
				// token held by the phone the user is on now from one still
				// sitting on a phone they signed out of by signing in here.
				// Checked against the UserSessions table on every request —
				// see UserSessions.ResolveActiveAsync.
				new(UserSessions.SessionClaimType, sessionId.ToString()),
			};

			var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
			var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: expiresAt,
				signingCredentials: credentials);

			return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
		}
	}

	public interface IJwtTokenService
	{
		/// <summary>
		/// Issues a signed JWT for the given user, bound to
		/// <paramref name="sessionId"/>. Returns the encoded token and its UTC
		/// expiry so the caller can surface it in LoginResponse.
		///
		/// The token is only honoured while that session is still live, which
		/// is how signing in on a new device signs the old one out.
		/// </summary>
		Task<(string token, DateTime expiresAt)> GenerateTokenAsync(
			ApplicationUser user, Guid sessionId);
	}
}
