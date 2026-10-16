using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	public class BeadTransaction
	{
		[Key]
		public int Id { get; set; }

		public string? UserId { get; set; }
		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }

		public int? ProjectId { get; set; }
		[ForeignKey(nameof(ProjectId))]
		public virtual Project? Project { get; set; }

		public int? ContributionId { get; set; }
		[ForeignKey(nameof(ContributionId))]
		public virtual Contribution? Contribution { get; set; }

		public decimal BeadsChange { get; set; }
		public string? Reason { get; set; }
		public DateTime DateCreated { get; set; } = DateTime.UtcNow;
	}
}