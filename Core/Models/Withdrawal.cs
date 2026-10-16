using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
    public class Withdrawal
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        public decimal Amount { get; set; }
        public string? TransferReference { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public string? AccountWithdrawnTo { get; set; }
        public string? AccountName { get; set; }
		public DateTime WithdrawalDate { get; set; } = DateTime.UtcNow;
	}


}
