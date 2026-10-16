using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.Helpers;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Apparcus.Areas.Api.Controllers
{
	[Route("api/auth")]
	[ApiController]
	public class AuthController : ControllerBase
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly IUserHelper _userHelper;
		private readonly IJwtTokenService _jwtTokenService;
		private readonly IEmailTemplateService _emailTemplateService;
		private readonly AppDbContext _context;

		public AuthController(
			UserManager<ApplicationUser> userManager,
			IUserHelper userHelper,
			IJwtTokenService jwtTokenService,
			IEmailTemplateService emailTemplateService,
			AppDbContext context)
		{
			_userManager = userManager;
			_userHelper = userHelper;
			_jwtTokenService = jwtTokenService;
			_emailTemplateService = emailTemplateService;
			_context = context;
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			var user = await _userHelper.FindByEmailAsync(request.Email);
			if (user == null)
				return ResponseHelper.JsonError("Invalid email or password");

			var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
			if (!passwordValid)
				return ResponseHelper.JsonError("Invalid email or password");

			if (await _userManager.IsLockedOutAsync(user))
				return ResponseHelper.JsonError("Account temporarily locked. Try again later.");

			// A correct password is not enough to move the account onto a
			// device it does not know, because moving it would sign the
			// current device out. Whoever is asking has to show they can read
			// the account's email first. Nothing is revoked in the meantime.
			//
			// TEMPORARY: the email-code challenge is off while the mail
			// account is being fixed, so a new device signs straight in and
			// the old one is signed out below. Uncomment to restore.
			// if (await DeviceVerifications.IsDeviceUnrecognisedAsync(
			// 		_context, user.Id, request.DeviceId))
			// {
			// 	return await ChallengeDeviceAsync(user, request.DeviceId, request.DeviceName);
			// }

			// One account, one device. Whatever was signed in before this is
			// signed out here, and finds out on its next request.
			var (token, expiresAt, displacedAnotherDevice) =
				await IssueSessionTokenAsync(user, request.DeviceId, request.DeviceName);

			return Ok(await BuildVerifiedLoginResponseAsync(
				user, token, expiresAt, displacedAnotherDevice));
		}

		/// <summary>
		/// Finishes a login that login itself refused to finish.
		///
		/// The password is checked again rather than trusted from the earlier
		/// call: the pending challenge says someone could read the email, not
		/// that they still hold the password, and a session needs both. Only
		/// once the code is spent does the account move — this is the single
		/// place a device can take over from another.
		/// </summary>
		[HttpPost("verify-device")]
		public async Task<IActionResult> VerifyDevice([FromBody] VerifyDeviceRequest request)
		{
			var user = await _userHelper.FindByEmailAsync(request.Email);
			if (user == null)
				return ResponseHelper.JsonError("Invalid email or password");

			if (!await _userManager.CheckPasswordAsync(user, request.Password))
				return ResponseHelper.JsonError("Invalid email or password");

			if (await _userManager.IsLockedOutAsync(user))
				return ResponseHelper.JsonError("Account temporarily locked. Try again later.");

			var result = await DeviceVerifications.RedeemAsync(
				_context, user.Id, request.DeviceId, request.Code);

			// Saved on every path, not just the happy one: a failed attempt
			// has to stick, or the guess limit counts nothing.
			await _context.SaveChangesAsync();

			switch (result)
			{
				case DeviceVerificationResult.Invalid:
					return ResponseHelper.JsonError("That code is not correct. Please check and try again.");

				case DeviceVerificationResult.Expired:
					return ResponseHelper.JsonError(
						"That code has expired. Please sign in again to get a new one.");

				case DeviceVerificationResult.TooManyAttempts:
					return ResponseHelper.JsonError(
						"Too many incorrect codes. Please sign in again to get a new one.");
			}

			var (token, expiresAt, displacedAnotherDevice) =
				await IssueSessionTokenAsync(user, request.DeviceId, request.DeviceName);

			return Ok(await BuildVerifiedLoginResponseAsync(
				user, token, expiresAt, displacedAnotherDevice));
		}

		/// <summary>
		/// Sends the code again for a login still waiting on one.
		///
		/// Takes the password rather than a handle to the pending challenge,
		/// so this cannot be used to post mail to an address on nothing but a
		/// guess at who banks here.
		/// </summary>
		[HttpPost("resend-device-code")]
		public async Task<IActionResult> ResendDeviceCode([FromBody] LoginRequest request)
		{
			var user = await _userHelper.FindByEmailAsync(request.Email);
			if (user == null)
				return ResponseHelper.JsonError("Invalid email or password");

			if (!await _userManager.CheckPasswordAsync(user, request.Password))
				return ResponseHelper.JsonError("Invalid email or password");

			return await ChallengeDeviceAsync(user, request.DeviceId, request.DeviceName);
		}

		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest request)
		{
			var existing = await _userHelper.FindByEmailAsync(request.Email);
			if (existing != null)
				return ResponseHelper.JsonError("Email already exists, please use another email");

			var vm = new ApplicationUserViewModel
			{
				FirstName = request.FirstName,
				LastName = request.LastName,
				Email = request.Email,
				PhoneNumber = request.PhoneNumber,
				Password = request.Password,
				DateOfBirth = request.DateOfBirth,
			};

			var user = await _userHelper.RegisterUser(vm);
			if (user == null)
				return ResponseHelper.JsonError("Unable to register. Please check your details and try again.");

			var baseUrl = $"{Request.Scheme}://{Request.Host}";
			// TEMPORARY: email sending disabled while the mail account is being fixed.
			// _emailTemplateService.SendRegistrationEmail(user, baseUrl);

			var (token, expiresAt, _) =
				await IssueSessionTokenAsync(user, request.DeviceId, request.DeviceName);

			return Ok(BuildLoginResponse(user, token, expiresAt, Constants.UserRole));
		}

		[HttpPost("forgot-password")]
		public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
		{
			var user = await _userManager.FindByEmailAsync(request.Email);

			if (user != null)
			{
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				var baseUrl = $"{Request.Scheme}://{Request.Host}";
				var resetUrl = $"{baseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(request.Email)}";
				// TEMPORARY: email sending disabled while the mail account is being fixed.
				// _emailTemplateService.SendPasswordResetEmail(user, resetUrl);
			}

			return ResponseHelper.JsonSuccess(
				"If an account with that email exists, a password reset link has been sent.");
		}

		[HttpPost("reset-password")]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request)
		{
			var user = await _userManager.FindByEmailAsync(request.Email);
			if (user == null)
				return ResponseHelper.JsonError("Invalid request.");

			var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
			if (!result.Succeeded)
				return ResponseHelper.JsonError(string.Join("; ", result.Errors.Select(e => e.Description)));

			// A reset is what someone does when they have lost control of the
			// account. Any device still signed in was let in on the old
			// password, so none of them survive it — including, deliberately,
			// whatever device is doing the reset: the user proves themselves
			// again with the password they just chose.
			await UserSessions.RevokeAllForUserAsync(
				_context, user.Id, UserSessions.ReasonPasswordReset);
			await BiometricDeviceKeys.RevokeAllForUserAsync(
				_context, user.Id, UserSessions.ReasonPasswordReset);

			// Including any sign-in code an attacker triggered on their way to
			// being locked out. The password it was raised against is gone.
			await DeviceVerifications.SupersedeAllAsync(_context, user.Id);

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("Password reset successful!");
		}

		/// <summary>
		/// Ends the session this token belongs to.
		///
		/// Not strictly required — the app can simply drop the token — but
		/// without it a token discarded on a handed-over or sold phone stays
		/// valid until it expires, and the account keeps a live session
		/// against it. Calling this is what makes the seat free immediately.
		/// </summary>
		[HttpPost("logout")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public async Task<IActionResult> Logout()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrWhiteSpace(userId))
				return ResponseHelper.JsonSuccess("Signed out");

			await UserSessions.RevokeAllForUserAsync(
				_context, userId, UserSessions.ReasonLogout);
			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("Signed out");
		}

		/// <summary>
		/// Raises the challenge and answers with the "check your email" shape.
		///
		/// Returns 200, not an error: nothing has gone wrong, the login is
		/// simply not finished. The response deliberately looks the same
		/// whether a code was actually posted or the cooldown swallowed it,
		/// so tapping resend cannot be used to time the mail queue.
		/// </summary>
		private async Task<IActionResult> ChallengeDeviceAsync(
			ApplicationUser user, string? deviceId, string? deviceName)
		{
			var issued = await DeviceVerifications.IssueAsync(
				_context,
				user.Id,
				deviceId,
				deviceName,
				HttpContext.Connection.RemoteIpAddress?.ToString());

			if (issued.HasValue)
			{
				await _context.SaveChangesAsync();

				// TEMPORARY: email sending disabled while the mail account is being fixed.
				// _emailTemplateService.SendDeviceVerificationEmail(
				// 	user,
				// 	issued.Value.code,
				// 	deviceName,
				// 	HttpContext.Connection.RemoteIpAddress?.ToString());
			}

			return Ok(new DeviceVerificationRequiredResponse
			{
				MaskedEmail = DeviceVerifications.MaskEmail(user.Email),
				Msg = "For your security, we sent a code to your email. "
					+ "Enter it to finish signing in on this device.",
				ResendAfterSeconds = (int)DeviceVerifications.ResendCooldown.TotalSeconds,
				CodeLength = 6,
			});
		}

		/// <summary>
		/// The response for a login that actually opened a session, with the
		/// caller's role resolved.
		/// </summary>
		private async Task<LoginResponse> BuildVerifiedLoginResponseAsync(
			ApplicationUser user, string token, DateTime expiresAt, bool displacedAnotherDevice)
		{
			var roles = await _userManager.GetRolesAsync(user);
			var role = roles.Contains(Constants.SuperAdminRole) ? Constants.SuperAdminRole
				: roles.Contains(Constants.AdminRole) ? Constants.AdminRole
				: Constants.UserRole;

			var response = BuildLoginResponse(user, token, expiresAt, role);
			response.SignedOutOtherDevice = displacedAnotherDevice;

			return response;
		}

		/// <summary>
		/// Opens the one session this account is allowed and mints a token
		/// bound to it, retiring whatever session was live before.
		///
		/// The session row is written before the token is handed out, so there
		/// is no window where a client holds a token the API would not
		/// recognise.
		/// </summary>
		private async Task<(string token, DateTime expiresAt, bool displacedAnotherDevice)>
			IssueSessionTokenAsync(ApplicationUser user, string? deviceId, string? deviceName)
		{
			var (session, displacedAnotherDevice) = await UserSessions.StartAsync(
				_context,
				user.Id,
				deviceId,
				deviceName,
				HttpContext.Connection.RemoteIpAddress?.ToString());

			// A session is now open, so any code still sitting in the user's
			// inbox has nothing left to authorise and must not be spendable
			// later.
			await DeviceVerifications.SupersedeAllAsync(_context, user.Id);

			// The token carries the session id, and the session carries the
			// token's expiry, so neither can be built without the other. The
			// id is settled at construction and the row is still only tracked
			// here, so both are filled in before the single save below — no
			// window where a client holds a token the API cannot resolve.
			var (token, expiresAt) = await _jwtTokenService.GenerateTokenAsync(user, session.Id);
			session.ExpiresAt = expiresAt;

			await _context.SaveChangesAsync();

			return (token, expiresAt, displacedAnotherDevice);
		}

		/// <summary>
		/// The whole profile, not just an identity — the app renders balances and
		/// the personal account straight off this, so a second round-trip before
		/// first paint would be wasted.
		/// </summary>
		private static LoginResponse BuildLoginResponse(
			ApplicationUser user,
			string token,
			DateTime expiresAt,
			string role) => new()
		{
			Token = token,
			ExpiresAt = expiresAt,
			UserId = user.Id,
			FullName = $"{user.FirstName} {user.LastName}".Trim(),
			Email = user.Email ?? string.Empty,
			PhoneNumber = user.PhoneNumber,
			ProfilePictureUrl = user.ProfilePictureUrl,
			Role = role,
			KycVerified = user.KycVerified,
			TransactionPinSet = user.TransactionPinSet,
			BeadsBalance = user.BeadsBalance,
			PersonalBalance = user.PersonalBalance,
			PersonalAccountNumber = user.PersonalAccountNumber,
			PersonalHoldersName = user.PersonalHoldersName,
		};
	}
}
