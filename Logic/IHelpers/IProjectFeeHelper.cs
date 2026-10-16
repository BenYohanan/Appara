using Core.Models;

namespace Logic.IHelpers
{
	/// <summary>
	/// The one-time fee for opening a money project. See ProjectFeeConstants
	/// for the rule itself.
	/// </summary>
	public interface IProjectFeeHelper
	{
		/// <summary>
		/// What opening one more project would cost this user, and what it
		/// would be paid from — asked before the form is sent so the user is
		/// told up front, not after.
		/// </summary>
		Task<ProjectFeeQuote> QuoteAsync(string userId);

		/// <summary>
		/// Charges a project that has just been created. Never fails the
		/// creation: with nothing to pay from, the fee is recorded as owed and
		/// the returned note says how it will be collected.
		/// </summary>
		Task<ProjectFeeQuote> ChargeNewProjectAsync(Project project, ApplicationUser user);

		/// <summary>
		/// Settles an owed fee before a withdrawal, from Beads, the Personal
		/// Account, or the project's own balance once it is large enough.
		/// Returns null when the project is clear to withdraw from, otherwise
		/// the reason it is not.
		/// </summary>
		Task<string?> SettleBeforeWithdrawalAsync(Project project, ApplicationUser user, Wallet wallet);

		/// <summary>
		/// Called as a contribution lands. When an owed fee can now come out of
		/// the project, marks it paid and takes it off the wallet, and returns
		/// the amount to hold back from what is settled into the owner's
		/// sub-account. Returns 0 when nothing is taken.
		/// </summary>
		decimal TakeOwedFeeFromContribution(Project project, Wallet wallet, decimal incoming);
	}

	public class ProjectFeeQuote
	{
		public decimal Fee { get; set; }
		/// <summary>Free projects left before the fee applies.</summary>
		public int FreeProjectsRemaining { get; set; }
		public bool Charged { get; set; }
		/// <summary>"Beads", "Personal", "Owed", or null when nothing is charged.</summary>
		public string? PaidFrom { get; set; }
		/// <summary>One sentence for the user. Null when there is nothing to say.</summary>
		public string? Note { get; set; }
	}
}
