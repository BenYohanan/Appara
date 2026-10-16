using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModels
{
	public class AdminDashboardDto
	{
		public int? ProjectCount { get; set; }
		public int? ClientCount { get; set; }
		public int? ContibutorsCount { get; set; }
		public decimal? TotalEarnings { get; set; }
		public string? UserName { get; set; }
		public List<ProjectViewModel>? Projects { get; set; }
		public bool KYCVerified { get; set; }
		public string? UserId { get; set; }
	}
	public class UserDashboardDto
	{
		public string? FullName { get; set; }
		public string? Email { get; set; }
		public string? PhoneNumber { get; set; }
		public int? ProjectCount { get; set; }
		public int? ContibutorsCount { get; set; }
		public decimal? TotalEarnings { get; set; }
		public decimal? WalletBalance { get; set; }
		public List<ProjectViewModel>? Projects { get; set; }
		public bool KYCVerified { get; set; }
		public string? UserId { get; set; }
		public decimal? BeadsBalance { get; set; }
		public decimal? PersonalBalance { get; set; }
		public string? AccountNumber { get; set; }
		public string? AccountName { get; set; }
		public string? PersonalAccountNumber { get; set; }
		public string? PersonalAccountName { get; set; }
	}

	public class BeadTransactionViewModel
	{
		public int Id { get; set; }
		public decimal BeadsChange { get; set; }
		public string Reason { get; set; }
		public string ProjectTitle { get; set; }
		public DateTime DateCreated { get; set; }
	}
	public class PersonalTransactionViewModel
	{
		public int Id { get; set; }
		public string Type { get; set; }
		public string Direction { get; set; }
		public decimal Amount { get; set; }
		public decimal BalanceAfter { get; set; }
		public string Narration { get; set; }

		/// <summary>
		/// The bank's reference for the transfer behind this row. Carried so a
		/// receipt can quote something the sending or receiving bank will also
		/// recognise — a receipt without one is just a screenshot.
		/// </summary>
		public string? Reference { get; set; }

		public DateTime DateCreated { get; set; }
	}
}