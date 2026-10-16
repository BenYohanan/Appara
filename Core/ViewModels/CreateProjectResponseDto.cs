using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModels
{
    public class CreateProjectResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProjectId { get; set; }
        public string AccountNumber { get; set; }
        public string BankName { get; set; }
    }
}
