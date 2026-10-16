using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
    public class ProjectSupporter
    {
        public ProjectSupporter()
        {
            Deleted = false;
            DateCreated = DateTime.Now;
        }
        [Key]
        public int Id { get; set; }
        public string? FullName { get; set; }
        public decimal Amount { get; set; }
        public string? PhoneNumber { get; set; }
        public bool Deleted { get; set; }
		public DateTime? DateCreated { get; set; }
		public int ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }
        public virtual ICollection<ProjectComment>? Comments { get; set; }
        public virtual ICollection<ProjectCustomFieldValue> ProjectCustomFieldValues { get; set; } = [];
    }
}
