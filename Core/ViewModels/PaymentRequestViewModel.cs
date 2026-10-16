using Core.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModels
{
    public class PaymentRequestViewModel
    {
        public int ProjectId { get; set; }
        public int? ProjectSupporterId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<ProjectCustomFieldValueVM>? CustomFieldValues { get; set; }
		public decimal Amount { get; set; }
	}
    public class ProjectCustomFieldValueVM
    {
        public int? ProjectCustomFieldId { get; set; }
        public string? Value { get; set; }
        public string FieldName { get; set; } = string.Empty;
    }

	public class WithdrawRequest
	{
		public int ProjectId { get; set; }
		public decimal Amount { get; set; }
		public string? AccountNumber { get; set; }
		public string? BankCode { get; set; }
		public string? sessionId { get; set; }
		public string? TransactionPin { get; set; }

		/// <summary>
		/// Sent instead of <see cref="TransactionPin"/> by a phone the user has
		/// enrolled for biometric withdrawal. Only honoured for a signed-in
		/// project owner, and only below the server's own amount cap.
		/// </summary>
		public string? DeviceKey { get; set; }

		public string? AccountName { get; set; }
		public bool ConsentToShortfall { get; set; }
	}

	public class ResolveAccountRequest
	{
		public string AccountNumber { get; set; } = null!;
		public string BankCode { get; set; } = null!;
	}
}
