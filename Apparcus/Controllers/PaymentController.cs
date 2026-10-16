using Apparcus.Models;
using Core.Constants;
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
using static Core.Enums.ApparcusEnum;

/// <summary>
/// Signed in by default, with the two contributor-facing endpoints opted back
/// out below.
///
/// There was no class-level rule, so anything without its own [Authorize] was
/// open — including "withdraw", which takes the destination account from the
/// request body and identifies the payee from the project id rather than from
/// the caller. A four-digit PIN was the only thing between an anonymous
/// request and a project's balance.
/// </summary>
[Route("api/payment")]
[ApiController]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
public class PaymentController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly ISafeHavenHelper _safeHavenHelper;
	private readonly IUserHelper _userHelper;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IProjectFeeHelper _projectFeeHelper;

	public PaymentController(AppDbContext context, ISafeHavenHelper safeHavenHelper, IUserHelper userHelper, IHttpContextAccessor httpContextAccessor, IProjectFeeHelper projectFeeHelper)
	{
		_projectFeeHelper = projectFeeHelper;
		_context = context;
		_safeHavenHelper = safeHavenHelper;
		_userHelper = userHelper;
		_httpContextAccessor = httpContextAccessor;
	}

	/// <summary>
	/// Open: a contributor paying into a project has no account here.
	/// </summary>
	[AllowAnonymous]
	[HttpPost("register-supporter")]
	public async Task<IActionResult> RegisterSupporter([FromBody] PaymentRequestViewModel model)
	{
		var project = await _context.Projects
			.FirstOrDefaultAsync(x => x.Id == model.ProjectId);

		if (project == null)
			return BadRequest("Project not found");

		if (model.Amount <= 0)
			return BadRequest("Enter the amount you are contributing.");

		// The contributor's browser previews the charge, but it does not get to
		// decide it — a tampered preview would otherwise open a virtual account
		// expecting less than the published price.
		var payableAmount = TransactionChargeConstants.PayableFor(model.Amount);

		var supporter = await _context.ProjectSupporters
			.FirstOrDefaultAsync(x =>
				x.PhoneNumber == model.PhoneNumber &&
				x.ProjectId == model.ProjectId);

		if (supporter == null)
		{
			supporter = new ProjectSupporter
			{
				ProjectId = model.ProjectId,
				FullName = model.FullName,
				PhoneNumber = model.PhoneNumber,
				Amount = model.Amount,
				DateCreated = DateTime.UtcNow
			};

			_context.ProjectSupporters.Add(supporter);
			await _context.SaveChangesAsync();
		}
		else
		{
			supporter.Amount += model.Amount;
		}

		if (model.CustomFieldValues != null && model.CustomFieldValues.Count != 0)
		{
			foreach (var field in model.CustomFieldValues
						 .Where(x => !string.IsNullOrWhiteSpace(x.Value)))
			{
				var customFieldValue = new ProjectCustomFieldValue
				{
					ProjectCustomFieldId = field.ProjectCustomFieldId.Value,
					Value = field.Value,
					ProjectId = model.ProjectId,
					ProjectSupporterId = supporter.Id
				};

				_context.ProjectCustomFieldValues.Add(customFieldValue);
			}

			await _context.SaveChangesAsync();
		}

		var contribution = new Contribution
		{
			ProjectId = model.ProjectId,
			ProjectSupporterId = supporter.Id,
			Amount = model.Amount,
			PayableAmount = payableAmount,
			PaymentStatus = PaymentStatus.Pending,
			Date = DateTime.UtcNow
		};

		_context.Contributions.Add(contribution);
		await _context.SaveChangesAsync();

		contribution.ExternalReference = GenerateReference(contribution.Id);
		_context.SaveChanges();

		var request = _httpContextAccessor.HttpContext?.Request;

		var callbackUrl =
			$"{request.Scheme}://{request.Host}/api/webhook/safehaven";

		var va = await _safeHavenHelper.CreateVirtualAccount(new CreateVirtualAccountRequest
		{
			AccountName = $"{supporter.Project.Title} | {supporter.FullName}",
			ExternalReference = contribution.ExternalReference,
			Amount = contribution.PayableAmount,
			AmountControl = "Fixed",
			ValidFor = 1200,
			CallbackUrl = callbackUrl
		});

		if (va == null)
			return BadRequest("Unable to create virtual account");

		contribution.VirtualAccountId = va.data._id;
		contribution.VirtualAccountNumber = va.data.accountNumber;

		await _context.SaveChangesAsync();

		return Ok(new
		{
			success = true,
			data = new
			{
				contributionId = contribution.Id,
				contributorId = supporter.Id,
				va.data.accountNumber,
				bank = "Safe Haven Micro Finance",
				va.data.accountName,
				payable = va.data.amount
			}
		});
	}

	/// <summary>
	/// The published charge table, so the contribution form previews exactly
	/// what the server will price and the public pricing page and the app never
	/// drift from it. Anonymous: contributors are not signed in.
	/// </summary>
	[AllowAnonymous]
	[HttpGet("pricing")]
	public IActionResult Pricing()
	{
		return Ok(new
		{
			success = true,
			data = new
			{
				bands = TransactionChargeConstants.Bands.Select(b => new
				{
					minAmount = b.MinAmount,
					maxAmount = b.MaxAmount,
					bankFlat = b.BankFlat,
					bankPercentage = b.BankPercentage,
					apparcusFlat = b.ApparcusFlat,
					earlyBeads = b.EarlyBeads,
					standardBeads = b.StandardBeads,
					amountLabel = b.AmountLabel,
					chargeLabel = b.ChargeLabel,
					beadsLabel = b.BeadsLabel
				}),
				withdrawal = new
				{
					feeNaira = WithdrawalFeeConstants.WithdrawalFeeBeads,
					freePerProject = WithdrawalFeeConstants.FreeWithdrawalsPerProject
				},
				recordPacks = RecordQuotaConstants.Packs.Select(p => new
				{
					size = p.Size,
					priceNaira = p.PriceNaira
				})
			}
		});
	}

	private string GenerateReference(int contributorsId)
	{
		var random = Guid.NewGuid().ToString("N")[..2].ToUpper();
		var time = DateTime.UtcNow.Ticks.ToString()[..3];

		return $"PAY-{time}{random}-{contributorsId}";
	}

	[HttpGet("withdrawal-info")]
	public async Task<IActionResult> GetWithdrawalInfo([FromQuery] int projectId)
	{
		var wallet = await _context.Wallets
			.Include(x => x.Project)
			.FirstOrDefaultAsync(x => x.ProjectId == projectId);

		if (wallet == null)
			return ResponseHelper.JsonError("Wallet not found");

		// A project id is a small number. Without this, anyone signed in could
		// read any project's balance and its owner's Beads by typing one.
		if (wallet.Project?.CreatedById != _userHelper.GetCurrentUserId())
			return ResponseHelper.JsonError("Wallet not found");

		var user = await _userHelper.FindByUserIdAsync(wallet.Project.CreatedById);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		return ResponseHelper.JsonSuccessWithObject("Withdrawal info", new
		{
			walletBalance = wallet.Balance,
			freeWithdrawalsRemaining = wallet.FreeWithdrawalsRemaining,
			beadsBalance = user.BeadsBalance,
			withdrawalFeeBeads = WithdrawalFeeConstants.WithdrawalFeeBeads
		});
	}

	[HttpPost("withdraw")]
	public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request)
	{
		if (request == null)
			return ResponseHelper.ErrorMsg();

		var wallet = await _context.Wallets
			.Include(x => x.Project)
				.ThenInclude(p => p.CreatedBy)
			.FirstOrDefaultAsync(x => x.ProjectId == request.ProjectId);

		if (wallet == null)
			return ResponseHelper.JsonError("No contribution has been made for project");

		var user = wallet.Project?.CreatedBy;

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		// The payee is read off the project, and the destination account comes
		// from the request body, so the caller has to be the project's owner
		// before either is honoured. The transaction PIN below confirms the
		// person; this confirms whose money is being moved.
		if (user.Id != _userHelper.GetCurrentUserId())
			return ResponseHelper.JsonError("You can only withdraw from your own project.");

		if (string.IsNullOrEmpty(user.AccountNumber))
			return ResponseHelper.JsonError("Withdrawal account not set up for this user");

		if (!user.TransactionPinSet || string.IsNullOrWhiteSpace(user.TransactionPin))
			return ResponseHelper.JsonError("Transaction PIN not set");

		// Either the PIN the user typed, or a key from a phone they enrolled.
		// The device-key branch is the narrower of the two: it also stops at
		// the amount cap below.
		var usingDeviceKey = string.IsNullOrWhiteSpace(request.TransactionPin)
			&& !string.IsNullOrWhiteSpace(request.DeviceKey);

		BiometricDevice? authorisedDevice = null;

		if (usingDeviceKey)
		{
			authorisedDevice = await BiometricDeviceKeys.ResolveActiveDeviceAsync(
				_context, user.Id, request.DeviceKey);

			if (authorisedDevice == null)
				return ResponseHelper.JsonError("This phone is no longer approved for biometric withdrawal. Set it up again, or use your PIN.");

			// The cap lives here, not in the app. The app's copy only decides
			// which form to show; this is what a caller bypassing the app runs
			// into.
			if (request.Amount > BiometricDeviceKeys.MaxAmountWithoutPin)
				return ResponseHelper.JsonError($"Withdrawals above ₦{BiometricDeviceKeys.MaxAmountWithoutPin:N0} need your transaction PIN.");
		}
		else
		{
			// Checked before BCrypt sees it: Verify throws on a null, which
			// would answer a request that simply left the field out with a 500
			// instead of "enter your PIN".
			if (string.IsNullOrWhiteSpace(request.TransactionPin))
				return ResponseHelper.JsonError("Enter your transaction PIN");

			var isValidPin = BCrypt.Net.BCrypt.Verify(
				request.TransactionPin,
				user.TransactionPin);

			if (!isValidPin)
				return ResponseHelper.JsonError("Invalid transaction PIN");
		}

		if (wallet.Balance <= 0)
			return ResponseHelper.JsonError("Insufficient balance");

		// A project opened with its fee still owed cannot be withdrawn from
		// until it is paid. This settles it now if anything can cover it, which
		// may take it off the wallet — so it runs before the amount is checked
		// against the balance.
		var feeBlock = await _projectFeeHelper.SettleBeforeWithdrawalAsync(wallet.Project!, user, wallet);
		if (feeBlock != null)
			return ResponseHelper.JsonError(feeBlock);

		const decimal roundingTolerance = 1.00m;

		if (request.Amount > wallet.Balance + roundingTolerance)
		{
			return ResponseHelper.JsonError(
				$"You can't withdraw more than this project's available balance of ₦{wallet.Balance:N2}.");
		}

		var requestedAmount = request.Amount > wallet.Balance ? wallet.Balance : request.Amount;

		// Sending to an Apparcus Personal Account is a book transfer between two
		// sub-accounts of the same corporate account, not a withdrawal to a bank.
		// It has to be routed and accounted for separately — everything below this
		// point prices and settles a real external transfer.
		var personalAccountRecipient = await FindPersonalAccountHolder(request.AccountNumber, request.BankCode);

		if (personalAccountRecipient != null)
		{
			return await WithdrawProjectToPersonalAccount(
				wallet, user, personalAccountRecipient, requestedAmount, request);
		}

		var transferAmount = requestedAmount;
		var walletDeduction = requestedAmount;

		decimal beadsUsed = 0;
		decimal platformAbsorbedCharge = 0;
		decimal projectBalanceFeeDeduction = 0;
		decimal shortfallAmount = 0;
		decimal preToppedUp = 0;
		string feeNote;
		bool requiresConsent = false;

		if (wallet.FreeWithdrawalsRemaining > 0)
		{
			wallet.FreeWithdrawalsRemaining--;

			var used = WithdrawalFeeConstants.FreeWithdrawalsPerProject - wallet.FreeWithdrawalsRemaining;
			feeNote = $"{used} down, you have {wallet.FreeWithdrawalsRemaining} withdrawal point(s) remaining.";
		}
		else if (user.BeadsBalance >= WithdrawalFeeConstants.WithdrawalFeeBeads)
		{
			user.BeadsBalance -= WithdrawalFeeConstants.WithdrawalFeeBeads;
			beadsUsed = WithdrawalFeeConstants.WithdrawalFeeBeads;

			// Beads are an internal loyalty token — no real naira moves when they're
			// spent. Explicitly fund the real SafeHaven bank fee from the corporate
			// account here, so the user's real project balance is never drawn down
			// to cover a fee they already paid for with Beads.
			var beadsFeeTopUp = await _safeHavenHelper.MoveToProjectOwnerSubAccount(
				user.AccountNumber,
				WithdrawalFeeConstants.RealTransferFee);

			if (beadsFeeTopUp?.data == null)
				return ResponseHelper.JsonError("Unable to complete withdrawal — fee coverage failed. Please try again.");

			preToppedUp = WithdrawalFeeConstants.RealTransferFee;
			platformAbsorbedCharge += WithdrawalFeeConstants.RealTransferFee;

			_context.BeadTransactions.Add(new BeadTransaction
			{
				UserId = user.Id,
				ProjectId = wallet.ProjectId,
				BeadsChange = -WithdrawalFeeConstants.WithdrawalFeeBeads,
				Reason = $"Beads used to cover withdrawal charge for project '{wallet.Project?.Title}'",
				DateCreated = DateTime.UtcNow
			});

			feeNote = $"{WithdrawalFeeConstants.WithdrawalFeeBeads:N0} of your Beads will used to complete this withdrawal. (This covers VAT and bank charge)";
		}
		else
		{
			var surplus = wallet.Balance - requestedAmount;
			var fee = WithdrawalFeeConstants.WithdrawalFeeBeads;

			if (surplus >= fee)
			{
				walletDeduction = requestedAmount + fee;
				projectBalanceFeeDeduction = fee;

				feeNote = $"₦{fee:N0} will used to complete this withdrawal. (This covers VAT and bank charge)";
			}
			else
			{
				shortfallAmount = fee - surplus;
				walletDeduction = wallet.Balance;
				transferAmount = requestedAmount - shortfallAmount;
				projectBalanceFeeDeduction = surplus;

				if (transferAmount <= 0)
					return ResponseHelper.JsonError("Balance is too low to cover the withdrawal charge.");

				requiresConsent = true;

				if (!request.ConsentToShortfall)
				{
					return ResponseHelper.JsonError(
						$"This withdrawal will be short by ₦{shortfallAmount:N0} to cover the bank charge. Please confirm to proceed.");
				}

				feeNote = $"The transferred amount will be short of ₦{shortfallAmount:N0} (This covers VAT and bank charge)";
			}
		}

		var requiredHeadroom = transferAmount + WithdrawalFeeConstants.RealTransferFee;

		var userTotalLedgerBalance = await _userHelper.GetTotalProjectWalletBalanceAsync(user.Id) + preToppedUp;

		if (userTotalLedgerBalance < requiredHeadroom)
		{
			var topUpNeeded = requiredHeadroom - userTotalLedgerBalance;

			var topUp = await _safeHavenHelper.MoveToProjectOwnerSubAccount(
				user.AccountNumber,
				topUpNeeded);

			if (topUp?.data == null)
				return ResponseHelper.JsonError("Unable to complete withdrawal — settlement top-up failed. Please try again.");

			platformAbsorbedCharge += topUpNeeded;
		}

		var reference = Guid.NewGuid().ToString();

		var transfer = await _safeHavenHelper.InitiateTransfer(
			user.AccountNumber,
			request.AccountNumber,
			request.BankCode,
			transferAmount,
			$"Withdrawal for project {wallet.Project?.Title}",
			reference,
			request.sessionId);

		if (transfer == null)
			return ResponseHelper.JsonError("Transfer failed");

		if (transfer.data == null)
		{
			if (!string.IsNullOrEmpty(transfer.message))
				return ResponseHelper.JsonError(transfer.message);

			return ResponseHelper.JsonError("Transfer failed");
		}

		wallet.Balance -= walletDeduction;

		_context.Transactions.Add(new Transaction
		{
			ProjectId = wallet.ProjectId,
			AmountPaid = transferAmount,
			PlatformFee = 0,
			ProjectOwnerReceives = transferAmount,
			Reference = transfer.data.reference,
			Status = transfer.data.status,
			DateCreated = DateTime.UtcNow,
			BeadsUsed = beadsUsed,
			PlatformAbsorbedCharge = platformAbsorbedCharge,
			ProjectBalanceFeeDeduction = projectBalanceFeeDeduction,
			ShortfallAmount = shortfallAmount
		});

		_context.Withdrawals.Add(new Withdrawal
		{
			ProjectId = wallet.ProjectId,
			Amount = transferAmount,
			TransferReference = transfer.data.reference,
			Status = transfer.data.status,
			CreatedAt = DateTime.UtcNow,
			AccountWithdrawnTo = request.AccountNumber,
			AccountName = request.AccountName
		});

		var notification = new Notification
		{
			UserId = wallet.Project?.CreatedById ?? null,
			Title = "Withdrawal Successful",
			Body = $"Your withdrawal of ₦{transferAmount:N2} for the project '{wallet.Project?.Title}' was successful.",
			Read = false,
			Type = "Withdrawal",
			DateCreated = DateTime.UtcNow
		};
		_context.Notifications.Add(notification);

		// Stamped only on success, so the timestamp answers "when did this
		// phone last move money?" rather than "when was a key last tried?".
		if (authorisedDevice != null)
			authorisedDevice.LastUsedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Transfer initiated successfully", new
		{
			note = feeNote
		});
	}

	[HttpGet("personal-withdrawal-info")]
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public async Task<IActionResult> GetPersonalWithdrawalInfo()
	{
		var userId = _userHelper.GetCurrentUserId();
		var user = await _userHelper.FindByUserIdAsync(userId);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		return ResponseHelper.JsonSuccessWithObject("Personal withdrawal info", new
		{
			personalBalance = user.PersonalBalance,
			beadsBalance = user.BeadsBalance,
			withdrawalFeeBeads = WithdrawalFeeConstants.WithdrawalFeeBeads
		});
	}

	public class PersonalWithdrawRequest
	{
		public decimal Amount { get; set; }
		public string AccountNumber { get; set; }
		public string BankCode { get; set; }
		public string AccountName { get; set; }
		public string TransactionPin { get; set; }

		/// <summary>
		/// Sent instead of <see cref="TransactionPin"/> by an enrolled phone.
		/// See the checks in <c>WithdrawPersonal</c> for what it is allowed to do.
		/// </summary>
		public string? DeviceKey { get; set; }

		public string sessionId { get; set; }
		public bool ConsentToShortfall { get; set; }
	}

	[HttpPost("withdraw-personal")]
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public async Task<IActionResult> WithdrawPersonal([FromBody] PersonalWithdrawRequest request)
	{
		if (request == null)
			return ResponseHelper.ErrorMsg();

		var userId = _userHelper.GetCurrentUserId();
		var user = await _userHelper.FindByUserIdAsync(userId);

		if (user == null)
			return ResponseHelper.JsonError("User not found");

		if (string.IsNullOrEmpty(user.PersonalAccountNumber))
			return ResponseHelper.JsonError("Personal account not set up. Complete KYC first.");

		if (!user.TransactionPinSet || string.IsNullOrWhiteSpace(user.TransactionPin))
			return ResponseHelper.JsonError("Transaction PIN not set");

		// Same two routes as the project withdrawal. This endpoint already
		// requires a signed-in caller and reads the balance from that user, so
		// there is no separate ownership check to make.
		var usingDeviceKey = string.IsNullOrWhiteSpace(request.TransactionPin)
			&& !string.IsNullOrWhiteSpace(request.DeviceKey);

		BiometricDevice? authorisedDevice = null;

		if (usingDeviceKey)
		{
			authorisedDevice = await BiometricDeviceKeys.ResolveActiveDeviceAsync(
				_context, user.Id, request.DeviceKey);

			if (authorisedDevice == null)
				return ResponseHelper.JsonError("This phone is no longer approved for biometric withdrawal. Set it up again, or use your PIN.");

			if (request.Amount > BiometricDeviceKeys.MaxAmountWithoutPin)
				return ResponseHelper.JsonError($"Withdrawals above ₦{BiometricDeviceKeys.MaxAmountWithoutPin:N0} need your transaction PIN.");
		}
		else
		{
			if (string.IsNullOrWhiteSpace(request.TransactionPin))
				return ResponseHelper.JsonError("Enter your transaction PIN");

			var isValidPin = BCrypt.Net.BCrypt.Verify(request.TransactionPin, user.TransactionPin);

			if (!isValidPin)
				return ResponseHelper.JsonError("Invalid transaction PIN");
		}

		if (user.PersonalBalance <= 0)
			return ResponseHelper.JsonError("Insufficient balance");

		const decimal roundingTolerance = 1.00m;

		if (request.Amount > user.PersonalBalance + roundingTolerance)
		{
			return ResponseHelper.JsonError(
				$"You can't withdraw more than your Personal Account balance of ₦{user.PersonalBalance:N2}.");
		}

		var requestedAmount = request.Amount > user.PersonalBalance ? user.PersonalBalance : request.Amount;

		// As above: another Apparcus Personal Account is not an external bank.
		var personalAccountRecipient = await FindPersonalAccountHolder(request.AccountNumber, request.BankCode);

		if (personalAccountRecipient != null)
		{
			return await TransferBetweenPersonalAccounts(user, personalAccountRecipient, requestedAmount);
		}

		var transferAmount = requestedAmount;
		var balanceDeduction = requestedAmount;

		decimal beadsUsed = 0;
		decimal shortfallAmount = 0;
		decimal preToppedUp = 0;
		string feeNote;

		// NOTE: Personal Account withdrawals have no per-project "free withdrawal
		// points" concept, since that money was never raised for any specific
		// project. Fee waterfall here is: Beads first, otherwise shorted from the
		// withdrawal amount with explicit consent — a simplified version of the
		// project withdrawal waterfall, adapted since Condition 1 doesn't apply.
		if (user.BeadsBalance >= WithdrawalFeeConstants.WithdrawalFeeBeads)
		{
			user.BeadsBalance -= WithdrawalFeeConstants.WithdrawalFeeBeads;
			beadsUsed = WithdrawalFeeConstants.WithdrawalFeeBeads;

			var beadsFeeTopUp = await _safeHavenHelper.MoveToProjectOwnerSubAccount(
				user.PersonalAccountNumber,
				WithdrawalFeeConstants.RealTransferFee);

			if (beadsFeeTopUp?.data == null)
				return ResponseHelper.JsonError("Unable to complete withdrawal — fee coverage failed. Please try again.");

			preToppedUp = WithdrawalFeeConstants.RealTransferFee;

			_context.BeadTransactions.Add(new BeadTransaction
			{
				UserId = user.Id,
				BeadsChange = -WithdrawalFeeConstants.WithdrawalFeeBeads,
				Reason = "Beads used to cover Personal Account withdrawal charge",
				DateCreated = DateTime.UtcNow
			});

			feeNote = $"{WithdrawalFeeConstants.WithdrawalFeeBeads:N0} of your Beads will used to complete this withdrawal. (This covers VAT and bank charge)";
		}
		else
		{
			var surplus = user.PersonalBalance - requestedAmount;
			var fee = WithdrawalFeeConstants.WithdrawalFeeBeads;

			if (surplus >= fee)
			{
				balanceDeduction = requestedAmount + fee;
				feeNote = $"₦{fee:N0} will used to complete this withdrawal. (This covers VAT and bank charge)";
			}
			else
			{
				shortfallAmount = fee - surplus;
				balanceDeduction = user.PersonalBalance;
				transferAmount = requestedAmount - shortfallAmount;

				if (transferAmount <= 0)
					return ResponseHelper.JsonError("Balance is too low to cover the withdrawal charge.");

				if (!request.ConsentToShortfall)
				{
					return ResponseHelper.JsonError(
						$"This withdrawal will be short by ₦{shortfallAmount:N0} to cover the bank charge. Please confirm to proceed.");
				}

				feeNote = $"The transferred amount will be short of ₦{shortfallAmount:N0} (This covers VAT and bank charge)";
			}
		}

		var requiredHeadroom = transferAmount + WithdrawalFeeConstants.RealTransferFee;
		var availableBalance = user.PersonalBalance + preToppedUp;

		if (availableBalance < requiredHeadroom)
		{
			var topUpNeeded = requiredHeadroom - availableBalance;

			var topUp = await _safeHavenHelper.MoveToProjectOwnerSubAccount(
				user.PersonalAccountNumber,
				topUpNeeded);

			if (topUp?.data == null)
				return ResponseHelper.JsonError("Unable to complete withdrawal — settlement top-up failed. Please try again.");
		}

		var reference = Guid.NewGuid().ToString();

		var transfer = await _safeHavenHelper.InitiateTransfer(
			user.PersonalAccountNumber,
			request.AccountNumber,
			request.BankCode,
			transferAmount,
			"Apparcus: Personal Account Withdrawal",
			reference,
			request.sessionId);

		if (transfer == null)
			return ResponseHelper.JsonError("Transfer failed");

		if (transfer.data == null)
		{
			if (!string.IsNullOrEmpty(transfer.message))
				return ResponseHelper.JsonError(transfer.message);

			return ResponseHelper.JsonError("Transfer failed");
		}

		user.PersonalBalance -= balanceDeduction;

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = user.Id,
			Type = PersonalTransactionType.ExternalWithdrawal,
			Amount = balanceDeduction,
			BalanceAfter = user.PersonalBalance,
			Reference = transfer.data.reference,
			Narration = $"Withdrawal to {request.AccountName} ({request.AccountNumber})",
			DateCreated = DateTime.UtcNow
		});

		var notification = new Notification
		{
			UserId = user.Id,
			Title = "Withdrawal Successful",
			Body = $"Your Personal Account withdrawal of ₦{transferAmount:N2} was successful.",
			Read = false,
			Type = "Withdrawal",
			DateCreated = DateTime.UtcNow
		};
		_context.Notifications.Add(notification);

		if (authorisedDevice != null)
			authorisedDevice.LastUsedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Withdrawal successful", new
		{
			note = feeNote,
			newPersonalBalance = user.PersonalBalance
		});
	}

	/// <summary>
	/// The Apparcus user whose Personal Account this number belongs to, or null
	/// when the number is an ordinary external bank account.
	///
	/// Both withdrawal endpoints check this before doing anything else. A
	/// destination inside Apparcus cannot go out over the external rails: the
	/// two accounts are sub-accounts of the same SafeHaven corporate account, so
	/// the movement is a book transfer, and the receiving side's balance lives
	/// in our own ledger rather than arriving as a webhook. WebhookController
	/// deliberately ignores any credit debited from one of our own accounts, on
	/// the stated understanding that whoever initiated it already accounted for
	/// it — which is exactly what these two paths now do.
	/// </summary>
	private async Task<ApplicationUser?> FindPersonalAccountHolder(string? accountNumber, string? bankCode)
	{
		if (string.IsNullOrWhiteSpace(accountNumber))
			return null;

		// Account numbers are only unique within a bank. Matching on the number
		// alone would route money to an Apparcus user whenever some unrelated
		// bank happened to issue the same one.
		if (bankCode != SafeHavenConstants.BankCode)
			return null;

		return await _context.ApplicationUsers
			.FirstOrDefaultAsync(u => u.PersonalAccountNumber == accountNumber);
	}

	/// <summary>
	/// Project balance out to an Apparcus Personal Account. No external bank
	/// fee is incurred, so none of the fee waterfall applies and no withdrawal
	/// point is spent — the money never leaves the platform.
	/// </summary>
	private async Task<IActionResult> WithdrawProjectToPersonalAccount(
		Wallet wallet,
		ApplicationUser owner,
		ApplicationUser recipient,
		decimal amount,
		WithdrawRequest request)
	{
		if (string.IsNullOrEmpty(owner.AccountNumber) || string.IsNullOrEmpty(recipient.PersonalAccountNumber))
			return ResponseHelper.JsonError("That Apparcus account isn't ready to receive money yet.");

		var reference = Guid.NewGuid().ToString();

		// InternalTransfer resolves its own session against the destination.
		// The session the client sent was resolved for an external transfer and
		// does not apply here.
		var transfer = await _safeHavenHelper.InternalTransfer(
			owner.AccountNumber,
			recipient.PersonalAccountNumber,
			amount,
			$"Apparcus: {wallet.Project?.Title} payout",
			reference);

		if (transfer?.data == null)
			return ResponseHelper.JsonError("Transfer failed. Please try again.");

		wallet.Balance -= amount;

		recipient.PersonalBalance += amount;

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = recipient.Id,
			Type = PersonalTransactionType.ProjectPayoutReceived,
			Amount = amount,
			BalanceAfter = recipient.PersonalBalance,
			RelatedProjectId = wallet.ProjectId,
			Reference = transfer.data.reference,
			Narration = $"Payout from '{wallet.Project?.Title}'",
			DateCreated = DateTime.UtcNow
		});

		_context.Withdrawals.Add(new Withdrawal
		{
			ProjectId = wallet.ProjectId,
			Amount = amount,
			TransferReference = transfer.data.reference,
			Status = transfer.data.status,
			CreatedAt = DateTime.UtcNow,
			AccountWithdrawnTo = recipient.PersonalAccountNumber,
			AccountName = request.AccountName
		});

		_context.Notifications.Add(new Notification
		{
			UserId = recipient.Id,
			Title = "Money Received",
			Body = $"₦{amount:N2} moved from '{wallet.Project?.Title}' into your Personal Account.",
			Read = false,
			Type = "Deposit",
			DateCreated = DateTime.UtcNow
		});

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Transfer successful", new
		{
			note = "Moved into your Apparcus Personal Account — no bank fee, and no withdrawal point used.",
			newPersonalBalance = recipient.PersonalBalance
		});
	}

	/// <summary>
	/// Personal Account to another Apparcus Personal Account. Same reasoning as
	/// the project payout above: a book transfer, so no bank fee and both sides
	/// are written here.
	/// </summary>
	private async Task<IActionResult> TransferBetweenPersonalAccounts(
		ApplicationUser sender,
		ApplicationUser recipient,
		decimal amount)
	{
		if (sender.Id == recipient.Id)
			return ResponseHelper.JsonError("That's your own Personal Account.");

		if (string.IsNullOrEmpty(sender.PersonalAccountNumber) || string.IsNullOrEmpty(recipient.PersonalAccountNumber))
			return ResponseHelper.JsonError("That Apparcus account isn't ready to receive money yet.");

		var reference = Guid.NewGuid().ToString();

		var transfer = await _safeHavenHelper.InternalTransfer(
			sender.PersonalAccountNumber,
			recipient.PersonalAccountNumber,
			amount,
			$"Apparcus: transfer from {sender.FullName}",
			reference);

		if (transfer?.data == null)
			return ResponseHelper.JsonError("Transfer failed. Please try again.");

		sender.PersonalBalance -= amount;
		recipient.PersonalBalance += amount;

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = sender.Id,
			Type = PersonalTransactionType.PersonalTransferSent,
			Amount = amount,
			BalanceAfter = sender.PersonalBalance,
			Reference = transfer.data.reference,
			Narration = $"Sent to {recipient.FullName}",
			DateCreated = DateTime.UtcNow
		});

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = recipient.Id,
			Type = PersonalTransactionType.PersonalTransferReceived,
			Amount = amount,
			BalanceAfter = recipient.PersonalBalance,
			Reference = transfer.data.reference,
			Narration = $"Received from {sender.FullName}",
			DateCreated = DateTime.UtcNow
		});

		_context.Notifications.Add(new Notification
		{
			UserId = recipient.Id,
			Title = "Money Received",
			Body = $"₦{amount:N2} arrived from {sender.FullName}.",
			Read = false,
			Type = "Deposit",
			DateCreated = DateTime.UtcNow
		});

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Transfer successful", new
		{
			note = "Sent within Apparcus — no bank fee.",
			newPersonalBalance = sender.PersonalBalance
		});
	}

	[HttpPost("resolve-account")]
	public async Task<IActionResult> ResolveAccount([FromBody] ResolveAccountRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.AccountNumber) || request.AccountNumber.Length != 10)
			return ResponseHelper.JsonError("Invalid account number (must be 10 digits)");

		if (string.IsNullOrWhiteSpace(request.BankCode))
			return ResponseHelper.JsonError("Bank code is required");

		try
		{
			var enquiry = await _safeHavenHelper.NameEnquiry(request.AccountNumber, request.BankCode);

			if (enquiry == null || enquiry.data == null)
				return ResponseHelper.JsonError(enquiry?.message ?? "Account verification failed");

			return ResponseHelper.JsonSuccessWithObject("Account verified", new
			{
				enquiry.data.accountName,
				enquiry.data.bankCode,
				enquiry.data.sessionId
			});
		}
		catch (Exception ex)
		{
			return ResponseHelper.JsonError("Verification error: " + ex.Message);
		}
	}

	/// <summary>
	/// Open: the contributor's own page polls this while their transfer
	/// settles, and they are not signed in.
	/// </summary>
	[AllowAnonymous]
	[HttpGet("check-status")]
	public async Task<IActionResult> CheckStatus([FromQuery] int? contributionId)
	{
		if (!contributionId.HasValue)
			return ResponseHelper.JsonError("Invalid contribution id.");

		var contribution = await _context.Contributions
			.FirstOrDefaultAsync(x => x.Id == contributionId.Value);

		if (contribution == null)
			return ResponseHelper.JsonError("Contribution not found.");

		return ResponseHelper.JsonSuccessWithReturnData(
			contribution.PaymentStatus.ToString());
	}

	public class InternalPaymentRequest
	{
		public int ProjectId { get; set; }
		public decimal Amount { get; set; }
	}

	[HttpPost("pay-internal")]
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public async Task<IActionResult> PayInternal([FromBody] InternalPaymentRequest request)
	{
		if (request == null || request.Amount <= 0)
			return ResponseHelper.JsonError("Invalid amount");

		var userId = _userHelper.GetCurrentUserId();
		var payer = await _userHelper.FindByUserIdAsync(userId);

		if (payer == null)
			return ResponseHelper.JsonError("User not found");

		if (string.IsNullOrEmpty(payer.PersonalAccountNumber))
			return ResponseHelper.JsonError("Your Personal Account isn't set up yet. Complete KYC first.");

		var project = await _context.Projects
			.Include(p => p.CreatedBy)
			.FirstOrDefaultAsync(p => p.Id == request.ProjectId && !p.Deleted);

		if (project == null)
			return ResponseHelper.JsonError("Project not found");

		var creator = project.CreatedBy;

		if (creator == null || string.IsNullOrEmpty(creator.AccountNumber))
			return ResponseHelper.JsonError("This project's owner hasn't set up a payout account yet.");

		const decimal internalFee = 9m;
		var totalDebit = request.Amount + internalFee;

		if (payer.PersonalBalance < totalDebit)
			return ResponseHelper.JsonError($"Insufficient Personal Account balance. You need ₦{totalDebit:N2} (₦{request.Amount:N2} + ₦{internalFee:N0} fee).");

		var reference = Guid.NewGuid().ToString();

		var transfer = await _safeHavenHelper.InternalTransfer(
			payer.PersonalAccountNumber,
			creator.AccountNumber,
			request.Amount,
			$"Apparcus: {project.Title} Payout",
			reference);

		if (transfer?.data == null)
			return ResponseHelper.JsonError("Transfer failed. Please try again.");

		try
		{
			await _safeHavenHelper.CollectFeeToCorporateAccount(
				payer.PersonalAccountNumber,
				internalFee,
				$"Apparcus: Internal Payment Fee - {payer.FullName}",
				$"{reference}-fee");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Fee collection failed for {reference}: {ex.Message}");
		}

		var supporter = await _context.ProjectSupporters
			.FirstOrDefaultAsync(x => x.PhoneNumber == payer.PhoneNumber && x.ProjectId == project.Id);

		if (supporter == null)
		{
			supporter = new ProjectSupporter
			{
				ProjectId = project.Id,
				FullName = payer.FullName,
				PhoneNumber = payer.PhoneNumber,
				Amount = request.Amount,
				DateCreated = DateTime.UtcNow
			};
			_context.ProjectSupporters.Add(supporter);
			await _context.SaveChangesAsync();
		}
		else
		{
			supporter.Amount += request.Amount;
		}

		payer.PersonalBalance -= totalDebit;

		_context.PersonalTransactions.Add(new PersonalTransaction
		{
			UserId = payer.Id,
			Type = PersonalTransactionType.InternalProjectPayment,
			Amount = totalDebit,
			BalanceAfter = payer.PersonalBalance,
			RelatedProjectId = project.Id,
			Reference = reference,
			Narration = $"Contribution to '{project.Title}' (includes ₦{internalFee:N0} fee)",
			DateCreated = DateTime.UtcNow
		});

		project.AmountObtained += request.Amount;

		var wallet = await _context.Wallets.FirstOrDefaultAsync(x => x.ProjectId == project.Id);

		if (wallet == null)
		{
			wallet = new Wallet { ProjectId = project.Id, ProjectOwnerId = project.CreatedById, Balance = 0 };
			_context.Wallets.Add(wallet);
		}

		wallet.Balance += request.Amount;

		var contribution = new Contribution
		{
			ProjectId = project.Id,
			ProjectSupporterId = supporter.Id,
			Amount = request.Amount,
			PayableAmount = request.Amount,
			PaymentStatus = PaymentStatus.Successful,
			Date = DateTime.UtcNow,
			ApprovedDate = DateTime.UtcNow,
			ExternalReference = reference,
			// Paid from an Apparcus Personal Account, so that is the source.
			PayerAccountName = payer.PersonalHoldersName ?? payer.FullName,
			PayerAccountNumber = payer.PersonalAccountNumber,
			PayerBankCode = SafeHavenConstants.BankCode
		};
		_context.Contributions.Add(contribution);

		if (project.DateCreated != null)
		{
			var hoursSinceCreated = (DateTime.UtcNow - project.DateCreated.Value).TotalHours;
			var beadsEarned = TransactionChargeConstants.BeadsFor(request.Amount, hoursSinceCreated);

			if (beadsEarned > 0)
			{
				creator.BeadsBalance += beadsEarned;

				_context.BeadTransactions.Add(new BeadTransaction
				{
					UserId = creator.Id,
					ProjectId = project.Id,
					BeadsChange = beadsEarned,
					Reason = "Internal payment contribution reward",
					DateCreated = DateTime.UtcNow
				});
			}
		}

		await _context.SaveChangesAsync();

		return ResponseHelper.JsonSuccessWithObject("Payment successful", new
		{
			newPersonalBalance = payer.PersonalBalance
		});
	}
}