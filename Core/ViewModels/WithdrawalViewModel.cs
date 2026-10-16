using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModels
{
	public class WithdrawalViewModel
	{
		public int Id { get; set; }

		public string? ProjectName { get; set; }
		public string? SentTo { get; set; }
		public decimal Amount { get; set; }
		public string? TransferReference { get; set; }
		public string? Status { get; set; }
        public string? AccountWithdrawnTo { get; set; }
        public DateTime CreatedAt { get; set; }

	}
}
