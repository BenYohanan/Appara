using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModels
{
    public class CreateProjectDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal AmountNeeded { get; set; }
        public string AccountNumber { get; set; }
        public string BankName { get; set; }
        public string AccountHolderName { get; set; }

    }
}
