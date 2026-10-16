using System.ComponentModel;
using System.Reflection;

namespace Core.ViewModels
{
    public class ApplicationUserViewModel
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? FullName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public DateTime? DateRegistered { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsVerified { get; set; }
		public string? AccountNumber { get; set; }
		public string? AccountName { get; set; }

	}
}