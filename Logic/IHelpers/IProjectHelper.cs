

using Core.Models;
using Core.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using X.PagedList;

namespace Logic.IHelpers
{
    public interface IProjectHelper
    {
        IQueryable<ProjectViewModel> GetAllProjects();
		Task<bool> UpdateProject(ProjectViewModel project);
        List<ProjectSupporter> GetContributors();
        Task<List<ProjectViewModel>> GetUserProjectsAsync(string userId);
        ProjectViewModel? GetProjectById(int id);

        /// <summary>
        /// The same project as seen by a contributor who found it by search.
        /// The organiser's email address is withheld; their name and phone
        /// number are not, because a contributor needs to recognise whose
        /// project this is. Reaching this page at all requires the access
        /// code, when the owner has set one.
        /// </summary>
        ProjectViewModel? GetPublicProjectById(int id);

        /// <summary>True when the owner set a code that gates the public page.</summary>
        bool RequiresAccessCode(int id);

        /// <summary>Checks a contributor's 4-digit code against the stored hash.</summary>
        bool VerifyAccessCode(int id, string? code);

        /// <summary>Sets or clears the 4-digit code. Only the project's owner may.</summary>
        Task<bool> SetAccessCode(int id, string ownerId, string? code);

        /// <summary>
        /// Creates a Type-B project — a record of payments made elsewhere. It
        /// collects nothing, so it needs no wallet, bank sub-account or KYC.
        /// </summary>
        Task<Project?> CreateTypeBProject(ProjectViewModel project, ApplicationUser user);

        /// <summary>
        /// Records used against records paid for, counted across all the
        /// user's Type-B projects together.
        /// </summary>
        RecordQuotaStatus GetRecordQuota(string userId);

        /// <summary>
        /// Adds one record to a Type-B project. Returns the reason on failure
        /// so the caller can say what went wrong.
        /// </summary>
        Task<(bool ok, string? error)> AddRecord(int projectId, string ownerId, ProjectRecordViewModel record);

        /// <summary>Corrects one record on a Type-B project.</summary>
        Task<(bool ok, string? error)> UpdateRecord(int projectId, int recordId, string ownerId, ProjectRecordViewModel record);

        /// <summary>
        /// Name search across projects their owners have made public. Returns
        /// only what is safe to show a stranger.
        /// </summary>
        List<PublicProjectSearchResult> SearchPublicProjects(string? keyword, int take = 20);
        ProjectPaymentDTO GetPaymentsByProjectId(int projectId);
        bool AddComments(ProjectCommentsViewModel commentDetails);
        IPagedList<ProjectViewModel> Projects(IPageListModel<ProjectViewModel> model, int page, bool disabledOnly = false, string createdById = "", ProjectType? type = null);
		IPagedList<SupporterViewModel> GetProjectSupportersPaged(int projectId, IPageListModel<SupporterViewModel> filter, int page = 1, int pageSize = 10);
        IPagedList<PaymentDTO> GetPaymentsPaged(int projectId, IPageListModel<PaymentDTO> filter, int page = 1, int pageSize = 20);
		Task<Project?> CreateProject(ProjectViewModel project, ApplicationUser user);
		IPagedList<WithdrawalViewModel> WithdrawalHistory(IPageListModel<WithdrawalViewModel> model, int projectId, int page);
	}
}