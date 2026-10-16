using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	public enum PersonalTransactionType
	{
		ExternalDeposit,
		BeadsMovedIn,
		InternalProjectPayment,
		BeadsPurchased,
		ExternalWithdrawal,
		RecordQuotaPurchased,
		/// <summary>Money moved out of a project into this Personal Account.</summary>
		ProjectPayoutReceived,
		/// <summary>Sent to another Apparcus user's Personal Account.</summary>
		PersonalTransferSent,
		/// <summary>Received from another Apparcus user's Personal Account.</summary>
		PersonalTransferReceived,
		/// <summary>The one-time fee for opening a project.</summary>
		ProjectAccountFee
	}

	public static class PersonalTransactionTypes
	{
		/// <summary>
		/// The types that add to the Personal Account balance, and so read as
		/// money in wherever the history is shown.
		///
		/// Kept here rather than spelled out at each call site: the web page
		/// and the app's wallet each decided this for themselves and both had
		/// stopped at the first two, so a project payout and money received
		/// from another Apparcus user were drawn as money going out.
		///
		/// </summary>
		public static readonly PersonalTransactionType[] MoneyIn =
		[
			PersonalTransactionType.ExternalDeposit,
			PersonalTransactionType.BeadsMovedIn,
			PersonalTransactionType.ProjectPayoutReceived,
			PersonalTransactionType.PersonalTransferReceived,
		];

		private static readonly HashSet<string> MoneyInNames =
			MoneyIn.Select(t => t.ToString()).ToHashSet(StringComparer.Ordinal);

		/// <summary>
		/// "Money-In" or "Money-Out" for a transaction whose type has already
		/// been read as a name.
		///
		/// Deliberately answered in memory, off the rows a page has already
		/// fetched, rather than inside the query: a projection is all-or-
		/// nothing in EF, and a clause it cannot translate does not degrade —
		/// it throws, and takes the whole history page with it.
		/// </summary>
		public static string DirectionOf(string? typeName) =>
			typeName != null && MoneyInNames.Contains(typeName) ? "Money-In" : "Money-Out";
	}

	public class PersonalTransaction
	{
		[Key]
		public int Id { get; set; }

		public string? UserId { get; set; }
		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }

		public PersonalTransactionType Type { get; set; }
		public decimal Amount { get; set; }
		public decimal BalanceAfter { get; set; }

		public int? RelatedProjectId { get; set; }
		[ForeignKey(nameof(RelatedProjectId))]
		public virtual Project? RelatedProject { get; set; }

		public string? Reference { get; set; }
		public string? Narration { get; set; }
		public DateTime DateCreated { get; set; } = DateTime.UtcNow;
	}
}