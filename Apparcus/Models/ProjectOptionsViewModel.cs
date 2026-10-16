namespace Apparcus.Models
{
	/// <summary>
	/// Drives _ProjectOptions, the block of optional settings shared by the add
	/// and edit project modals. The two modals prefix their element ids ("" and
	/// "edit_"), so the partial is told the prefix rather than the markup being
	/// written out twice and drifting apart.
	/// </summary>
	public class ProjectOptionsViewModel
	{
		public string Prefix { get; set; } = string.Empty;

		/// <summary>Container the custom-field rows are appended to.</summary>
		public string CustomFieldsContainerId { get; set; } = string.Empty;

		/// <summary>Global JS function the "+ Add Field" button calls.</summary>
		public string AddCustomFieldFunction { get; set; } = string.Empty;

		/// <summary>
		/// True on the edit modal, where a code may already exist and can be
		/// changed or removed. The add modal can only ever set a first one.
		/// </summary>
		public bool IsEdit { get; set; }

		/// <summary>
		/// False on a Type-B project. "Show for Audit" adds a withdrawal
		/// history, and a Type-B project never takes money to withdraw.
		/// </summary>
		public bool ShowAuditToggle { get; set; } = true;
	}
}
