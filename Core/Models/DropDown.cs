using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Core.Enums.ApparcusEnum;

namespace Core.Models
{
    public class DropDown 
    {
        public DropDown()
        {
            DateCreated = DateTime.Now;
            Deleted = false;
        }
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool Deleted { get; set; }
        public DateTime DateCreated { get; set; }
        public DropdownEnums DropdownKey { get; set; }
    }
}
