using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
namespace Core.Models
{
	public class ApplicationUser : IdentityUser
	{
		public ApplicationUser()
		{
			Deleted = false;
			DateCreated = DateTime.UtcNow;
			TransactionPinSet = false;
			KycVerified = false;
		}
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public DateTime? DateOfBirth { get; set; }
		public bool Deleted { get; set; }
		public DateTime DateCreated { get; set; }
		public decimal BeadsBalance { get; set; } = 0;
		[NotMapped]
		public string FullName => FirstName + " " + LastName;
		[NotMapped]
		public List<string?> Roles { get; set; }
		[NotMapped]
		public string? UserRole { get; set; }
		public string? ProfilePictureUrl { get; set; }
		public string? SafeHavenIdentityId { get; set; }
		public string? SafeHavenIdentityType { get; set; }
		public string? TransactionPin { get; set; }
		public bool TransactionPinSet { get; set; }
		public bool KycVerified { get; set; }
		public DateTime? KycVerifiedAt { get; set; }

		public string? AccountNumber { get; set; }
		public string? AccountBank { get; set; }
		public string? HoldersName { get; set; }
		public string? SafeHavenAccountId { get; set; }

		public string? PersonalAccountNumber { get; set; }
		public string? PersonalAccountBank { get; set; }
		public string? PersonalHoldersName { get; set; }
		public string? PersonalSafeHavenAccountId { get; set; }
		public decimal PersonalBalance { get; set; } = 0m;

		/// <summary>
		/// How many Type-B records this user has paid to keep, counted across
		/// all their Type-B projects together. Buying a pack adds to it; the
		/// records themselves are counted at the point one is added.
		/// </summary>
		public int RecordQuota { get; set; } = 0;
	}
}