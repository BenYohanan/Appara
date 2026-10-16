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
using System.Text.Json;

namespace Apparcus.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IUserHelper _userHelper;
        private readonly IProjectHelper _projectHelper;
        private readonly IProjectFeeHelper _projectFeeHelper;
        private readonly IDropdownHelper _dropdownHelper;
        private readonly ISafeHavenHelper _safeHavenHelper;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public ProjectController(IUserHelper userHelper, IProjectHelper projectHelper, IDropdownHelper dropdownHelper, UserManager<ApplicationUser> userManager, AppDbContext context, ISafeHavenHelper safeHavenHelper, IProjectFeeHelper projectFeeHelper)
        {
            _projectFeeHelper = projectFeeHelper;
            _userHelper = userHelper;
            _projectHelper = projectHelper;
            _dropdownHelper = dropdownHelper;
            _userManager = userManager;
            _context = context;
            _safeHavenHelper = safeHavenHelper;
        }

        /// <summary>
        /// The project, if the signed-in user is allowed to look at it.
        ///
        /// Every page below takes a project id straight off the query string,
        /// and an id is a small number anyone can type. Ordinary users see only
        /// what they created; staff, who already list every project, still see
        /// everything.
        /// </summary>
        private Project? VisibleProject(int projectId)
        {
            if (projectId <= 0) return null;

            var project = _context.Projects
                .FirstOrDefault(p => p.Id == projectId && !p.Deleted);

            if (project == null) return null;

            var current = Utility.GetCurrentUser();

            var isStaff = current.UserRole == Constants.AdminRole
                || current.UserRole == Constants.SuperAdminRole;

            return isStaff || project.CreatedById == current.Id ? project : null;
        }

        public async Task<IActionResult> Index(IPageListModel<ProjectViewModel> viewModel, int page = 1)
        {
            var userId = Utility.GetCurrentUser().Id;
            var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == userId);
            ViewBag.Layout = _userHelper.GetRoleLayout();
            ViewBag.UsersDropdown = await _dropdownHelper.GetAllUsersDropdownAsync();
            ViewBag.KYCVerified = user?.KycVerified;
            viewModel.Model = _projectHelper.Projects(viewModel, page, type: ProjectType.A);
			viewModel.UserId = userId;
            viewModel.SearchAction = "Index";
            viewModel.SearchController = "Project";
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string projectDetails)
        {
            if (string.IsNullOrEmpty(projectDetails))
            {
                return ResponseHelper.JsonError("Invalid project details");
            }
            var userId = Utility.GetCurrentUser().Id;
            var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == userId);

            if (!user.KycVerified)
                return ResponseHelper.JsonError("You must complete KYC before creating a project.");

            var project = JsonSerializer.Deserialize<ProjectViewModel>(projectDetails);
            if (project == null)
            {
                return ResponseHelper.JsonError("Project details could not be processed");
            }
            var existingProject = _context.Projects
                .FirstOrDefault(p => p.Title == project.Name && p.CreatedById == userId && !p.Deleted);
            if (existingProject != null)
            {
                return ResponseHelper.JsonError("Project with name exists");
            }

            var created = await _projectHelper.CreateProject(project, user);
            if (created != null)
            {
                var fee = await _projectFeeHelper.ChargeNewProjectAsync(created, user);

                // The id goes back so the form can follow up with the access
                // code, which is never part of the project payload itself.
                return ResponseHelper.JsonSuccessWithObject("Project created successfully", new
                {
                    projectId = created.Id,
                    feeNote = fee.Note,
                    feeOwed = created.AccountFeeStatus == ProjectFeeStatus.Owed,
                });
            }
            return ResponseHelper.JsonError("Failed to create project");
        }

        /// <summary>
        /// What opening one more project would cost, so the add form can say
        /// so before it is submitted.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AccountFee()
        {
            return Json(await _projectFeeHelper.QuoteAsync(Utility.GetCurrentUser().Id));
        }

        [HttpGet]
        public IActionResult GetProjectById(int id)
        {
            if (VisibleProject(id) == null) return Json(null);

            var request = AppHttpContext.Current.Request;
            string baseUrl = $"{request.Scheme}://{request.Host}";
            var project = _context.Projects
                .Where(x => x.Id == id && !x.Deleted)
                .Select(x => new ProjectViewModel
                {
                    Id = x.Id,
                    Name = x.Title,
                    Description = x.Description,
                    AmountNeeded = x.AmountNeeded,
                    ShowForAudit = x.ShowForAudit,
                    IsPublic = x.IsPublic,
                    HasAccessCode = x.AccessCode != null,
                    SupportLink = $"{baseUrl}/Guest/View/{x.Id}",
                    CustomFields = x.CustomFields.Select(f => new ProjectCustomFieldVM
                    {
                        Id = f.Id,
                        FieldName = f.FieldName,
                        FieldType = f.FieldType,
                        IsRequired = f.IsRequired
                    }).ToList()
                })
                .FirstOrDefault();

            if (project == null)
                return Json(null);

            // The QR code is drawn through System.Drawing, which throws
            // PlatformNotSupportedException off Windows. It is a nicety on the
            // edit modal, so a failure to draw it must not take the whole
            // project payload down with it.
            try
            {
                var qrBytes = ProjectHelper.GenerateQRCode(project.SupportLink ?? "");
                if (qrBytes != null)
                {
                    project.QRCodeBase64 = Convert.ToBase64String(qrBytes);
                }
            }
            catch
            {
                project.QRCodeBase64 = null;
            }

            return Json(project);
        }


        /// <summary>
        /// Sets the 4-digit code contributors must enter to open the public
        /// page, or clears it when passed nothing. The code is only ever
        /// written here — never through the general project edit payload, so a
        /// stray field on a project form cannot change or reveal it.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SetAccessCode(int id, string? code)
        {
            var trimmed = code?.Trim();

            if (!string.IsNullOrEmpty(trimmed) && (trimmed.Length != 4 || !trimmed.All(char.IsDigit)))
                return ResponseHelper.JsonError("The code must be exactly 4 digits.");

            var userId = Utility.GetCurrentUser().Id;
            var saved = await _projectHelper.SetAccessCode(id, userId, trimmed);

            if (!saved)
                return ResponseHelper.JsonError("We could not find that project under your account.");

            return ResponseHelper.JsonSuccess(string.IsNullOrEmpty(trimmed)
                ? "Access code removed. Anyone who finds this project can now open it."
                : "Access code saved. Share it with your contributors.");
        }

        [HttpGet]
        public IActionResult Supporters(int projectId, IPageListModel<SupporterViewModel> viewModel, int page = 1)
        {
            var project = VisibleProject(projectId);
            if (project == null) return NotFound();

            ViewBag.Layout = _userHelper.GetRoleLayout();
            ViewData["projectId"] = projectId;
            viewModel.SearchAction = "Supporters";
            viewModel.SearchController = "Project";
            viewModel.CanFilterByDateRange = true;

            var supporters = _projectHelper.GetProjectSupportersPaged(projectId, viewModel, page);
            viewModel.Model = supporters;

            ViewBag.ProjectTitle = project.Title ?? "Project";

            // A Type-B project keeps records of money already paid elsewhere, so
            // the page has to say "records", link back to the Type-B list, and
            // drop the columns that only mean something while a project is still
            // collecting.
            ViewBag.IsTypeB = project.Type == ProjectType.B;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Payments(int projectId, IPageListModel<PaymentDTO> viewModel, int page = 1)
        {
            if (VisibleProject(projectId) == null) return NotFound();

            var userId = Utility.GetCurrentUser().Id;
            var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == userId);
            if (user == null) return NotFound();
            ViewBag.TransactionPin = user?.TransactionPinSet ?? false;

            ViewBag.Layout = _userHelper.GetRoleLayout();
            ViewBag.Banks = await _safeHavenHelper.GetBanksAsync();
            var project = _context.Projects
                .Include(x => x.Wallets)
                .FirstOrDefault(p => p.Id == projectId && !p.Deleted);

            if (project == null) return NotFound();

            ViewBag.TotalCollected = project.AmountObtained;
            ViewBag.Balance = project.Wallets?.Sum(x => x.Balance);
            ViewBag.ProjectName = project.Title ?? "Project Payments";
            ViewBag.ProjectId = projectId;

			ViewBag.FreeWithdrawalsRemaining = project.Wallets?.FirstOrDefault()?.FreeWithdrawalsRemaining;

			viewModel.SearchAction = "Payments";
            viewModel.SearchController = "Project";
            viewModel.CanFilterByDateRange = true;

            var payments = _projectHelper.GetPaymentsPaged(projectId, viewModel, page);
            viewModel.Model = payments;

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(string projectDetails)
        {
            if (string.IsNullOrEmpty(projectDetails))
                return ResponseHelper.JsonError("Invalid project details");

            var project = JsonSerializer.Deserialize<ProjectViewModel>(projectDetails);
            if (project == null)
                return ResponseHelper.JsonError("Project data could not be processed");

            var existing = VisibleProject(project.Id);
            if (existing == null)
                return ResponseHelper.JsonError("We could not find that project under your account.");

            // A Type-B project collects nothing, so it never needed KYC to
            // exist and must not need it to be edited either — CreateTypeB
            // does not ask for it.
            if (existing.Type == ProjectType.A)
            {
                var userId = Utility.GetCurrentUser().Id;
                var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null || !user.KycVerified)
                    return ResponseHelper.JsonError("You must complete KYC before editing a project.");
            }

            var updated = await _projectHelper.UpdateProject(project);
            if (updated)
                return ResponseHelper.JsonSuccess("Project updated successfully");

            return ResponseHelper.JsonError("Failed to update project");
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var project = VisibleProject(id);

            if (project == null)
                return ResponseHelper.JsonError("Project already deleted");

            var wallet = _context.Wallets
                .FirstOrDefault(w => w.ProjectId == id);

            if (wallet != null)
            {
                wallet.Balance = 0m;
            }

            project.Status = ProjectStatus.Deleted;
            project.Deleted = true;

            _context.SaveChanges();

            return ResponseHelper.JsonSuccess("Project deleted successfully");
        }

		[HttpPost]
		public IActionResult UpdateStatus(int id, ProjectStatus status)
		{
			var project = VisibleProject(id);

			if (project == null)
				return ResponseHelper.JsonError("Project not found");

			project.Status = status;

            if (status == ProjectStatus.Disabled)
            {
				var wallet = _context.Wallets.FirstOrDefault(w => w.ProjectId == id);

				if (wallet != null)
				{
					wallet.Balance = 0m;
				}
			}
			_context.SaveChanges();

			return ResponseHelper.JsonSuccess($"Project updated successfully");
		}

		public async Task<IActionResult> WithDrawalHistory(IPageListModel<WithdrawalViewModel> viewModel, int projectId, int page = 1)
        {
            if (VisibleProject(projectId) == null) return NotFound();

            ViewBag.Layout = _userHelper.GetRoleLayout();
            viewModel.Model = _projectHelper.WithdrawalHistory(viewModel, projectId, page); ;
            viewModel.SearchAction = "WithDrawalHistory";
            viewModel.SearchController = "Project";
            return View(viewModel);
        }


        /// <summary>
        /// Type-B projects: records of payments made elsewhere, kept so a paper
        /// sheet does not have to be. Listed apart from Type-A because nothing
        /// here is collecting.
        /// </summary>
        [HttpGet]
        public IActionResult TypeB(IPageListModel<ProjectViewModel> viewModel, int page = 1)
        {
            var userId = Utility.GetCurrentUser().Id;
            ViewBag.Layout = _userHelper.GetRoleLayout();
            ViewBag.RecordQuota = _projectHelper.GetRecordQuota(userId);

            viewModel.Model = _projectHelper.Projects(viewModel, page, type: ProjectType.B);
            viewModel.UserId = userId;
            viewModel.SearchAction = "TypeB";
            viewModel.SearchController = "Project";

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTypeB(string projectDetails)
        {
            if (string.IsNullOrEmpty(projectDetails))
                return ResponseHelper.JsonError("Invalid project details");

            var userId = Utility.GetCurrentUser().Id;
            var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
                return ResponseHelper.JsonError("User not found");

            var project = JsonSerializer.Deserialize<ProjectViewModel>(projectDetails);
            if (project == null)
                return ResponseHelper.JsonError("Project details could not be processed");

            var existing = _context.Projects
                .FirstOrDefault(p => p.Title == project.Name && p.CreatedById == userId && !p.Deleted);
            if (existing != null)
                return ResponseHelper.JsonError("Project with name exists");

            var created = await _projectHelper.CreateTypeBProject(project, user);
            if (created == null)
                return ResponseHelper.JsonError("Failed to create project");

            return ResponseHelper.JsonSuccessWithObject("Project created successfully", new { projectId = created.Id });
        }

        [HttpPost]
        public async Task<IActionResult> AddRecord(string recordDetails)
        {
            if (string.IsNullOrEmpty(recordDetails))
                return ResponseHelper.JsonError("Invalid record details");

            var record = JsonSerializer.Deserialize<ProjectRecordViewModel>(recordDetails);
            if (record == null)
                return ResponseHelper.JsonError("Record details could not be processed");

            var userId = Utility.GetCurrentUser().Id;
            var (ok, error) = await _projectHelper.AddRecord(record.ProjectId, userId, record);

            if (!ok)
                return ResponseHelper.JsonError(error ?? "Failed to add record");

            // Sent back so the form can show what is left without a round trip.
            return ResponseHelper.JsonSuccessWithObject("Record added", _projectHelper.GetRecordQuota(userId));
        }

        [HttpGet]
        public IActionResult DisabledProjects(IPageListModel<ProjectViewModel> viewModel, int page = 1)
        {
            var userId = Utility.GetCurrentUser().Id;
            ViewBag.Layout = _userHelper.GetRoleLayout();
            var projects = _projectHelper.Projects(viewModel, page, disabledOnly: true);
            viewModel.Model = projects;
            viewModel.UserId = userId;
            viewModel.SearchAction = "DisabledProjects";
            viewModel.SearchController = "Project";
            return View(viewModel);
        }
    }
}