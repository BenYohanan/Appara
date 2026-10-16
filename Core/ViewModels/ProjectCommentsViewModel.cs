
using Core.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.ViewModels
{
    public class ProjectCommentsViewModel
    {
        public string? FullName { get; set; }
        public string? Comment { get; set; }
        public bool Deleted { get; set; }
        public DateTime DateCreated { get; set; }
        public int ProjectId { get; set; }
  
    }
}
