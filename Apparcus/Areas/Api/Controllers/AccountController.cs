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
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apparcus.Controllers.Api
{
	[Route("api/account")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	public class AccountApiController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly ISafeHavenHelper _safeHavenHelper;
		private readonly IWebHostEnvironment _env;

		private const long MaxAvatarBytes = 5 * 1024 * 1024;
		private static readonly string[] AllowedAvatarExtensions =
			[".jpg", ".jpeg", ".png", ".webp"];

		public AccountApiController(
			AppDbContext context,
			UserManager<ApplicationUser> userManager,
			ISafeHavenHelper safeHavenHelper,
			IWebHostEnvironment env)
		{
			_context = context;
			_userManager = userManager;
			_safeHavenHelper = safeHavenHelper;
			_env = env;
		}

		private string CurrentUserId =>
			User.FindFirstValue(ClaimTypes.NameIdentifier)
			?? throw new UnauthorizedAccessException();

		/// <summary>
		/// The session this request's token was minted for. Null only if the
		/// claim is missing or malformed, and a null here revokes everything
		/// including this device — the safe direction to fail.
		/// </summary>
		private Guid? CurrentSessionId =>
			Guid.TryParse(User.FindFirstValue(UserSessions.SessionClaimType), out var id)
				? id
				: null;

		[HttpGet("profile")]
		public IActionResult GetProfile()
		{
			var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			return Ok(new ProfileViewModel
			{
				Id = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				Email = user.Email ?? string.Empty,
				PhoneNumber = user.PhoneNumber,
				ProfilePictureUrl = user.ProfilePictureUrl,
				KycVerified = user.KycVerified,
				TransactionPinSet = user.TransactionPinSet,
				BeadsBalance = user.BeadsBalance,
				PersonalBalance = user.PersonalBalance,
				PersonalAccountNumber = user.PersonalAccountNumber,
				PersonalHoldersName = user.PersonalHoldersName,
			});
		}

		[HttpPut("profile")]
		public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			user.FirstName = request.FirstName;
			user.LastName = request.LastName;
			if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
				user.PhoneNumber = request.PhoneNumber;

			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("Profile updated");
		}

		/// <summary>
		/// Closes the account for good, from inside the app.
		///
		/// Required by the App Store of anything that lets people sign up, and
		/// the right thing regardless: an account someone can open but only
		/// leave by emailing support is not really theirs.
		///
		/// Money is the one thing that stops it. A balance cannot be quietly
		/// erased along with the account that owns it, so a user holding one is
		/// told to withdraw first rather than losing it — their money, their
		/// decision, and not one to take on their behalf.
		///
		/// What is kept is the ledger. Contributions, payouts and withdrawals
		/// are other people's records as much as this user's, and a financial
		/// platform has to be able to account for them afterwards. So the
		/// person is erased from the account — name, email, phone, date of
		/// birth, picture, PIN, identity and bank details all go, and the
		/// password with them — while the rows those transactions hang from
		/// stay, pointing at someone who is no longer anybody.
		/// </summary>
		[HttpPost("delete")]
		public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
		{
			var user = await _userManager.FindByIdAsync(CurrentUserId);
			if (user == null) return NotFound();

			// Asked for again here rather than trusted from the session: this is
			// the one action that cannot be undone, and a phone left unlocked on
			// a table should not be enough to end somebody's account.
			if (string.IsNullOrWhiteSpace(request?.Password)
				|| !await _userManager.CheckPasswordAsync(user, request.Password))
				return ResponseHelper.JsonError("That password is not correct.");

			if (user.PersonalBalance > 0)
				return ResponseHelper.JsonError(
					$"Your Personal Account still holds ₦{user.PersonalBalance:N2}. "
					+ "Withdraw it first, then delete your account.");

			var projectsHolding = await _context.Wallets
				.Where(w => w.ProjectOwnerId == user.Id && w.Balance > 0)
				.SumAsync(w => (decimal?)w.Balance) ?? 0m;

			if (projectsHolding > 0)
				return ResponseHelper.JsonError(
					$"Your projects still hold ₦{projectsHolding:N2} between them. "
					+ "Withdraw it first, then delete your account.");

			// Closed rather than removed: a contributor's own record of what
			// they paid into them has to survive the organiser leaving.
			var projects = await _context.Projects
				.Where(p => p.CreatedById == user.Id && !p.Deleted)
				.ToListAsync();

			foreach (var project in projects)
			{
				project.Status = ProjectStatus.Closed;
				project.IsPublic = false;
				project.AccessCode = null;
			}

			// Unique, because Identity indexes both and a second deletion would
			// otherwise collide with the first.
			var tombstone = $"deleted-{Guid.NewGuid():N}";

			user.Deleted = true;
			user.FirstName = "Deleted";
			user.LastName = "account";
			user.Email = $"{tombstone}@deleted.apparcus.com";
			user.NormalizedEmail = user.Email.ToUpperInvariant();
			user.UserName = tombstone;
			user.NormalizedUserName = tombstone.ToUpperInvariant();
			user.PhoneNumber = null;
			user.DateOfBirth = null;
			user.ProfilePictureUrl = null;
			user.TransactionPin = null;
			user.TransactionPinSet = false;
			user.SafeHavenIdentityId = null;
			user.SafeHavenIdentityType = null;
			user.KycVerified = false;
			user.KycVerifiedAt = null;
			user.AccountNumber = null;
			user.AccountBank = null;
			user.HoldersName = null;
			user.PersonalAccountNumber = null;
			user.PersonalAccountBank = null;
			user.PersonalHoldersName = null;

			// No password at all, rather than a scrambled one: nothing can be
			// guessed into, and a reset link has nothing to reset.
			user.PasswordHash = null;
			user.SecurityStamp = Guid.NewGuid().ToString();

			await UserSessions.RevokeAllForUserAsync(
				_context, user.Id, "account_deleted");
			await BiometricDeviceKeys.RevokeAllForUserAsync(
				_context, user.Id, "account_deleted");
			await DeviceVerifications.SupersedeAllAsync(_context, user.Id);

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("Your account has been deleted.");
		}

		[HttpPost("change-password")]
		public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
		{
			var user = await _userManager.FindByIdAsync(CurrentUserId);
			if (user == null) return NotFound();

			var result = await _userManager.ChangePasswordAsync(
				user, request.CurrentPassword, request.NewPassword);

			if (!result.Succeeded)
				return ResponseHelper.JsonError(string.Join("; ", result.Errors.Select(e => e.Description)));

			// Changing the password is what someone does when they think their
			// account is compromised. Any phone still holding a device key was
			// trusted on the strength of the old password, so that trust goes
			// with it.
			await BiometricDeviceKeys.RevokeAllForUserAsync(
				_context, CurrentUserId, "password_changed");

			// Same reasoning for signed-in devices. Only one session should
			// exist anyway, but a change of password is exactly the moment to
			// make sure of it — and this device keeps its own, so the user
			// isn't thrown out of the screen they just used.
			await UserSessions.RevokeAllForUserAsync(
				_context, CurrentUserId, UserSessions.ReasonPasswordChanged, CurrentSessionId);

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("Password changed");
		}

		[HttpPost("transaction-pin")]
		public async Task<IActionResult> SetTransactionPin([FromBody] SetTransactionPinRequest request)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			if (user.TransactionPinSet)
			{
				if (string.IsNullOrWhiteSpace(request.CurrentPin))
					return ResponseHelper.JsonError("Current PIN is required to change your PIN");

				var currentValid = BCrypt.Net.BCrypt.Verify(request.CurrentPin, user.TransactionPin);
				if (!currentValid)
					return ResponseHelper.JsonError("Current PIN is incorrect");
			}

			if (request.NewPin.Length != 6 || !request.NewPin.All(char.IsDigit))
				return ResponseHelper.JsonError("PIN must be exactly 6 digits");

			var replacingExistingPin = user.TransactionPinSet;

			user.TransactionPin = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
			user.TransactionPinSet = true;

			// A device key stands in for the old PIN, so it must not outlive
			// it. Skipped when setting a first PIN, where there is nothing to
			// invalidate and no enrolment can exist yet.
			if (replacingExistingPin)
			{
				await BiometricDeviceKeys.RevokeAllForUserAsync(
					_context, CurrentUserId, "pin_changed");
			}

			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("Transaction PIN saved");
		}

		// -----------------------------------------------------------
		// Biometric withdrawal — trading the password + PIN once for a
		// device key the phone keeps behind its fingerprint sensor.
		// -----------------------------------------------------------

		[HttpGet("biometric/status")]
		public async Task<IActionResult> GetBiometricStatus()
		{
			var now = DateTime.UtcNow;
			var device = await _context.BiometricDevices
				.Where(d => d.UserId == CurrentUserId
					&& d.RevokedAt == null
					&& d.ExpiresAt > now)
				.OrderByDescending(d => d.DateCreated)
				.FirstOrDefaultAsync();

			return Ok(new BiometricStatusResponse
			{
				Enrolled = device != null,
				DeviceLabel = device?.DeviceLabel,
				EnrolledAt = device?.DateCreated,
				ExpiresAt = device?.ExpiresAt,
				MaxAmountWithoutPin = BiometricDeviceKeys.MaxAmountWithoutPin,
			});
		}

		[HttpPost("biometric/enroll")]
		public async Task<IActionResult> EnrollBiometricDevice(
			[FromBody] BiometricEnrollRequest request)
		{
			var user = await _userManager.FindByIdAsync(CurrentUserId);
			if (user == null) return NotFound();

			if (!user.TransactionPinSet || string.IsNullOrWhiteSpace(user.TransactionPin))
				return ResponseHelper.JsonError("Set a transaction PIN before turning this on");

			// Both checks, and a single message for either failure: saying
			// which one was wrong tells someone holding the phone whether it
			// is the password or the PIN they still need to find.
			var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
			var pinValid = BCrypt.Net.BCrypt.Verify(request.TransactionPin, user.TransactionPin);

			if (!passwordValid || !pinValid)
				return ResponseHelper.JsonError("That password or PIN is not correct");

			// One active key per account. Re-enrolling on a new phone retires
			// the old one, so a handset that was sold or lost stops working
			// without the user having to think about it.
			await BiometricDeviceKeys.RevokeAllForUserAsync(
				_context, CurrentUserId, "re_enrolled");

			var key = BiometricDeviceKeys.GenerateKey();

			_context.BiometricDevices.Add(new BiometricDevice
			{
				UserId = CurrentUserId,
				KeyHash = BiometricDeviceKeys.Hash(key),
				DeviceLabel = string.IsNullOrWhiteSpace(request.DeviceLabel)
					? null
					: request.DeviceLabel.Trim(),
				DateCreated = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.Add(BiometricDeviceKeys.Lifetime),
			});

			await _context.SaveChangesAsync();

			return Ok(new BiometricEnrollResponse
			{
				DeviceKey = key,
				ExpiresAt = DateTime.UtcNow.Add(BiometricDeviceKeys.Lifetime),
				MaxAmountWithoutPin = BiometricDeviceKeys.MaxAmountWithoutPin,
			});
		}

		[HttpPost("biometric/revoke")]
		public async Task<IActionResult> RevokeBiometricDevices()
		{
			var revoked = await BiometricDeviceKeys.RevokeAllForUserAsync(
				_context, CurrentUserId, "user");
			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess(revoked > 0
				? "Biometric withdrawal turned off for this account"
				: "Biometric withdrawal was not turned on");
		}

		// -----------------------------------------------------------
		// KYC — SafeHaven BVN/NIN with an OTP to the registered phone.
		// -----------------------------------------------------------

		[HttpPost("kyc/initiate")]
		public async Task<IActionResult> InitiateKyc([FromBody] KycInitiateRequest request)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			if (user.KycVerified)
				return ResponseHelper.JsonError("Your identity is already verified.");

			var (identityId, msg) = await _safeHavenHelper.InitiateIdentityVerification(
				user, request.KycType, request.Number);

			if (string.IsNullOrEmpty(identityId))
				return ResponseHelper.JsonError(msg ?? "Unable to verify this number");

			return ResponseHelper.JsonSuccessWithObject("OTP sent", new { identityId, msg });
		}

		[HttpPost("kyc/verify-otp")]
		public async Task<IActionResult> VerifyKycOtp([FromBody] KycVerifyOtpRequest request)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			var (confirmedIdentityId, reason) = await _safeHavenHelper.ValidateIdentityVerification(
				request.IdentityId, request.IdentityType, request.Otp);

			if (string.IsNullOrEmpty(confirmedIdentityId))
				return ResponseHelper.JsonError(string.IsNullOrWhiteSpace(reason)
					? "Invalid or expired OTP"
					: reason);

			user.KycVerified = true;
			user.KycVerifiedAt = DateTime.UtcNow;
			user.SafeHavenIdentityId = confirmedIdentityId;
			user.SafeHavenIdentityType = request.IdentityType;

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("KYC verification completed");
		}

		// -----------------------------------------------------------
		// Personal Account — the Naira wallet that sits behind Beads.
		// Mirrors the web's /User/PersonalAccount page.
		// -----------------------------------------------------------

		[HttpGet("personal")]
		public async Task<IActionResult> PersonalAccount(int page = 1, int pageSize = 20)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			page = page < 1 ? 1 : page;
			pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

			var query = _context.PersonalTransactions
				.Where(t => t.UserId == user.Id)
				.OrderByDescending(t => t.DateCreated);

			var totalCount = await query.CountAsync();

			var items = await query
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(t => new PersonalTransactionViewModel
				{
					Id = t.Id,
					Type = t.Type.ToString(),
					Amount = t.Amount,
					BalanceAfter = t.BalanceAfter,
					Narration = t.Narration,
					Reference = t.Reference,
					DateCreated = t.DateCreated
				})
				.ToListAsync();

			// Off the fetched rows, for the reason given on DirectionOf.
			foreach (var item in items)
				item.Direction = PersonalTransactionTypes.DirectionOf(item.Type);

			return ResponseHelper.JsonSuccessWithObject("Ok", new
			{
				personalBalance = user.PersonalBalance,
				beadsBalance = user.BeadsBalance,
				accountNumber = user.PersonalAccountNumber,
				accountName = user.PersonalHoldersName,
				kycVerified = user.KycVerified,
				items,
				page,
				pageSize,
				totalCount,
				totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
			});
		}

		/// <summary>
		/// Bank list for the personal-withdrawal picker. The project payments
		/// endpoint bundles this already; personal withdrawals have no project
		/// to hang it off, so they need it standalone.
		/// </summary>
		[HttpGet("banks")]
		public async Task<IActionResult> Banks()
		{
			var banks = await _safeHavenHelper.GetBanksAsync();
			return Ok(new { banks });
		}

		/// <summary>
		/// Profile picture upload, matching what the web's Profile page does.
		/// Returns the stored URL so the client can show it without a refetch.
		/// </summary>
		[HttpPost("profile-picture")]
		[RequestSizeLimit(5 * 1024 * 1024)]
		public async Task<IActionResult> UploadProfilePicture([FromForm] IFormFile file)
		{
			if (file == null || file.Length == 0)
				return ResponseHelper.JsonError("No image was uploaded");

			if (file.Length > MaxAvatarBytes)
				return ResponseHelper.JsonError("Image must be 5MB or smaller");

			// Trust the extension, not the client's content type — the file is
			// served back as a static asset, so an .html or .svg here would run
			// in the browser under our own origin.
			var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();

			if (string.IsNullOrEmpty(extension) || !AllowedAvatarExtensions.Contains(extension))
				return ResponseHelper.JsonError("Only JPG, PNG or WEBP images are allowed");

			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return NotFound();

			var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
			Directory.CreateDirectory(uploadsFolder);

			// Never build the stored name from user input — only the extension
			// survives, so a crafted filename can't traverse out of the folder.
			var fileName = $"{Guid.NewGuid():N}{extension}";
			var filePath = Path.Combine(uploadsFolder, fileName);

			await using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			user.ProfilePictureUrl = $"/uploads/avatars/{fileName}?v={DateTime.UtcNow.Ticks}";
			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccessWithObject("Profile picture updated", new
			{
				profilePictureUrl = user.ProfilePictureUrl
			});
		}
	}
}
