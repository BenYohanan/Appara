using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	public class Notification
	{
		[Key]
		public int Id { get; set; }
		public string? UserId { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string Type { get; set; } = "System";
		public bool Read { get; set; }
		public DateTime DateCreated { get; set; } = DateTime.UtcNow;
		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }
	}
}
