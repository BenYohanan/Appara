using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.Helpers;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apparcus.Areas.Api.Controllers
{
	[Route("api/project")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	public class ProjectsController : ControllerBase
	{
		private readonly IProjectHelper _projectHelper;
		private readonly IProjectFeeHelper _projectFeeHelper;
		private readonly ISafeHavenHelper _safeHavenHelper;
		private readonly AppDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;

		public ProjectsController(
			IProjectHelper projectHelper,
			IProjectFeeHelper projectFeeHelper,
			ISafeHavenHelper safeHavenHelper,
			AppDbContext context,
			UserManager<ApplicationUser> userManager)
		{
			_projectHelper = projectHelper;
			_projectFeeHelper = projectFeeHelper;
			_safeHavenHelper = safeHavenHelper;
			_context = context;
			_userManager = userManager;
		}

		private string CurrentUserId =>
			User.FindFirstValue(ClaimTypes.NameIdentifier)
			?? throw new UnauthorizedAccessException();

		/// <summary>
		/// The project, if it belongs to the caller. A project id is a small
		/// number, so every read below has to ask rather than assume — the
		/// contributor list alone carries names and phone numbers.
		/// </summary>
		private async Task<Project?> OwnedProjectAsync(int id) =>
			await _context.Projects
				.FirstOrDefaultAsync(p => p.Id == id
					&& !p.Deleted
					&& p.CreatedById == CurrentUserId);

		/// <summary>
		/// [type] is "A" or "B". Omitted means every kind, which is what the
		/// All/Active/Closed tabs used to get and still do.
		/// </summary>
		[HttpGet]
		public IActionResult Index(
			[FromQuery] string? keyword,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to,
			[FromQuery] string? type,
			[FromQuery] int page = 1)
		{
			var model = new IPageListModel<ProjectViewModel>
			{
				Keyword = keyword,
				StartDate = from,
				EndDate = to,
			};

			ProjectType? projectType = Enum.TryParse<ProjectType>(type, true, out var parsed)
				? parsed
				: null;

			var paged = _projectHelper.Projects(model, page, createdById: CurrentUserId, type: projectType);
			return Ok(PagedApiResponse<ProjectViewModel>.From(paged));
		}

		/// <summary>
		/// Creates a Type-B project — a record of payments made elsewhere. Kept
		/// apart from Create because it takes no money and so needs no KYC or
		/// bank sub-account.
		/// </summary>
		[HttpPost("type-b")]
		public async Task<IActionResult> CreateTypeB([FromBody] ProjectViewModel model)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return Unauthorized();

			var existing = await _context.Projects
				.AnyAsync(p => p.Title == model.Name && p.CreatedById == user.Id && !p.Deleted);
			if (existing)
				return ResponseHelper.JsonError("Project with name exists");

			var created = await _projectHelper.CreateTypeBProject(model, user);
			if (created == null)
				return ResponseHelper.JsonError("Failed to create project");

			return ResponseHelper.JsonSuccessWithObject("Project created successfully", new { projectId = created.Id });
		}

		/// <summary>
		/// Adds one record to a Type-B project. Answers with what is left of
		/// the user's quota so the Add Record screen can keep a running count
		/// while they work through a sheet.
		/// </summary>
		[HttpPost("{id:int}/records")]
		public async Task<IActionResult> AddRecord(int id, [FromBody] ProjectRecordViewModel model)
		{
			if (model == null)
				return ResponseHelper.JsonError("Record details could not be processed");

			model.ProjectId = id;
			var (ok, error) = await _projectHelper.AddRecord(id, CurrentUserId, model);

			if (!ok)
				return ResponseHelper.JsonError(error ?? "Failed to add record");

			return ResponseHelper.JsonSuccessWithObject("Record added", _projectHelper.GetRecordQuota(CurrentUserId));
		}

		/// <summary>
		/// Corrects a record on a Type-B project. The body is the record as it
		/// should now read, custom-field answers included.
		/// </summary>
		[HttpPut("{id:int}/records/{recordId:int}")]
		public async Task<IActionResult> UpdateRecord(int id, int recordId, [FromBody] ProjectRecordViewModel model)
		{
			if (model == null)
				return ResponseHelper.JsonError("Record details could not be processed");

			var (ok, error) = await _projectHelper.UpdateRecord(id, recordId, CurrentUserId, model);

			if (!ok)
				return ResponseHelper.JsonError(error ?? "Failed to update record");

			return ResponseHelper.JsonSuccess("Record updated");
		}

		[HttpGet("{id:int}")]
		public async Task<IActionResult> GetById(int id)
		{
			if (await OwnedProjectAsync(id) == null) return NotFound();

			var project = _projectHelper.GetProjectById(id);
			if (project == null) return NotFound();
			return Ok(project);
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] ProjectViewModel model)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return Unauthorized();

			if (!user.KycVerified)
				return ResponseHelper.JsonError("You must complete KYC before creating a project.");

			var existingProject = _context.Projects
				.FirstOrDefault(p => p.Title == model.Name && p.CreatedById == user.Id && !p.Deleted);
			if (existingProject != null)
				return ResponseHelper.JsonError("Project with name exists");

			var created = await _projectHelper.CreateProject(model, user);
			if (created == null)
				return ResponseHelper.JsonError("Failed to create project");

			var fee = await _projectFeeHelper.ChargeNewProjectAsync(created, user);

			return ResponseHelper.JsonSuccessWithObject("Project created successfully", new
			{
				projectId = created.Id,
				feeNote = fee.Note,
				feeOwed = created.AccountFeeStatus == ProjectFeeStatus.Owed,
			});
		}

		/// <summary>
		/// What opening one more project would cost, and what it would be paid
		/// from, so the create form can say so before the user submits.
		/// </summary>
		[HttpGet("account-fee")]
		public async Task<IActionResult> AccountFee()
		{
			return Ok(await _projectFeeHelper.QuoteAsync(CurrentUserId));
		}

		[HttpPut("{id:int}")]
		public async Task<IActionResult> Update(int id, [FromBody] ProjectViewModel model)
		{
			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return Unauthorized();

			var owned = await OwnedProjectAsync(id);
			if (owned == null)
				return Forbid();

			// Type-B collects nothing, so it never needed KYC to exist and must
			// not need it to be edited either.
			if (owned.Type == ProjectType.A && !user.KycVerified)
				return ResponseHelper.JsonError("You must complete KYC before editing a project.");

			model.Id = id;
			var updated = await _projectHelper.UpdateProject(model);
			if (!updated)
				return ResponseHelper.JsonError("Failed to update project");

			return ResponseHelper.JsonSuccess("Project updated successfully");
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id)
		{
			var project = await _context.Projects
				.FirstOrDefaultAsync(p => p.Id == id && !p.Deleted);
			if (project == null)
				return ResponseHelper.JsonError("Project already deleted");

			if (project.CreatedById != CurrentUserId)
				return Forbid();

			var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.ProjectId == id);
			if (wallet != null)
				wallet.Balance = 0m;

			project.Status = ProjectStatus.Deleted;
			project.Deleted = true;

			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("Project deleted successfully");
		}

		[HttpPatch("{id:int}/status")]
		public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateProjectStatusRequest request)
		{
			var project = await _context.Projects
				.FirstOrDefaultAsync(p => p.Id == id && !p.Deleted);
			if (project == null)
				return ResponseHelper.JsonError("Project not found");

			if (project.CreatedById != CurrentUserId)
				return Forbid();

			project.Status = request.Status;

			if (request.Status == ProjectStatus.Disabled)
			{
				var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.ProjectId == id);
				if (wallet != null)
					wallet.Balance = 0m;
			}

			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("Project updated successfully");
		}

		/// <summary>
		/// Sets or clears the project's 4-digit contributor code. Body carries
		/// null or an empty string to clear it.
		/// </summary>
		[HttpPost("{id:int}/access-code")]
		public async Task<IActionResult> SetAccessCode(int id, [FromBody] SetAccessCodeRequest request)
		{
			var trimmed = request?.Code?.Trim();

			if (!string.IsNullOrEmpty(trimmed) && (trimmed.Length != 4 || !trimmed.All(char.IsDigit)))
				return ResponseHelper.JsonError("The code must be exactly 4 digits.");

			var saved = await _projectHelper.SetAccessCode(id, CurrentUserId, trimmed);
			if (!saved)
				return ResponseHelper.JsonError("We could not find that project under your account.");

			return ResponseHelper.JsonSuccess(string.IsNullOrEmpty(trimmed)
				? "Access code removed. Anyone who finds this project can now open it."
				: "Access code saved. Share it with your contributors.");
		}

		[HttpGet("{id:int}/supporters")]
		public async Task<IActionResult> Supporters(
			int id,
			[FromQuery] string? keyword,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to,
			[FromQuery] int page = 1)
		{
			if (await OwnedProjectAsync(id) == null) return NotFound();

			var model = new IPageListModel<SupporterViewModel>
			{
				Keyword = keyword,
				StartDate = from,
				EndDate = to,
			};

			var paged = _projectHelper.GetProjectSupportersPaged(id, model, page);
			return Ok(PagedApiResponse<SupporterViewModel>.From(paged));
		}

		[HttpGet("{id:int}/payments")]
		public async Task<IActionResult> Payments(
			int id,
			[FromQuery] string? keyword,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to,
			[FromQuery] int page = 1)
		{
			var project = await _context.Projects
				.Include(x => x.Wallets)
				.FirstOrDefaultAsync(p => p.Id == id
					&& !p.Deleted
					&& p.CreatedById == CurrentUserId);
			if (project == null) return NotFound();

			var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
			if (user == null) return Unauthorized();

			var model = new IPageListModel<PaymentDTO>
			{
				Keyword = keyword,
				StartDate = from,
				EndDate = to,
			};

			var paged = _projectHelper.GetPaymentsPaged(id, model, page);
			var banks = await _safeHavenHelper.GetBanksAsync();

			// The bank is kept as its code; the owner reads its name.
			foreach (var payment in paged)
			{
				payment.PayerBank = payment.PayerBankCode == null
					? null
					: banks.FirstOrDefault(b => b.code == payment.PayerBankCode)?.name;
			}

			return Ok(new
			{
				projectName = project.Title,
				totalCollected = project.AmountObtained,
				balance = project.Wallets?.Sum(x => x.Balance) ?? 0,
				transactionPinSet = user.TransactionPinSet,
				freeWithdrawalsRemaining = project.Wallets?.FirstOrDefault()?.FreeWithdrawalsRemaining,
				banks,
				payments = PagedApiResponse<PaymentDTO>.From(paged),
			});
		}

		[HttpGet("{id:int}/withdrawals")]
		public async Task<IActionResult> Withdrawals(
			int id,
			[FromQuery] string? keyword,
			[FromQuery] DateTime? from,
			[FromQuery] DateTime? to,
			[FromQuery] int page = 1)
		{
			if (await OwnedProjectAsync(id) == null) return NotFound();

			var model = new IPageListModel<WithdrawalViewModel>
			{
				Keyword = keyword,
				StartDate = from,
				EndDate = to,
			};

			var paged = _projectHelper.WithdrawalHistory(model, id, page);
			return Ok(PagedApiResponse<WithdrawalViewModel>.From(paged));
		}
	}

	public class UpdateProjectStatusRequest
	{
		public ProjectStatus Status { get; set; }
	}

	public class SetAccessCodeRequest
	{
		public string? Code { get; set; }
	}
}
