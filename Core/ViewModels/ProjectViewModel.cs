using Core.Models;

namespace Core.ViewModels
{
    public class ProjectViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal AmountNeeded { get; set; }
        public decimal Balance { get; set; }
        public decimal AmountObtained { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public bool Deleted { get; set; }
        public DateTime? DateCreated { get; set; }
        public string? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedByPhoneNumber { get; set; }
        public string? CreatedByEmail { get; set; }
        public string? SupportLink { get; set; }
        public string? QRCodeBase64 { get; set; }
        public string? OwnersProfilePic { get; set; }
		public bool ShowForAudit { get; set; }
		/// <summary>Findable by name from the public site, with the creator's email withheld.</summary>
		public bool IsPublic { get; set; }
		/// <summary>
		/// Whether a 4-digit code gates the public page. The code itself is
		/// never carried on a view model — only ever the fact that one exists.
		/// </summary>
		public bool HasAccessCode { get; set; }
		public string? Status { get; set; }
		/// <summary>"A" or "B". See Project.Type.</summary>
		public string? Type { get; set; }
		public DateTime? CustomDate { get; set; }
		/// <summary>
		/// How many records a Type-B project holds. Counted in the projection
		/// because the list view nulls ProjectSupporters to keep the query cheap.
		/// </summary>
		public int RecordCount { get; set; }
		public string? RelatedImageUrl { get; set; }
		/// <summary>
		/// The custom field shown beside each name on the public payment list
		/// instead of the amount. Null means the amount.
		/// </summary>
		public int? PaymentListFieldId { get; set; }
		/// <summary>
		/// The same choice by field name. A new project's fields have no ids
		/// until they are saved, so the create forms can only name the one they
		/// mean; the server resolves it. Filled on the way out too, for the edit
		/// forms.
		/// </summary>
		public string? PaymentListField { get; set; }
		/// <summary>NotCharged, Paid or Owed. See Project.AccountFeeStatus.</summary>
		public string? AccountFeeStatus { get; set; }
		public DateTime? CreatedByDateJoined { get; set; }
        public virtual ICollection<ProjectSupporter>? ProjectSupporters { get; set; }
        public virtual ICollection<ProjectContributionVM>? ProjectContributions { get; set; }
        public virtual ICollection<ProjectComment>? Comments { get; set; }
        public virtual ICollection<WithdrawalViewModel>? Withdrawals { get; set; }
        public List<ProjectCustomFieldVM>? CustomFields { get; set; }
    }
    /// <summary>
    /// One record on a Type-B project: a payment that already happened, typed
    /// in from a paper sheet. Name and amount are the only required parts;
    /// everything else the owner wants is a custom field.
    /// </summary>
    public class ProjectRecordViewModel
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string? FullName { get; set; }
        public decimal Amount { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? DateCreated { get; set; }
        public List<ProjectRecordFieldValue> FieldValues { get; set; } = [];
    }

    public class ProjectRecordFieldValue
    {
        public int ProjectCustomFieldId { get; set; }
        public string? FieldName { get; set; }
        public string? Value { get; set; }
    }

    /// <summary>
    /// What the user has used of what they have paid for, counted across all
    /// their Type-B projects together.
    /// </summary>
    public class RecordQuotaStatus
    {
        public int Used { get; set; }
        public int Quota { get; set; }
        public int Remaining => Quota - Used > 0 ? Quota - Used : 0;
        public decimal PersonalBalance { get; set; }

        /// <summary>Every pack on offer, smallest first.</summary>
        public List<RecordPackVM> Packs { get; set; } = [];

        /// <summary>
        /// The smallest pack, kept flat for clients written before packs came
        /// in more than one size.
        /// </summary>
        public int PackSize { get; set; }
        public decimal PackPriceNaira { get; set; }
    }

    public class RecordPackVM
    {
        public int Size { get; set; }
        public decimal PriceNaira { get; set; }
    }

    public class ProjectCustomFieldVM
    {
        public int? Id { get; set; }
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }
        public bool IsRequired { get; set; }
    }
	public class ProjectContributionVM
	{
		public string? ContributorsName { get; set; }
		public string? Amount { get; set; }
		public DateTime? Date { get; set; }
		/// <summary>
		/// What the payment list shows beside the name: the answer to the field
		/// the owner chose, or null to show the amount.
		/// </summary>
		public string? DisplayValue { get; set; }
		public string? DisplayLabel { get; set; }
		[System.Text.Json.Serialization.JsonIgnore]
		public int? ProjectSupporterId { get; set; }
	}

	/// <summary>
	/// A public project as it appears in search results. Deliberately narrow:
	/// it carries only what a stranger may see, so nothing private can leak
	/// through the search endpoint even by accident.
	/// </summary>
	public class PublicProjectSearchResult
	{
		public int Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		/// <summary>
		/// Null on a code-protected project. Showing what a project has raised
		/// to anyone who can type its name would hand over the very thing the
		/// code exists to hold back, so the figures wait for the code.
		/// </summary>
		public decimal? AmountNeeded { get; set; }
		public decimal? AmountObtained { get; set; }
		/// <summary>Lets the search UI say "code required" before the click.</summary>
		public bool HasAccessCode { get; set; }
		/// <summary>
		/// "A" or "B". A Type-B project is raising towards nothing, so a
		/// result for one must not be rendered as progress against a target.
		/// </summary>
		public string? Type { get; set; }
		public DateTime? DateCreated { get; set; }
	}
}
