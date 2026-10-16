using Core.Models;
using Core.ViewModels;
using X.PagedList;

namespace Logic.IHelpers
{
    public interface IUserHelper
    {
        IPagedList<ApplicationUserViewModel> Users(IPageListModel<ApplicationUserViewModel> model, int page);
        Task<ApplicationUser?> FindByEmailAsync(string email);
        Task<ApplicationUser?> FindByUserNameAsync(string userName);
        string GetRoleLayout();
        IQueryable<ApplicationUserViewModel> GetUsers();
        string GetValidatedUrl(List<string> roles);
        Task<ApplicationUser?> RegisterUser(ApplicationUserViewModel applicationUserViewModel);
        string GetCurrentUserId();
        Task<ApplicationUser?> CreateUserFromAdmin(ApplicationUserViewModel model);
		Task<ApplicationUser?> FindByUserIdAsync(string userId);
		Task<decimal> GetTotalProjectWalletBalanceAsync(string userId);
	}
}
