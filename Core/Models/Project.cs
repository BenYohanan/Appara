using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
    public class Project
    {
        public Project()
        {
            Deleted = false;
            DateCreated = DateTime.Now;
            Status = ProjectStatus.Active;
            Type = ProjectType.A;
		}
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal AmountNeeded { get; set; }
        public decimal AmountObtained { get; set; }
		public string? AccountNumber { get; set; }
		public string? AccountBank { get; set; }
		public string? HoldersName { get; set; }
		public string? RecipientCode { get; set; }
		public string? SafeHavenAccountId { get; set; }
		public bool Deleted { get; set; }
		public bool ShowForAudit { get; set; }
		/// <summary>
		/// Opt-in: lets anyone find this project by name from the public site.
		/// Off by default; making a project findable is always a deliberate act.
		/// What a stranger may then see is decided by AccessCode below and by
		/// ProjectHelper.GetPublicProjectById.
		/// </summary>
		public bool IsPublic { get; set; }
		/// <summary>
		/// BCrypt hash of the 4-digit code a contributor must enter before the
		/// public payment page will open. Null means no code has been set, and
		/// the project is reachable by anyone who finds it in search.
		///
		/// Hashed rather than stored plainly because people reuse four digits —
		/// a leaked project code should not hand anyone a transaction PIN.
		/// </summary>
		public string? AccessCode { get; set; }
        public DateTime? DateCreated { get; set; }
		/// <summary>
		/// A (the default): money is collected through Apparcus. B: a record of
		/// payments that already happened elsewhere, typed in by the owner to
		/// replace a paper sheet. A Type-B project never takes a contribution,
		/// so it has no wallet, no account number and no KYC requirement.
		/// </summary>
		public ProjectType Type { get; set; }
		/// <summary>
		/// The date the records belong to, set by the owner — the day the
		/// offering was taken, which is rarely the day it gets typed up. Kept
		/// apart from DateCreated so editing the project cannot move it.
		/// </summary>
		public DateTime? CustomDate { get; set; }
		public string? RelatedImageUrl { get; set; }
		/// <summary>
		/// The custom field shown beside each contributor's name on the public
		/// payment list, in place of the amount. Null shows the amount.
		///
		/// A plain id rather than a foreign key: fields are dropped and re-added
		/// whenever the project is edited, and a field that has gone simply
		/// falls back to the amount when the list is drawn.
		/// </summary>
		public int? PaymentListFieldId { get; set; }
		/// <summary>
		/// The one-time fee for opening this project. See ProjectFeeConstants.
		/// </summary>
		public ProjectFeeStatus AccountFeeStatus { get; set; }
		/// <summary>Where the fee came from once paid: Beads, Personal or Project.</summary>
		public string? AccountFeePaidFrom { get; set; }
		public DateTime? AccountFeePaidAt { get; set; }
        public ProjectStatus? Status { get; set; }
		public string? CreatedById { get; set; }
        [ForeignKey(nameof(CreatedById))]
        public virtual ApplicationUser? CreatedBy { get; set; }
        public virtual ICollection<ProjectSupporter>? ProjectSupporters { get; set; }
        public virtual ICollection<Contribution>? Contributions { get; set; }
        public virtual ICollection<ProjectComment>? Comments { get; set; }
        public virtual ICollection<Wallet>? Wallets { get; set; }
        public virtual ICollection<ProjectCustomField> CustomFields { get; set; }
        public decimal? AmountRemaining => AmountNeeded - AmountObtained;
    }

    public enum ProjectType
	{
		A,
		B
	}

    public enum ProjectFeeStatus
	{
		/// <summary>One of the free projects, or a Type-B project.</summary>
		NotCharged,
		Paid,
		/// <summary>
		/// Created with nothing to pay from. Collected out of the project once
		/// it holds enough, and withdrawals wait until it is.
		/// </summary>
		Owed
	}

    public enum ProjectStatus
	{
		Active,
		Closed,
		Disabled,
        Deleted
	}
}
