using Core.Constants;
using Core.DbContext;
using Core.Models;
using Logic.IHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Logic.Helpers
{
	public class ProjectFeeHelper : IProjectFeeHelper
	{
		private const string FromBeads = "Beads";
		private const string FromPersonal = "Personal";
		private const string FromProject = "Project";
		private const string Owed = "Owed";

		private readonly AppDbContext _context;
		private readonly ISafeHavenHelper _safeHaven;
		private readonly ILogger<ProjectFeeHelper> _logger;

		public ProjectFeeHelper(AppDbContext context, ISafeHavenHelper safeHaven, ILogger<ProjectFeeHelper> logger)
		{
			_context = context;
			_safeHaven = safeHaven;
			_logger = logger;
		}

		/// <summary>
		/// Money projects the user has opened, deleted ones included: counting
		/// only live projects would let someone delete and recreate to stay
		/// inside the free five for ever. Type-B projects are paid for by the
		/// record, so they are neither charged nor counted.
		/// </summary>
		private Task<int> ProjectsOpenedAsync(string userId, int? excludingProjectId = null) =>
			_context.Projects.CountAsync(p => p.CreatedById == userId
				&& p.Type == ProjectType.A
				&& (excludingProjectId == null || p.Id != excludingProjectId));

		private static string Naira(decimal amount) => $"₦{amount:N0}";

		private static string OwedNote =>
			$"Your Beads and Personal Account can't cover the {Naira(ProjectFeeConstants.FeeNaira)} project fee right now, "
			+ $"so it will be debited automatically from this project once its balance reaches {Naira(ProjectFeeConstants.CollectFromProjectAt)}. "
			+ "Withdrawals from this project stay on hold until the fee is paid. "
			+ "To clear it sooner, buy Beads or fund your Personal Account.";

		public async Task<ProjectFeeQuote> QuoteAsync(string userId)
		{
			var opened = await ProjectsOpenedAsync(userId);
			var freeLeft = Math.Max(0, ProjectFeeConstants.FreeProjects - opened);

			var quote = new ProjectFeeQuote
			{
				Fee = ProjectFeeConstants.FeeNaira,
				FreeProjectsRemaining = freeLeft,
			};

			if (freeLeft > 0)
			{
				// The one about to be opened is among the free ones.
				var leftAfter = freeLeft - 1;
				quote.Note = leftAfter > 0
					? $"This project is free. You'll have {leftAfter} free project{(leftAfter == 1 ? "" : "s")} left after it."
					: $"This is your last free project. Each project after it has a one-time {Naira(ProjectFeeConstants.FeeNaira)} fee.";
				return quote;
			}

			var user = await _context.ApplicationUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
			quote.Charged = true;

			if (user != null && user.BeadsBalance >= ProjectFeeConstants.FeeNaira)
			{
				quote.PaidFrom = FromBeads;
				quote.Note = $"A one-time {Naira(ProjectFeeConstants.FeeNaira)} project fee applies. It will be paid with {ProjectFeeConstants.FeeNaira:N0} of your Beads.";
			}
			else if (user != null && user.PersonalBalance >= ProjectFeeConstants.FeeNaira
				&& !string.IsNullOrEmpty(user.PersonalAccountNumber))
			{
				quote.PaidFrom = FromPersonal;
				quote.Note = $"A one-time {Naira(ProjectFeeConstants.FeeNaira)} project fee applies. It will be paid from your Personal Account.";
			}
			else
			{
				quote.PaidFrom = Owed;
				quote.Note = $"A one-time {Naira(ProjectFeeConstants.FeeNaira)} project fee applies. "
					+ "You can still create this project: the fee will be debited automatically from it once its balance reaches "
					+ $"{Naira(ProjectFeeConstants.CollectFromProjectAt)}, and withdrawals wait until then.";
			}

			return quote;
		}

		public async Task<ProjectFeeQuote> ChargeNewProjectAsync(Project project, ApplicationUser user)
		{
			var result = new ProjectFeeQuote { Fee = ProjectFeeConstants.FeeNaira };

			if (project.Type != ProjectType.A || project.AccountFeeStatus != ProjectFeeStatus.NotCharged)
				return result;

			var openedBefore = await ProjectsOpenedAsync(user.Id, project.Id);
			result.FreeProjectsRemaining = Math.Max(0, ProjectFeeConstants.FreeProjects - openedBefore - 1);

			if (openedBefore < ProjectFeeConstants.FreeProjects)
				return result;

			result.Charged = true;

			if (await TryPayFromBeadsOrPersonal(project, user))
			{
				result.PaidFrom = project.AccountFeePaidFrom;
				result.Note = project.AccountFeePaidFrom == FromBeads
					? $"A one-time {Naira(ProjectFeeConstants.FeeNaira)} project fee was paid with {ProjectFeeConstants.FeeNaira:N0} of your Beads."
					: $"A one-time {Naira(ProjectFeeConstants.FeeNaira)} project fee was paid from your Personal Account.";
			}
			else
			{
				project.AccountFeeStatus = ProjectFeeStatus.Owed;
				result.PaidFrom = Owed;
				result.Note = OwedNote;
			}

			await _context.SaveChangesAsync();
			return result;
		}

		/// <summary>
		/// Beads first, then the Personal Account. Leaves the fee untouched and
		/// returns false when neither can pay. The caller saves.
		/// </summary>
		private async Task<bool> TryPayFromBeadsOrPersonal(Project project, ApplicationUser user)
		{
			var fee = ProjectFeeConstants.FeeNaira;

			if (user.BeadsBalance >= fee)
			{
				// Beads are our own token: spending them moves no naira, so there
				// is no transfer to wait on.
				user.BeadsBalance -= fee;

				_context.BeadTransactions.Add(new BeadTransaction
				{
					UserId = user.Id,
					ProjectId = project.Id,
					BeadsChange = -fee,
					Reason = $"Project fee for '{project.Title}'",
					DateCreated = DateTime.UtcNow
				});

				MarkPaid(project, FromBeads);
				return true;
			}

			if (user.PersonalBalance >= fee && !string.IsNullOrEmpty(user.PersonalAccountNumber))
			{
				try
				{
					// The money leaves first and the balance follows, never the
					// other way round, so a failed transfer cannot be booked as paid.
					var transfer = await _safeHaven.CollectFeeToCorporateAccount(
						user.PersonalAccountNumber,
						fee,
						$"Apparcus: Project fee - {project.Title}",
						Guid.NewGuid().ToString());

					if (transfer?.data != null)
					{
						user.PersonalBalance -= fee;

						_context.PersonalTransactions.Add(new PersonalTransaction
						{
							UserId = user.Id,
							Type = PersonalTransactionType.ProjectAccountFee,
							Amount = fee,
							BalanceAfter = user.PersonalBalance,
							RelatedProjectId = project.Id,
							Reference = transfer.data.reference,
							Narration = $"Project fee for '{project.Title}'",
							DateCreated = DateTime.UtcNow
						});

						MarkPaid(project, FromPersonal);
						return true;
					}
				}
				catch (Exception ex)
				{
					// Falls through to owed: the user is not refused a project
					// because the bank did not answer.
					_logger.LogWarning(ex, "Project fee for project {ProjectId} could not be taken from the Personal Account.", project.Id);
				}
			}

			return false;
		}

		public async Task<string?> SettleBeforeWithdrawalAsync(Project project, ApplicationUser user, Wallet wallet)
		{
			if (project.AccountFeeStatus != ProjectFeeStatus.Owed)
				return null;

			if (await TryPayFromBeadsOrPersonal(project, user))
			{
				await _context.SaveChangesAsync();
				return null;
			}

			if (wallet.Balance >= ProjectFeeConstants.CollectFromProjectAt
				&& !string.IsNullOrEmpty(user.AccountNumber))
			{
				try
				{
					var transfer = await _safeHaven.CollectFeeToCorporateAccount(
						user.AccountNumber,
						ProjectFeeConstants.FeeNaira,
						$"Apparcus: Project fee - {project.Title}",
						Guid.NewGuid().ToString());

					if (transfer?.data != null)
					{
						wallet.Balance -= ProjectFeeConstants.FeeNaira;
						RecordProjectDeduction(project, transfer.data.reference);
						MarkPaid(project, FromProject);
						await _context.SaveChangesAsync();
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Project fee for project {ProjectId} could not be taken from the project balance.", project.Id);
				}

				return "We couldn't collect this project's fee just now. Please try the withdrawal again.";
			}

			return $"This project's one-time {Naira(ProjectFeeConstants.FeeNaira)} fee hasn't been paid yet, so it can't be withdrawn from. "
				+ $"Buy Beads, fund your Personal Account, or let the project's balance reach {Naira(ProjectFeeConstants.CollectFromProjectAt)}, then try again.";
		}

		public decimal TakeOwedFeeFromContribution(Project project, Wallet wallet, decimal incoming)
		{
			var fee = ProjectFeeConstants.FeeNaira;

			// Held back from the money on its way in, so no extra transfer is
			// needed: the fee simply never reaches the owner's sub-account. A
			// contribution smaller than the fee is left alone and the fee waits
			// for the next one, or for a withdrawal.
			if (project.AccountFeeStatus != ProjectFeeStatus.Owed
				|| wallet.Balance < ProjectFeeConstants.CollectFromProjectAt
				|| incoming < fee)
				return 0m;

			wallet.Balance -= fee;
			RecordProjectDeduction(project, null);
			MarkPaid(project, FromProject);

			_context.Notifications.Add(new Notification
			{
				UserId = project.CreatedById,
				Title = "Project fee paid",
				Body = $"The one-time {Naira(fee)} fee for '{project.Title}' has been taken from the project. Withdrawals are now open.",
				Read = false,
				Type = "Project",
				DateCreated = DateTime.UtcNow
			});

			return fee;
		}

		private void RecordProjectDeduction(Project project, string? reference)
		{
			_context.Transactions.Add(new Transaction
			{
				ProjectId = project.Id,
				AmountPaid = 0,
				PlatformFee = ProjectFeeConstants.FeeNaira,
				ProjectOwnerReceives = 0,
				ProjectBalanceFeeDeduction = ProjectFeeConstants.FeeNaira,
				Reference = reference,
				Status = "Project fee",
				DateCreated = DateTime.UtcNow
			});
		}

		private static void MarkPaid(Project project, string from)
		{
			project.AccountFeeStatus = ProjectFeeStatus.Paid;
			project.AccountFeePaidFrom = from;
			project.AccountFeePaidAt = DateTime.UtcNow;
		}
	}
}
