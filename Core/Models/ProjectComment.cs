using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ProjectComment
    {
        public ProjectComment()
        {
            Deleted = false;
            DateCreated = DateTime.Now;
        }

        [Key]
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Comment { get; set; }
        public bool Deleted { get; set; }
        public DateTime DateCreated { get; set; }
        public int ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; }
    }
}
