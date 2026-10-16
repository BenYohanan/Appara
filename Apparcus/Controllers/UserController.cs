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
using System.Text.Json;
using X.PagedList.Extensions;

namespace Apparcus.Controllers
{
	/// <summary>
	/// Signed in throughout. The controller carried no authorize rule at all,
	/// so the dashboard, the profile, the personal account and the Beads
	/// history were all reachable without an account — as was CreateFromAdmin,
	/// which creates one.
	/// </summary>
	[Authorize]
	public class UserController : Controller
	{
		private readonly IUserHelper _userHelper;
		private readonly IProjectHelper _projectHelper;
		private readonly ISafeHavenHelper _safeHavenHelper;
		private readonly AppDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;

		public UserController(IUserHelper userHelper, IProjectHelper projectHelper, AppDbContext context, UserManager<ApplicationUser> userManager, ISafeHavenHelper safeHavenHelper)
		{
			_userHelper = userHelper;
			_projectHelper = projectHelper;
			_context = context;
			_userManager = userManager;
			_safeHavenHelper = safeHavenHelper;
		}

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			ViewBag.Layout = _userHelper.GetRoleLayout();
			ViewBag.Banks = await _safeHavenHelper.GetBanksAsync();
			var projects = _projectHelper.GetAllProjects();
			var contributors = _projectHelper.GetContributors();
			var userProjectIds = projects.Select(p => p.Id).ToHashSet();
			var contributorsCount = contributors.Count(c => userProjectIds.Contains(c.ProjectId));
			var userId = Utility.GetCurrentUser().Id;
			var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == userId);
			var userVm = new UserDashboardDto
			{
				FullName = $"{user.FirstName} {user.LastName}".Trim(),
				Email = user.Email,
				PhoneNumber = user.PhoneNumber,
				ProjectCount = projects.ToList().Count,
				Projects = [.. projects],
				WalletBalance = await _userHelper.GetTotalProjectWalletBalanceAsync(user.Id),
				ContibutorsCount = contributorsCount,
				KYCVerified = user.KycVerified,
				UserId = user.Id,
				BeadsBalance = user.BeadsBalance,
				PersonalBalance = user.PersonalBalance,
				AccountNumber = user.AccountNumber,
				AccountName = user.HoldersName,
				PersonalAccountNumber = user.PersonalAccountNumber,
				PersonalAccountName = user.PersonalHoldersName,
			};
			return View(userVm);
		}

		[HttpPost]
		[Authorize(Roles = SeedItems.AdminRole + "," + SeedItems.SuperAdminRole)]
		public IActionResult CreateFromAdmin(string userDetails)
		{
			if (string.IsNullOrEmpty(userDetails))
				return Json(new { success = false, message = "Invalid user details" });

			var model = JsonSerializer.Deserialize<ApplicationUserViewModel>(userDetails);
			if (model == null)
				return Json(new { success = false, message = "User details could not be processed" });

			var existingUser = _userHelper.FindByEmailAsync(model.Email).Result;
			if (existingUser != null)
				return Json(new { success = false, message = "Email already exists" });

			var createdUser = _userHelper.CreateUserFromAdmin(model).Result;
			if (createdUser != null)
				return Json(new { success = true, message = "User created successfully" });

			return Json(new { success = false, message = "Failed to create user" });
		}

		[HttpGet]
		public async Task<IActionResult> Profile()
		{
			var user = await _userManager.GetUserAsync(User);
			if (user == null)
				return RedirectToAction("Login", "Account");
			ViewBag.Layout = _userHelper.GetRoleLayout();
			return View(user);
		}

		[HttpGet]
		public async Task<IActionResult> PersonalAccount(IPageListModel<PersonalTransactionViewModel> viewModel, int page = 1)
		{
			ViewBag.Layout = _userHelper.GetRoleLayout();

			var userId = Utility.GetCurrentUser().Id;
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
				return NotFound();

			ViewBag.PersonalBalance = user.PersonalBalance.ToString("N2");
			ViewBag.BeadsBalance = user.BeadsBalance.ToString("N0");
			ViewBag.PersonalAccountNumber = user.PersonalAccountNumber ?? "Not set up";

			var query = _context.PersonalTransactions
				.Where(t => t.UserId == userId)
				.OrderByDescending(t => t.DateCreated)
				.Select(t => new PersonalTransactionViewModel
				{
					Id = t.Id,
					Type = t.Type.ToString(),
					Amount = t.Amount,
					BalanceAfter = t.BalanceAfter,
					Narration = t.Narration,
					Reference = t.Reference,
					DateCreated = t.DateCreated
				});

			var paged = query.ToPagedList(page, 20);

			// Set on the rows this page already fetched. A project payout and
			// money received from another Apparcus user both add to the
			// balance, and both used to be drawn as money going out.
			foreach (var item in paged)
				item.Direction = PersonalTransactionTypes.DirectionOf(item.Type);

			viewModel.Model = paged;
			viewModel.SearchAction = "PersonalAccount";
			viewModel.SearchController = "User";

			return View(viewModel);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Profile(IFormFile file, string FirstName, string LastName, string PhoneNumber)
		{
			var user = await _userManager.GetUserAsync(User);
			if (user == null)
				return RedirectToAction("Login", "Account");

			user.FirstName = FirstName?.Trim();
			user.LastName = LastName?.Trim();
			user.PhoneNumber = PhoneNumber?.Trim();

			if (file != null && file.Length > 0)
			{
				var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
				Directory.CreateDirectory(uploadsFolder);

				var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
				var filePath = Path.Combine(uploadsFolder, uniqueFileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
				{
					await file.CopyToAsync(stream);
				}

				user.ProfilePictureUrl = "/uploads/avatars/" + uniqueFileName + "?v=" + DateTime.Now.Ticks;
			}

			var result = await _userManager.UpdateAsync(user);

			if (result.Succeeded)
			{
				TempData["SuccessMessage"] = "Profile updated successfully!";
			}
			else
			{
				foreach (var error in result.Errors)
					ModelState.AddModelError("", error.Description);
			}

			return RedirectToAction("Profile");
		}

		[HttpGet]
		public async Task<IActionResult> BeadsHistory(IPageListModel<BeadTransactionViewModel> viewModel, int page = 1)
		{
			ViewBag.Layout = _userHelper.GetRoleLayout();

			var userId = Utility.GetCurrentUser().Id;
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == userId);

			if (user == null)
				return NotFound();

			ViewBag.BeadsBalance = user.BeadsBalance.ToString("N0");

			var query = _context.BeadTransactions
				.Include(t => t.Project)
				.Where(t => t.UserId == userId)
				.OrderByDescending(t => t.DateCreated)
				.Select(t => new BeadTransactionViewModel
				{
					Id = t.Id,
					BeadsChange = t.BeadsChange,
					Reason = t.Reason,
					ProjectTitle = t.Project != null ? t.Project.Title : "",
					DateCreated = t.DateCreated
				});

			viewModel.Model = query.ToPagedList(page, 20);
			viewModel.SearchAction = "BeadsHistory";
			viewModel.SearchController = "User";

			return View(viewModel);
		}
	}
}