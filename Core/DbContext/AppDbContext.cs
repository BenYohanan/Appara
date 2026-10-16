using Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Core.DbContext
{
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<DropDown> DropDowns { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectSupporter> ProjectSupporters { get; set; }
        public DbSet<ProjectComment> ProjectComments { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Contribution> Contributions { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Withdrawal> Withdrawals { get; set; }
        public DbSet<ProjectCustomField> ProjectCustomFields { get; set; }
        public DbSet<ProjectCustomFieldValue> ProjectCustomFieldValues { get; set; }
		public DbSet<Notification> Notifications { get; set; }
		public DbSet<BeadTransaction> BeadTransactions { get; set; }
		public DbSet<PersonalTransaction> PersonalTransactions { get; set; }
		public DbSet<BiometricDevice> BiometricDevices { get; set; }
		public DbSet<UserSession> UserSessions { get; set; }
		public DbSet<DeviceVerification> DeviceVerifications { get; set; }
	}
}
