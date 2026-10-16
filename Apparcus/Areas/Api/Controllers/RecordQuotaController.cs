using Apparcus.Models;
using Core.Constants;
using Core.DbContext;
using Core.Models;
using Logic.Helpers;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Apparcus.Areas.Api.Controllers
{
	/// <summary>
	/// Type-B projects keep records rather than take money, so they are paid
	/// for by the record: one pack — in any of the sizes
	/// <see cref="RecordQuotaConstants.Packs"/> offers — covers all of a user's
	/// Type-B projects together.
	///
	/// Both surfaces hit this — the web dashboard over the Identity cookie and
	/// the mobile app over a JWT — which is what naming both schemes is for.
	/// </summary>
	[Route("api/record-quota")]
	[ApiController]
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public class RecordQuotaController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly IProjectHelper _projectHelper;
		private readonly ISafeHavenHelper _safeHavenHelper;
		private readonly IUserHelper _userHelper;

		public RecordQuotaController(
			AppDbContext context,
			IProjectHelper projectHelper,
			ISafeHavenHelper safeHavenHelper,
			IUserHelper userHelper)
		{
			_context = context;
			_projectHelper = projectHelper;
			_safeHavenHelper = safeHavenHelper;
			_userHelper = userHelper;
		}

		[HttpGet("status")]
		public async Task<IActionResult> Status()
		{
			var userId = _userHelper.GetCurrentUserId();
			var user = await _userHelper.FindByUserIdAsync(userId);

			if (user == null)
				return ResponseHelper.JsonError("User not found");

			return ResponseHelper.JsonSuccessWithReturnData(_projectHelper.GetRecordQuota(user.Id));
		}

		/// <summary>
		/// Buys one pack, priced from the server's own table rather than
		/// anything the caller sends — <paramref name="packSize"/> only picks
		/// which published pack, and an unrecognised size is refused. The money
		/// is pulled from the user's Personal Account into the corporate account
		/// first, and the quota is only credited once that transfer comes back —
		/// never the other way round, so a failed transfer cannot leave someone
		/// holding records they did not pay for.
		/// </summary>
		[HttpPost("buy")]
		public async Task<IActionResult> Buy([FromQuery] int? packSize)
		{
			var userId = _userHelper.GetCurrentUserId();
			var user = await _userHelper.FindByUserIdAsync(userId);

			if (user == null)
				return ResponseHelper.JsonError("User not found");

			// No size means the client predates the larger packs, so it gets the
			// one it was written against.
			var pack = packSize == null
				? RecordQuotaConstants.DefaultPack
				: RecordQuotaConstants.PackOfSize(packSize.Value);

			if (pack == null)
				return ResponseHelper.JsonError("That record pack is not one we offer.");

			if (string.IsNullOrEmpty(user.PersonalAccountNumber))
				return ResponseHelper.JsonError("Personal account not set up. Complete KYC first.");

			if (user.PersonalBalance < pack.PriceNaira)
				return ResponseHelper.JsonError(
					$"Insufficient Personal Account balance. You need ₦{pack.PriceNaira:N0}.");

			var reference = Guid.NewGuid().ToString();

			var transfer = await _safeHavenHelper.CollectFeeToCorporateAccount(
				user.PersonalAccountNumber,
				pack.PriceNaira,
				$"Apparcus: {pack.Size:N0} Records - {user.FullName}",
				reference);

			if (transfer?.data == null)
				return ResponseHelper.JsonError("Unable to complete that purchase right now. Please try again.");

			user.PersonalBalance -= pack.PriceNaira;
			user.RecordQuota += pack.Size;

			_context.PersonalTransactions.Add(new PersonalTransaction
			{
				UserId = user.Id,
				Type = PersonalTransactionType.RecordQuotaPurchased,
				Amount = pack.PriceNaira,
				BalanceAfter = user.PersonalBalance,
				Reference = transfer.data.reference,
				Narration = $"Protected {pack.Size:N0} records",
				DateCreated = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return ResponseHelper.JsonSuccessWithObject(
				$"You can now save another {pack.Size:N0} records.",
				_projectHelper.GetRecordQuota(user.Id));
		}
	}
}
