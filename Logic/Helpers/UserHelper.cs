using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.IHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using X.PagedList;
using X.PagedList.Extensions;

namespace Logic.Helpers
{
	public class UserHelper(AppDbContext db, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor) : IUserHelper
	{
		private readonly AppDbContext db = db;
		private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?
                .FindFirstValue(ClaimTypes.NameIdentifier);
        }
        public async Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            return await db.ApplicationUsers
                .Where(s => s.Email == email && !s.Deleted)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }
        public async Task<ApplicationUser?> FindByUserNameAsync(string userName)
        {
            return await db.ApplicationUsers
                .Where(s => s.UserName == userName && !s.Deleted)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }
		public async Task<ApplicationUser?> FindByUserIdAsync(string userId)
		{
			return await db.ApplicationUsers
				.Where(s => s.Id == userId && !s.Deleted)
				.FirstOrDefaultAsync().ConfigureAwait(false);
		}
		public string GetValidatedUrl(List<string> roles)
        {
            var roleUrlMap = new Dictionary<string, string>
            {
                { SeedItems.SuperAdminRole, SeedItems.SuperAdminDashboard },
                { SeedItems.AdminRole, SeedItems.AdminDashboard },
                { SeedItems.UserRole, SeedItems.UserDashboard }
            };

            foreach (var role in roles)
            {
                if (roleUrlMap.TryGetValue(role, out var url))
                {
                    return url;
                }
            }

            return "/Account/Login";

        }
        public async Task<ApplicationUser?> RegisterUser(ApplicationUserViewModel applicationUserViewModel)
        {
            var user = new ApplicationUser
            {
                FirstName = applicationUserViewModel.FirstName,
                LastName = applicationUserViewModel.LastName,
                Email = applicationUserViewModel.Email,
                PhoneNumber = applicationUserViewModel.PhoneNumber,
                UserName = applicationUserViewModel.Email,
                DateOfBirth = applicationUserViewModel.DateOfBirth
            };

            var addedUser = await _userManager.CreateAsync(user, applicationUserViewModel.Password).ConfigureAwait(false);
            if (addedUser.Succeeded)
            {
                var addedUserToRole = await _userManager.AddToRoleAsync(user, SeedItems.UserRole).ConfigureAwait(false);
                if (addedUserToRole.Succeeded)
                {
                    return user;
                }
            }
            return null;
        }
        public IPagedList<ApplicationUserViewModel> Users(IPageListModel<ApplicationUserViewModel> model, int page)
        {
            try
            {
                var query = GetUsers();

                if (!string.IsNullOrEmpty(model.Keyword))
                {
                    var key = model.Keyword.ToLower();

                    query = query.Where(x =>
                        x.FirstName.ToLower().Contains(key) ||
                        x.LastName.ToLower().Contains(key) ||
                        x.Email.ToLower().Contains(key) ||
                        x.PhoneNumber.ToLower().Contains(key) ||
                        x.FullName.ToLower().Contains(key));
                }

                if (model.StartDate.HasValue)
                {
                    query = query.Where(x => x.DateRegistered >= model.StartDate);
                }
                if (model.EndDate.HasValue)
                {
                    query = query.Where(x => x.DateOfBirth <= model.EndDate);
                }
               
                var logs = query
                    .OrderByDescending(x => x.DateRegistered)
                    .Select(r => new ApplicationUserViewModel
                    {
                        Id = r.Id,
                        FirstName = r.FirstName,
                        LastName = r.LastName,
                        Email = r.Email,
                        FullName = $"{r.FirstName} {r.LastName}",
                        PhoneNumber = r.PhoneNumber,
                        DateRegistered = r.DateRegistered,
                        DateOfBirth = r.DateOfBirth,
                        IsAdmin = r.IsAdmin,
                        IsVerified = r.IsVerified,
                        AccountName = r.AccountName,
                        AccountNumber = r.AccountNumber
					}).ToPagedList(page, 25);
                model.CanFilterByDeliveryStatus = true;

                return logs;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public IQueryable<ApplicationUserViewModel> GetUsers()
        {
            var adminRoleId = db.Roles
                .Where(r => r.Name == SeedItems.AdminRole)
                .Select(r => r.Id)
                .FirstOrDefault();

            var userRoleId = db.Roles
                .Where(r => r.Name == SeedItems.UserRole)
                .Select(r => r.Id)
                .FirstOrDefault();

            var query =
                from u in db.ApplicationUsers
                where !u.Deleted
                let isAdmin = db.UserRoles
                    .Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId)
                let isUser = db.UserRoles
                    .Any(ur => ur.UserId == u.Id && ur.RoleId == userRoleId)
                where isAdmin || isUser
                select new ApplicationUserViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    FullName = u.FirstName + " " + u.LastName,
                    PhoneNumber = u.PhoneNumber,
                    DateRegistered = u.DateCreated,
                    DateOfBirth = u.DateOfBirth,
                    IsAdmin = isAdmin,
                    IsVerified = u.KycVerified != null && u.KycVerified,
					AccountName = u.HoldersName,
					AccountNumber = u.AccountNumber
				};

            return query;
        }

        public string GetRoleLayout()
        {
            var user = Utility.GetCurrentUser();
            if (user == null)
            {
                return Constants.DefaultLayout;
            }
            return Constants.GeneralLayout;
        }

        public async Task<ApplicationUser?> CreateUserFromAdmin(ApplicationUserViewModel model)
        {
            var user = new ApplicationUser
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                UserName = model.Email,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded) return null;

            var role = model.IsAdmin ? SeedItems.AdminRole : SeedItems.UserRole;
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            return roleResult.Succeeded ? user : null;
        }

		public async Task<decimal> GetTotalProjectWalletBalanceAsync(string userId)
		{
			return await db.Wallets
				.Where(w => w.ProjectOwnerId == userId)
				.SumAsync(w => w.Balance);
		}

	}
}