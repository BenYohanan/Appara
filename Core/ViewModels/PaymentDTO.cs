using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Core.Enums.ApparcusEnum;

namespace Core.ViewModels
{
    public class ProjectPaymentDTO
    {
        public string? ProjectName { get; set; }
        public List<PaymentDTO>? Payments { get; set; }
    }
    public class PaymentDTO
    {
        public string? Contributor { get; set; }
        public string? PaymentStatusInString { get; set; }
        public string? ContributorPhone { get; set; }
		public string? ProofOfPayment { get; set; }
		public int? ContributorId { get; set; }
        public int? ProjectId { get; set; }
		public PaymentStatus PaymentStatus { get; set; }
		public string? Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public decimal? AmountPaid { get; set; }

        /// <summary>
        /// Where the money came from, for the project owner. The account
        /// number is only ever sent masked, e.g. "008 *** 2382".
        /// </summary>
        public string? PayerAccountName { get; set; }
        public string? PayerAccountNumber { get; set; }
        public string? PayerBank { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? PayerBankCode { get; set; }
    }
}
