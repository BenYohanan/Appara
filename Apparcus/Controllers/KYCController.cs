using Apparcus.Models;
using Core.Constants;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.Helpers;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Apparcus.Controllers
{
	/// <summary>
	/// KYC is reached from both surfaces — the dashboard modal over the Identity
	/// cookie, the app over its JWT — so it accepts either. Authenticating on
	/// the cookie alone answered the app with a redirect to the login page,
	/// which is what "your session could not be verified for KYC" was.
	/// </summary>
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public class KycController : ControllerBase
	{
		private readonly ISafeHavenHelper _safeHaven;
		private readonly IUserHelper _userHelper;
		private readonly AppDbContext _context;

		public KycController(ISafeHavenHelper safeHaven, IUserHelper userHelper, AppDbContext context)
		{
			_safeHaven = safeHaven;
			_userHelper = userHelper;
			_context = context;
		}

		/// <summary>
		/// Read from the token's claims rather than the server-side session,
		/// which only the web login ever fills in — for an app caller the old
		/// lookup resolved to a null id and found nobody.
		/// </summary>
		private string? CurrentUserId => _userHelper.GetCurrentUserId();

		[HttpPost]
		public async Task<JsonResult> InitiateKyc([FromBody] UserKycInitiateRequest model)
		{
			var userId = CurrentUserId;
			var user = _context.ApplicationUsers.FirstOrDefault(u => u.Id == userId);

			if (user == null)
			{
				return ResponseHelper.ErrorMsg();
			}

			if (user.KycVerified)
				return ResponseHelper.JsonError("User already verified.");

			var (identityId, message) = await _safeHaven.InitiateIdentityVerification(
				user,
				model.IdentityType,
				model.IdentityNumber
			).ConfigureAwait(false);

			if (string.IsNullOrEmpty(identityId))
			{
				return ResponseHelper.JsonError(message);
			}

			return ResponseHelper.JsonSuccessWithObject(message, identityId);
		}


		[HttpPost]
		public async Task<JsonResult> ValidateKyc([FromBody] KycValidateRequest model)
		{
			var userId = CurrentUserId;
			var user = _context.ApplicationUsers.FirstOrDefault(u => u.Id == userId);

			if (user == null)
			{
				return ResponseHelper.ErrorMsg();
			}

			if (user.KycVerified)
				return ResponseHelper.JsonError("User already verified.");

			var (confirmedIdentityId, reason) = await _safeHaven.ValidateIdentityVerification(
				model.IdentityId,
				model.IdentityType,
				model.Otp
			);

			if (string.IsNullOrEmpty(confirmedIdentityId))
				// SafeHaven's own words when it gave any — "OTP expired" tells the
				// user to tap Resend, where "please try again" has them retyping
				// a code that can no longer work.
				return ResponseHelper.JsonError(string.IsNullOrWhiteSpace(reason)
					? "OTP verification failed. Please try again."
					: reason);

			user.SafeHavenIdentityId = confirmedIdentityId;
			user.SafeHavenIdentityType = model.IdentityType;
			user.KycVerified = true;
			user.KycVerifiedAt = DateTime.UtcNow;

			var personalAccountCreated = await EnsurePersonalAccount(user);

			// Saved before the branch below, not after it. The personal account
			// is a follow-on step; letting its failure skip the save threw away
			// a verification the user had already passed the OTP for, and left
			// them verified at Safe Haven but not here.
			await _context.SaveChangesAsync();

			if (!personalAccountCreated)
			{
				// KYC itself succeeded — don't fail the whole request over this.
				// The account can be created lazily the next time it's needed
				// (e.g. on first Move Beads / Buy Beads attempt).
				return ResponseHelper.JsonSuccess(
					"KYC verification successful. Your personal account is still being set up — this will resolve shortly.");
			}

			return ResponseHelper.JsonSuccess("KYC verification successful");
		}

		[HttpPost]
		public async Task<JsonResult> SetTransactionPin([FromBody] KycSetTransactionPinRequest model)
		{
			var userId = CurrentUserId;
			var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == userId);
			if (user == null)
				return ResponseHelper.ErrorMsg();

			if (user.TransactionPinSet)
				return ResponseHelper.JsonSuccess("User already set transaction pin.");

			if (string.IsNullOrEmpty(model.TransactionPin))
				return ResponseHelper.JsonError("Transaction PIN is required.");

			user.TransactionPin = BCrypt.Net.BCrypt.HashPassword(model.TransactionPin);
			user.TransactionPinSet = true;

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccess("Transaction PIN set successfully");
		}

		private async Task<bool> EnsurePersonalAccount(ApplicationUser user)
		{
			if (!string.IsNullOrEmpty(user.PersonalSafeHavenAccountId))
				return true;

			try
			{
				var account = await _safeHaven.CreateSubAccount(user, $"PERSONAL_{user.Id}_{Guid.NewGuid()}");

				if (account == null || string.IsNullOrEmpty(account.data._id))
					return false;

				user.PersonalAccountNumber = account.data.accountNumber;
				user.PersonalHoldersName = account.data.accountName;
				user.PersonalSafeHavenAccountId = account.data._id;
				user.PersonalAccountBank = account.data._id;

				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}