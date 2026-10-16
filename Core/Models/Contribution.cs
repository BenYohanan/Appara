using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Core.Enums.ApparcusEnum;

namespace Core.Models
{
	public class Contribution
	{
		[Key]
		public int Id { get; set; }
		public PaymentStatus PaymentStatus { get; set; }

		public DateTime? ApprovedDate { get; set; }

		public int? ProjectId { get; set; }

		[ForeignKey(nameof(ProjectId))]
		public virtual Project? Project { get; set; }

		public decimal Amount { get; set; }

		public decimal PayableAmount { get; set; }

		public int? ProjectSupporterId { get; set; }

		[ForeignKey(nameof(ProjectSupporterId))]
		public virtual ProjectSupporter? ProjectSupporter { get; set; }

		public DateTime? Date { get; set; }

		public string? VirtualAccountId { get; set; }
		public string? VirtualAccountNumber { get; set; }
		public string? ExternalReference { get; set; }

		/// <summary>
		/// The account the money came from, as the bank reported it when the
		/// payment landed. Shown masked to the project owner only, so they can
		/// match a payment to the person behind it. Null on payments made
		/// before this was kept, and the name is sometimes left out by the
		/// sending bank.
		/// </summary>
		public string? PayerAccountName { get; set; }
		public string? PayerAccountNumber { get; set; }
		/// <summary>The sending bank's 6-digit NIP code.</summary>
		public string? PayerBankCode { get; set; }
	}

}
