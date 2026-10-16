using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
    public class ProjectCustomField
    {
        [Key]
        public int Id { get; set; }
        public string? FieldName { get; set; } 
        public string? FieldType { get; set; }
        public bool IsRequired { get; set; }
        public int? ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }
        public virtual ICollection<ProjectCustomFieldValue> Values { get; set; } = [];

    }
    public class ProjectCustomFieldValue
    {
        [Key]
        public int Id { get; set; }
        public string? Value { get; set; }
        public int ProjectCustomFieldId { get; set; }
        [ForeignKey(nameof(ProjectCustomFieldId))]
        public virtual ProjectCustomField? ProjectCustomField { get; set; }
        public int? ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }
        public int? ProjectSupporterId { get; set; }
        [ForeignKey(nameof(ProjectSupporterId))]
        public virtual ProjectSupporter? ProjectSupporter { get; set; }
    }
}
