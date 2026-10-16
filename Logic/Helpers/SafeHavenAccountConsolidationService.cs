using Core.DbContext;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Logic.Helpers
{
	public class ConsolidationReport
	{
		public List<string> Actions { get; } = [];
		public List<string> Warnings { get; } = [];
	}

	public class SafeHavenAccountConsolidationService
	{
		private readonly AppDbContext _context;
		private readonly ISafeHavenHelper _safeHaven;
		private readonly ILogger<SafeHavenAccountConsolidationService> _logger;

		public SafeHavenAccountConsolidationService(AppDbContext context, ILogger<SafeHavenAccountConsolidationService> logger, ISafeHavenHelper safeHaven)
		{
			_context = context;
			_logger = logger;
			_safeHaven = safeHaven;
		}

		public async Task<ConsolidationReport> RunAsync(bool dryRun = true)
		{
			var report = new ConsolidationReport();

			var groups = await _context.Projects
				.Where(p => !string.IsNullOrEmpty(p.SafeHavenAccountId))
				.Include(p => p.Wallets)
				.ToListAsync();

			foreach (var group in groups.GroupBy(p => p.CreatedById))
			{
				var userId = group.Key;
				var user = await _context.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId);

				if (user == null)
				{
					report.Warnings.Add($"User {userId} not found — skipping {group.Count()} project(s).");
					continue;
				}

				var needsProjectAccount = string.IsNullOrEmpty(user.SafeHavenAccountId);
				var needsPersonalAccount = string.IsNullOrEmpty(user.PersonalSafeHavenAccountId);

				if (!needsProjectAccount && !needsPersonalAccount)
				{
					report.Warnings.Add($"User {userId} already has both a consolidated project account and a personal account — skipping.");
					continue;
				}

				var projects = group.OrderBy(p => p.DateCreated).ToList();

				// Reuse existing SafeHaven sub-accounts instead of minting new ones where possible:
				// the earliest project's account becomes the user's consolidated PROJECT account,
				// and — if the user has a second project account sitting unused — that one is
				// repurposed as the user's PERSONAL account instead of calling CreateSubAccount again.
				// Only if no second project account exists do we fall back to creating a fresh one.
				Project primaryForProject = needsProjectAccount ? projects.First() : null;

				Project primaryForPersonal = needsPersonalAccount
					? projects.Skip(needsProjectAccount ? 1 : 0).FirstOrDefault()
					: null;

				if (needsProjectAccount)
				{
					report.Actions.Add(
						$"User {userId}: assigning account {primaryForProject.AccountNumber} ({primaryForProject.SafeHavenAccountId}) from project {primaryForProject.Id} ('{primaryForProject.Title}') as the consolidated PROJECT account.");
				}

				if (needsPersonalAccount)
				{
					if (primaryForPersonal != null)
					{
						report.Actions.Add(
							$"User {userId}: reusing account {primaryForPersonal.AccountNumber} ({primaryForPersonal.SafeHavenAccountId}) from project {primaryForPersonal.Id} ('{primaryForPersonal.Title}') as the PERSONAL account (no new SafeHaven sub-account created).");
					}
					else
					{
						report.Actions.Add(
							$"User {userId}: no spare project account to reuse — will create a NEW SafeHaven sub-account for the PERSONAL account.");
					}
				}

				report.Actions.Add(
					$"User {userId}: clearing SafeHaven fields and zeroing wallet balances on all {projects.Count} project(s).");

				if (dryRun)
					continue;

				if (needsProjectAccount)
				{
					user.AccountNumber = primaryForProject.AccountNumber;
					user.AccountBank = primaryForProject.AccountBank;
					user.HoldersName = primaryForProject.HoldersName;
					user.SafeHavenAccountId = primaryForProject.SafeHavenAccountId;
				}

				if (needsPersonalAccount)
				{
					if (primaryForPersonal != null)
					{
						user.PersonalAccountNumber = primaryForPersonal.AccountNumber;
						user.PersonalHoldersName = primaryForPersonal.HoldersName;
						user.PersonalSafeHavenAccountId = primaryForPersonal.SafeHavenAccountId;
						user.PersonalAccountBank = primaryForPersonal.AccountBank;
					}
					else
					{
						try
						{
							var account = await _safeHaven.CreateSubAccount(user, $"PERSONAL_{user.Id}_{Guid.NewGuid()}");

							if (account == null || string.IsNullOrEmpty(account.data._id))
							{
								report.Warnings.Add($"User {userId}: creating a new PERSONAL SafeHaven sub-account failed — user left without one.");
							}
							else
							{
								user.PersonalAccountNumber = account.data.accountNumber;
								user.PersonalHoldersName = account.data.accountName;
								user.PersonalSafeHavenAccountId = account.data._id;
								user.PersonalAccountBank = account.data._id;
							}
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Personal Account creation failed during consolidation for user {UserId}", userId);
							report.Warnings.Add($"User {userId}: creating a new PERSONAL SafeHaven sub-account threw {ex.Message} — user left without one.");
						}
					}
				}

				foreach (var p in projects)
				{
					p.AccountNumber = null;
					p.AccountBank = null;
					p.HoldersName = null;
					p.SafeHavenAccountId = null;

					if (p.Wallets != null)
					{
						foreach (var w in p.Wallets)
							w.Balance = 0;
					}
				}
			}

			if (!dryRun)
			{
				await _context.SaveChangesAsync();
				_logger.LogInformation("SafeHaven consolidation applied: {ActionCount} action(s), {WarningCount} warning(s).", report.Actions.Count, report.Warnings.Count);
			}
			else
			{
				_logger.LogInformation("SafeHaven consolidation dry-run: {ActionCount} action(s) would be taken, {WarningCount} warning(s).", report.Actions.Count, report.Warnings.Count);
			}

			return report;
		}
	}
}