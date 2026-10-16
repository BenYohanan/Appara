using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic;
using Logic.Helpers;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apparcus.Controllers
{
    /// <summary>
    /// Staff only, by role and not merely by being signed in.
    ///
    /// [Authorize] on its own let any signed-in account reach every action
    /// here — which includes setting another user's password, promoting an
    /// account to admin, and deleting users. Being logged in is not the bar
    /// for any of that.
    /// </summary>
    [Authorize(Roles = SeedItems.AdminRole + "," + SeedItems.SuperAdminRole)]
    public class AdminController(IUserHelper userHelper, AppDbContext appDbContext, IEmailTemplateService emailTemplateService, UserManager<ApplicationUser> userManager, IProjectHelper projectHelper, SafeHavenAccountConsolidationService safeHavenAccountConsolidationService) : Controller
    {
        private readonly IProjectHelper _projectHelper = projectHelper;
        private readonly IUserHelper _userHelper = userHelper;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly AppDbContext _context = appDbContext;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
		private readonly SafeHavenAccountConsolidationService _safeHavenAccountConsolidationService = safeHavenAccountConsolidationService;
		public async Task<IActionResult> Index()
        {
            var user = Utility.GetCurrentUser();
            var isVerified = _context.ApplicationUsers.FirstOrDefault(x => x.Id == user.Id)?.KycVerified ?? false;
            ViewBag.Layout = _userHelper.GetRoleLayout();
            var projects = _projectHelper.GetAllProjects();
            var data = new AdminDashboardDto
            {
                UserName = user.FullName,
                ProjectCount = projects.ToList().Count,
                ClientCount = _userHelper.GetUsers().ToList().Count,
                Projects = [.. projects],
                TotalEarnings = _context.Contributions.Sum(x => x.Amount),
                ContibutorsCount = _projectHelper.GetContributors().Count,
                KYCVerified = isVerified,
				UserId = user.Id
			};
            return View(data);
        }

		public async Task<IActionResult> Migrate()
		{
			await _safeHavenAccountConsolidationService.RunAsync(dryRun: false);
			return RedirectToAction("Index", "Admin");
		}

		public IActionResult Users(IPageListModel<ApplicationUserViewModel> model, int page = 1)
        {
            ViewBag.Layout = _userHelper.GetRoleLayout();
            var users = _userHelper.Users(model, page);
            model.Model = users;
            model.SearchAction = "Users";
            model.SearchController = "Admin";
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Json(new { isError = true, msg = "User not found." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
                return ResponseHelper.JsonSuccess("Password changed successfully.");

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return ResponseHelper.JsonError("Failed to change password");
        }

        [HttpPost]
        public async Task<JsonResult> MakeUserAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ResponseHelper.JsonError("User not found");

            var isAlreadyAdmin = await _userManager.IsInRoleAsync(user, SeedItems.AdminRole);
            if (isAlreadyAdmin)
                return ResponseHelper.JsonError("User is already an admin");

            var result = await _userManager.AddToRoleAsync(user, SeedItems.AdminRole);
            if (result.Succeeded)
                return ResponseHelper.JsonSuccess("User promoted to admin successfully");

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return ResponseHelper.JsonError($"Failed to promote user: {errors}");
        }

        [HttpGet]
        public async Task<IActionResult> ProjectSupporters(int id)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectSupporters)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return NotFound();

            ViewBag.ProjectTitle = project.Title;
            return View(project);
        }
        [HttpPost]
        public async Task<JsonResult> RemoveUserAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ResponseHelper.JsonError("User not found");

            var isAdmin = await _userManager.IsInRoleAsync(user, SeedItems.AdminRole);
            if (!isAdmin)
                return ResponseHelper.JsonError("User is not an admin");

            var result = await _userManager.RemoveFromRoleAsync(user, SeedItems.AdminRole);
            if (result.Succeeded)
                return ResponseHelper.JsonSuccess("User removed from admin role successfully");

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return ResponseHelper.JsonError($"Failed to remove admin role: {errors}");
        }
        [HttpPost]
        public JsonResult Delete(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return ResponseHelper.JsonError("An error has occurred, try again. Please contact support if the error persists.");

            var removeUser = _context.ApplicationUsers
                .FirstOrDefault(u => u.Id == userId && !u.Deleted);

            if (removeUser == null)
                return ResponseHelper.JsonError("User not found.");

            removeUser.Deleted = true;

            _context.SaveChanges();
            return ResponseHelper.JsonSuccess("Deleted!.");
        }

    }
}
