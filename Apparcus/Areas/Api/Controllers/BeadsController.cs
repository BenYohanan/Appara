using Core.Constants;
using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Logic.Helpers;
using Logic.IHelpers;
using Core.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/beads")]
[ApiController]
// Both surfaces hit this controller: the web dashboard over the Identity
// cookie, and the mobile app over a JWT. Naming both schemes is what makes
// the bearer token actually get validated here — without it a mobile call
// arrives anonymous and falls through to "User not found".
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
public class BeadsController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly ISafeHavenHelper _safeHavenHelper;
	private readonly IUserHelper _userHelper;

	private const decimal BeadPurchasePackSize = 50m;
	private const decimal BeadPurchasePriceNaira = 50m;

	public BeadsController(AppDbContext context, ISafeHavenHelper safeHavenHelper, IUserHelper userHelper)
	{
		_context = context;
		_safeHavenHelper = safeHavenHelper;
		_userHelper = userHelper;
	}

	public class MoveBeadsRequest
	{
		public decimal Amount { get; set; }
	}

	[HttpPost("move")]
	public async Task<IActionResult> MoveBeads([FromBody] MoveBeadsRequest request)
	{
		var userId = _userHelper.GetCurrentUserId();
		var user = await _userHelper.FindByUserIdAsync(userId);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		if (request.Amount <= 0)
			return ResponseHelper.JsonError("Invalid amount");

		if (user.BeadsBalance < request.Amount)
			return ResponseHelper.JsonError($"Insufficient Beads. You have {user.BeadsBalance:N0}.");

		if (string.IsNullOrEmpty(user.PersonalAccountNumber))
			return ResponseHelper.JsonError("Personal account not set up. Complete KYC first.");

		// 1 Bead = ₦1, moved from Apparcus Main Corporate Account into the user's own Personal sub-account.
		var reference = Guid.NewGuid().ToString();

		var transfer = await _safeHavenHelper.TransferFromCorporateAccount(
			user.PersonalAccountNumber,
			"090286",
			request.Amount,
			$"Apparcus: Beads Moved - {user.FullName}",
			reference);

		if (transfer?.data == null)
			return ResponseHelper.JsonError("Unable to move Beads right now. Please try again.");

		user.BeadsBalance -= request.Amount;
		user.PersonalBalance += request.Amount;

		_context.BeadTransactions.Add(new BeadTransaction
		{
			UserId = user.Id,
			BeadsChange = -request.Amount,
			Reason = "Beads converted to Naira (Move Beads)",
			DateCreated = DateTime.UtcNow
		});

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = user.Id,
			Type = PersonalTransactionType.BeadsMovedIn,
			Amount = request.Amount,
			BalanceAfter = user.PersonalBalance,
			Reference = transfer.data.reference,
			Narration = $"Apparcus: Beads Moved - {user.FullName}",
			DateCreated = DateTime.UtcNow
		});

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Beads moved to your Personal Account", new
		{
			newBeadsBalance = user.BeadsBalance,
			newPersonalBalance = user.PersonalBalance
		});
	}

	[HttpPost("buy")]
	public async Task<IActionResult> BuyBeads()
	{
		var userId = _userHelper.GetCurrentUserId();
		var user = await _userHelper.FindByUserIdAsync(userId);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		if (string.IsNullOrEmpty(user.PersonalAccountNumber))
			return ResponseHelper.JsonError("Personal account not set up. Complete KYC first.");

		if (user.PersonalBalance < BeadPurchasePriceNaira)
			return ResponseHelper.JsonError($"Insufficient Personal Account balance. You need ₦{BeadPurchasePriceNaira:N0}.");

		var reference = Guid.NewGuid().ToString();

		// Pull the real money out of the user's Personal Account into the corporate
		// account — mirrors MoveBeads' TransferFromCorporateAccount in reverse. This
		// keeps the corporate account's real balance in sync with what it will later
		// owe out when this user (or anyone) moves Beads back to Naira.
		var transfer = await _safeHavenHelper.CollectFeeToCorporateAccount(
			user.PersonalAccountNumber,
			BeadPurchasePriceNaira,
			$"Apparcus: Beads Purchase - {user.FullName}",
			reference);

		if (transfer?.data == null)
			return ResponseHelper.JsonError("Unable to purchase Beads right now. Please try again.");

		user.PersonalBalance -= BeadPurchasePriceNaira;
		user.BeadsBalance += BeadPurchasePackSize;

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = user.Id,
			Type = PersonalTransactionType.BeadsPurchased,
			Amount = BeadPurchasePriceNaira,
			BalanceAfter = user.PersonalBalance,
			Reference = transfer.data.reference,
			Narration = $"Purchased {BeadPurchasePackSize:N0} Beads",
			DateCreated = DateTime.UtcNow
		});

		_context.BeadTransactions.Add(new BeadTransaction
		{
			UserId = user.Id,
			BeadsChange = BeadPurchasePackSize,
			Reason = $"Purchased with ₦{BeadPurchasePriceNaira:N0} from Personal Account",
			DateCreated = DateTime.UtcNow
		});

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Beads purchased successfully", new
		{
			newBeadsBalance = user.BeadsBalance,
			newPersonalBalance = user.PersonalBalance
		});
	}

	/// <summary>Balance plus the ledger behind it — what the mobile Beads screen renders.</summary>
	[HttpGet("history")]
	public async Task<IActionResult> History(int page = 1, int pageSize = 20)
	{
		var userId = _userHelper.GetCurrentUserId();
		var user = await _userHelper.FindByUserIdAsync(userId);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		page = page < 1 ? 1 : page;
		pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

		var query = _context.BeadTransactions
			.Where(t => t.UserId == user.Id)
			.OrderByDescending(t => t.DateCreated);

		var totalCount = await query.CountAsync();

		var items = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(t => new BeadTransactionViewModel
			{
				Id = t.Id,
				BeadsChange = t.BeadsChange,
				Reason = t.Reason,
				ProjectTitle = t.Project != null ? t.Project.Title : "",
				DateCreated = t.DateCreated
			})
			.ToListAsync();

		return ResponseHelper.JsonSuccessWithObject("Ok", new
		{
			beadsBalance = user.BeadsBalance,
			personalBalance = user.PersonalBalance,
			beadPackSize = BeadPurchasePackSize,
			beadPackPrice = BeadPurchasePriceNaira,
			items,
			page,
			pageSize,
			totalCount,
			totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
		});
	}
}
