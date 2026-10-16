using Core.Constants;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.Helpers;
using Logic.IHelpers;
using Logic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.ApparcusEnum;

namespace Apparcus.Controllers
{
	[ApiController]
	[Route("api/webhook")]
	public class WebhookController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly ISafeHavenHelper _safeHavenHelper;
		private readonly IHubContext<PaymentHub> _hub;
		private readonly ILogger<WebhookController> _logger;
		private readonly string _corporateAccountNumber;
		private readonly IProjectFeeHelper _projectFeeHelper;

		public WebhookController(
			AppDbContext context,
			ISafeHavenHelper safeHavenHelper,
			IHubContext<PaymentHub> hub,
			ILogger<WebhookController> logger,
			IConfiguration config,
			IProjectFeeHelper projectFeeHelper)
		{
			_projectFeeHelper = projectFeeHelper;
			_context = context;
			_safeHavenHelper = safeHavenHelper;
			_hub = hub;
			_logger = logger;
			_corporateAccountNumber = config["SafeHaven:DebitAccountNumber"] ?? "";
		}

		[HttpPost("safehaven")]
		public async Task<IActionResult> SafeHavenWebhook([FromBody] SafeHavenWebhookModel model)
		{
			try
			{
				if (model.Data == null)
					return Ok();

				// Data.Type: "Inwards" for a credit, "Outwards" for a debit.
				var isVirtualAccountTransfer = model.Type == "virtualAccount.transfer";
				var isAccountCredit = model.Type == "transfer" && model.Data.Type == "Inwards";
				var isAccountDebit = model.Type == "transfer" && model.Data.Type == "Outwards";

				if (isAccountDebit)
				{
					return Ok();
				}

				if (!isVirtualAccountTransfer && !isAccountCredit)
				{
					_logger.LogInformation(
						"Ignoring unhandled webhook event. Type={Type} Data.Type={DataType}",
						model.Type, model.Data?.Type);
					return Ok();
				}

				var amount = model.Data.Amount;

				if (isAccountCredit)
				{
					return await HandleAccountCredit(model, amount);
				}

				var sendersBankCode = model.Data.SessionId != null && model.Data.SessionId.Length >= 6
					? model.Data.SessionId[..6]
					: null;

				var contribution = await _context.Contributions
					.Include(x => x.Project)
						.ThenInclude(p => p.CreatedBy)
					.Include(x => x.ProjectSupporter)
					.FirstOrDefaultAsync(x =>
						x.VirtualAccountNumber == model.Data.CreditAccountNumber
						|| x.Id.ToString() == model.Data.ExternalReference);

				if (contribution == null)
					return Ok();

				if (contribution.PaymentStatus == PaymentStatus.Successful)
					return Ok();

				if (amount < contribution.PayableAmount)
				{
					contribution.PaymentStatus =
						PaymentStatus.UnderPaid;

					_context.SaveChanges();

					await _hub.Clients
						.Group(contribution.Id.ToString())
						.SendAsync("PaymentCompleted", new
						{
							status = "UnderPaid"
						});

					await _safeHavenHelper.RefundTransfer(
							model.Data.DebitAccountNumber!,
							sendersBankCode,
							amount,
							"Underpayment Refund");

					return Ok();
				}

				if (amount > contribution.PayableAmount)
				{
					contribution.PaymentStatus =
						PaymentStatus.OverPaid;

					_context.SaveChanges();

					await _hub.Clients
						.Group(contribution.Id.ToString())
						.SendAsync("PaymentCompleted", new
						{
							status = "OverPaid"
						});

					await _safeHavenHelper.RefundTransfer(
						model.Data.DebitAccountNumber!,
						sendersBankCode,
						amount,
						"Overpayment Refund");

					return Ok();
				}

				decimal totalPaid = amount;
				decimal projectOwnerReceives = contribution.Amount;
				decimal platformFee = totalPaid - projectOwnerReceives;

				var transaction = new Transaction
				{
					ProjectId = contribution.ProjectId,
					ProjectSupporterId = contribution.ProjectSupporterId,
					AmountPaid = totalPaid,
					PlatformFee = platformFee,
					ProjectOwnerReceives = projectOwnerReceives,
					Reference = model.Data.PaymentReference,
					Status = "Successful",
					DateCreated = DateTime.UtcNow
				};

				_context.Transactions.Add(transaction);

				var notification = new Notification
				{
					UserId = contribution.Project?.CreatedById ?? null,
					Title = "Contribution Successful",
					Body = $"{contribution.ProjectSupporter?.FullName ?? "A supporter"} contribution of ₦{contribution.Amount:N2} for the project '{contribution.Project?.Title ?? ""}' was successful.",
					Read = false,
					Type = "Contribution",
					DateCreated = DateTime.UtcNow
				};
				_context.Notifications.Add(notification);

				contribution.Project.AmountObtained += projectOwnerReceives;

				var wallet = await _context.Wallets
					.FirstOrDefaultAsync(x => x.ProjectId == contribution.ProjectId);

				if (wallet == null)
				{
					wallet = new Wallet
					{
						ProjectId = contribution.ProjectId.Value,
						ProjectOwnerId = contribution.Project.CreatedById,
						Balance = 0
					};

					_context.Wallets.Add(wallet);
				}

				wallet.Balance += projectOwnerReceives;

				// A project opened with its fee owed pays it out of the first
				// contribution that takes it to the threshold. What is held back
				// stays in the corporate account, where the contribution landed.
				var feeHeldBack = _projectFeeHelper.TakeOwedFeeFromContribution(
					contribution.Project!, wallet, projectOwnerReceives);

				contribution.PaymentStatus = PaymentStatus.Successful;
				contribution.ApprovedDate = DateTime.UtcNow;

				// Who paid, as the bank reports it. Kept so the project owner can
				// tell which account a payment came from; the sending bank is the
				// first six digits of the NIP session id, the same code the
				// refunds above send money back to.
				contribution.PayerAccountName = string.IsNullOrWhiteSpace(model.Data.DebitAccountName)
					? null
					: model.Data.DebitAccountName.Trim();
				contribution.PayerAccountNumber = string.IsNullOrWhiteSpace(model.Data.DebitAccountNumber)
					? null
					: model.Data.DebitAccountNumber.Trim();
				contribution.PayerBankCode = sendersBankCode;

				var creator = contribution.Project?.CreatedBy;

				if (creator != null && contribution.Project?.DateCreated != null)
				{
					var hoursSinceProjectCreated =
						(contribution.ApprovedDate.Value - contribution.Project.DateCreated.Value).TotalHours;

					var isEarly = hoursSinceProjectCreated <= TransactionChargeConstants.EarlyWindowHours;

					// What a contribution is worth in Beads follows the same
					// published band its charge came from, so the smallest
					// contributions — which we now barely charge for — earn none.
					var beadsEarned = TransactionChargeConstants.BeadsFor(contribution.Amount, isEarly);

					if (beadsEarned > 0)
					{
						creator.BeadsBalance += beadsEarned;

						_context.BeadTransactions.Add(new BeadTransaction
						{
							UserId = creator.Id,
							ProjectId = contribution.ProjectId,
							ContributionId = contribution.Id,
							BeadsChange = beadsEarned,
							Reason = isEarly
								? "Early contribution incentive (within 5 days of project creation)"
								: "Standard contribution reward",
							DateCreated = DateTime.UtcNow
						});
					}
				}

				await _context.SaveChangesAsync();

				await _hub.Clients
					.Group(contribution.Id.ToString())
					.SendAsync("PaymentCompleted", new
					{
						status = "Successful"
					});

				if (!string.IsNullOrEmpty(creator?.AccountNumber))
				{
					await _safeHavenHelper.MoveToProjectOwnerSubAccount(
							creator.AccountNumber,
							projectOwnerReceives - feeHeldBack);
				}
				else
				{
					_logger.LogWarning(
						"Contribution {ContributionId}: could not settle ₦{Amount:N2} — project creator {UserId} has no SafeHaven account number.",
						contribution.Id, projectOwnerReceives, contribution.Project?.CreatedById);
				}

				return Ok();
			}
			catch (Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}

		private async Task<bool> IsInternalSystemTransfer(string? debitAccountNumber)
		{
			if (string.IsNullOrEmpty(debitAccountNumber))
				return false;

			// Any credit sourced from our own corporate settlement account, or from
			// another of our own users' SafeHaven sub-accounts, can only have been
			// initiated by our own backend — an external sender has no way to move
			// money out of an account they don't hold credentials for. In every such
			// case, the code that initiated the transfer already updated the relevant
			// balance synchronously right after the transfer call succeeded, so this
			// webhook is just an echo of something we've already accounted for.
			if (debitAccountNumber == _corporateAccountNumber)
				return true;

			return await _context.ApplicationUsers.AnyAsync(u =>
				u.AccountNumber == debitAccountNumber || u.PersonalAccountNumber == debitAccountNumber);
		}

		private async Task<IActionResult> HandleAccountCredit(SafeHavenWebhookModel model, decimal amount)
		{
			if (await IsInternalSystemTransfer(model.Data.DebitAccountNumber))
			{
				_logger.LogInformation(
					"Ignoring account.credit for ₦{Amount:N2} to {CreditAccountNumber} — sourced from our own account {DebitAccountNumber}, already handled synchronously by the code that initiated it.",
					amount, model.Data.CreditAccountNumber, model.Data.DebitAccountNumber);
				return Ok();
			}

			if (model.Data.CreditAccountNumber == _corporateAccountNumber)
			{
				_logger.LogInformation(
					"Credit of ₦{Amount:N2} landed in corporate account — expected from a fee/purchase collection.",
					amount);
				return Ok();
			}

			// Personal Account — legitimate deposit destination.
			var personalUser = await _context.ApplicationUsers
				.FirstOrDefaultAsync(u => u.PersonalAccountNumber == model.Data.CreditAccountNumber);

			if (personalUser != null)
			{
				if (!string.IsNullOrEmpty(model.Data.PaymentReference))
				{
					var alreadyProcessed = await _context.PersonalTransactions
						.AnyAsync(t => t.UserId == personalUser.Id
							&& t.Type == PersonalTransactionType.ExternalDeposit
							&& t.Reference == model.Data.PaymentReference);

					if (alreadyProcessed)
						return Ok();
				}

				personalUser.PersonalBalance += amount;

				// Who sent it, when the bank tells us. A deposit that reads only
				// as "external bank deposit" leaves the user to work out which
				// of the week's transfers this was; the sender's name is the one
				// detail that answers it, and it is already in the payload.
				var senderName = model.Data.DebitAccountName?.Trim();
				var fromSender = string.IsNullOrWhiteSpace(senderName)
					? null
					: senderName;

				_context.PersonalTransactions.Add(new PersonalTransaction
				{
					UserId = personalUser.Id,
					Type = PersonalTransactionType.ExternalDeposit,
					Amount = amount,
					BalanceAfter = personalUser.PersonalBalance,
					Reference = model.Data.PaymentReference,
					Narration = fromSender == null
						? "External bank deposit into Personal Account"
						: $"Received from {fromSender}",
					DateCreated = DateTime.UtcNow
				});

				_context.Notifications.Add(new Notification
				{
					UserId = personalUser.Id,
					Title = "Deposit Received",
					Body = fromSender == null
						? $"₦{amount:N2} was deposited into your Personal Account."
						: $"₦{amount:N2} was deposited into your Personal Account by {fromSender}.",
					Read = false,
					Type = "Deposit",
					DateCreated = DateTime.UtcNow
				});

				await _context.SaveChangesAsync();
				return Ok();
			}

			// Credit landing here (from an account we don't recognize
			// as our own) can't be attributed to any specific project, so it's
			// refunded rather than left untracked.
			var projectUser = await _context.ApplicationUsers
				.FirstOrDefaultAsync(u => u.AccountNumber == model.Data.CreditAccountNumber);

			if (projectUser != null)
			{
				_logger.LogWarning(
					"Unexpected direct deposit of ₦{Amount:N2} into Project Payout Account for user {UserId} — refunding.",
					amount, projectUser.Id);

				if (!string.IsNullOrEmpty(model.Data.DebitAccountNumber))
				{
					var sendersBankCode = model.Data.SessionId != null && model.Data.SessionId.Length >= 6
						? model.Data.SessionId[..6]
						: null;

					if (!string.IsNullOrEmpty(sendersBankCode))
					{
						await _safeHavenHelper.RefundTransfer(
							model.Data.DebitAccountNumber,
							sendersBankCode,
							amount,
							"Refund: this account does not accept direct deposits");
					}
					else
					{
						_logger.LogWarning(
							"Cannot refund deposit to Project Payout Account for user {UserId} — unable to determine sender's bank code from webhook payload.",
							projectUser.Id);
					}
				}

				_context.Notifications.Add(new Notification
				{
					UserId = projectUser.Id,
					Title = "Deposit Returned",
					Body = $"A deposit of ₦{amount:N2} was sent to an internal account that doesn't accept direct deposits. It has been refunded to the sender, if possible. Use your Personal Account ({projectUser.PersonalAccountNumber ?? "not set up"}) to receive money directly.",
					Read = false,
					Type = "Deposit",
					DateCreated = DateTime.UtcNow
				});

				await _context.SaveChangesAsync();
				return Ok();
			}

			_logger.LogWarning(
				"Received account credit for ₦{Amount:N2} to unrecognized account {CreditAccountNumber} — no matching user found. Manual reconciliation needed.",
				amount, model.Data.CreditAccountNumber);

			return Ok();
		}
	}
}