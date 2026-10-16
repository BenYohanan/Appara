using Core.Constants;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.IHelpers;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using X.PagedList;
using X.PagedList.Extensions;
using static Core.Enums.ApparcusEnum;

namespace Logic.Helpers
{
	public class ProjectHelper : IProjectHelper
	{
		private readonly AppDbContext _context;
		private readonly ISafeHavenHelper _safeHaven;

		public ProjectHelper(AppDbContext context, ISafeHavenHelper safeHaven)
		{
			_context = context;
			_safeHaven = safeHaven;
		}

		public IPagedList<ProjectViewModel> Projects(IPageListModel<ProjectViewModel> model, int page, bool disabledOnly = false, string createdById = "", ProjectType? type = null)
		{
			try
			{
				var user = Utility.GetCurrentUser();
				var request = AppHttpContext.Current.Request;
				string baseUrl = $"{request.Scheme}://{request.Host}";

				var query = _context.Projects
					.Include(p => p.CreatedBy)
					.Where(p => disabledOnly ? (p.Status == ProjectStatus.Disabled || p.Status == ProjectStatus.Closed || p.Deleted) : (p.Status != ProjectStatus.Disabled && !p.Deleted));

				if (!string.IsNullOrEmpty(createdById))
				{
					query = query.Where(p => p.CreatedById == createdById);
				}

				// The Type-B tab, and equally the All/Active/Closed tabs, which
				// list money projects only — a record sheet does not belong
				// among projects that are still collecting.
				if (type.HasValue)
				{
					query = query.Where(p => p.Type == type.Value);
				}

				if (user.UserRole == Constants.UserRole)
				{
					query = query.Where(p => p.CreatedById == user.Id);
				}

				if (!string.IsNullOrEmpty(model.Keyword))
				{
					var key = model.Keyword.ToLower();

					query = query.Where(p =>
						p.Title.ToLower().Contains(key) ||
						p.CreatedBy.FirstName.ToLower().Contains(key) ||
						p.CreatedBy.LastName.ToLower().Contains(key) ||
						p.AccountNumber.ToLower().Contains(key) ||
						p.HoldersName.ToLower().Contains(key) ||
						p.AccountBank.ToLower().Contains(key) ||
						p.AmountNeeded.ToString().Contains(key) ||
						(p.AmountObtained).ToString().Contains(key)
					);
				}

				// Both ends, not just one: the search form offers From and To,
				// and a From that filtered nothing looked like the search had
				// simply ignored it.
				if (model.StartDate.HasValue)
				{
					var from = model.StartDate.Value.Date;
					query = query.Where(p => p.DateCreated >= from);
				}

				if (model.EndDate.HasValue)
				{
					var to = model.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
					query = query.Where(p => p.DateCreated <= to);
				}

				model.CanFilterByDeliveryStatus = true;

				var projected = query.Select(c => new ProjectViewModel
				{
					Id = c.Id,
					Name = c.Title,
					Description = c.Description,
					AmountNeeded = c.AmountNeeded,
					AmountObtained = c.AmountObtained,
					Deleted = c.Deleted,
					DateCreated = c.DateCreated,
					ShowForAudit = c.ShowForAudit,
					IsPublic = c.IsPublic,
					HasAccessCode = c.AccessCode != null,
					Type = c.Type.ToString(),
					CustomDate = c.CustomDate,
					RelatedImageUrl = c.RelatedImageUrl,
					RecordCount = c.ProjectSupporters!.Count(x => !x.Deleted),
					CreatedById = c.CreatedById,
					CreatedBy = c.CreatedBy.FirstName + " " + c.CreatedBy.LastName,
					ProjectSupporters = null,
					SupportLink = $"{baseUrl}/Guest/View/{c.Id}",
					AccountNumber = c.CreatedBy.AccountNumber,
					AccountName = c.CreatedBy.HoldersName,
					Status = c.Status.ToString(),
					Balance = c.Wallets.Sum(x => x.Balance)
				});

				var paged = projected.ToPagedList(page, 25);

				var projectIds = paged.Select(x => x.Id).ToList();

				// Paid supporters only, as on the Supporters page itself.
				var supportersLookup = _context.ProjectSupporters
					.Where(s => projectIds.Contains(s.ProjectId)
						&& s.Amount > 0
						&& !s.Deleted
						&& (s.Project!.Type == ProjectType.B
							|| _context.Contributions.Any(c => c.ProjectSupporterId == s.Id
								&& c.PaymentStatus == PaymentStatus.Successful)))
					.GroupBy(s => s.ProjectId)
					.ToDictionary(g => g.Key, g => g.ToList());

				foreach (var item in paged)
				{
					item.ProjectSupporters = supportersLookup.ContainsKey(item.Id)
						? supportersLookup[item.Id]
						: [];
				}

				return paged;
			}
			catch
			{
				throw;
			}
		}

		public IQueryable<ProjectViewModel> GetAllProjects()
		{
			var user = Utility.GetCurrentUser();
			var request = AppHttpContext.Current.Request;
			string baseUrl = $"{request.Scheme}://{request.Host}";

			var query = _context.Projects
				.Include(p => p.CreatedBy)
				.Include(p => p.ProjectSupporters)
				.Where(p => !p.Deleted && p.Status != ProjectStatus.Disabled);

			if (user.UserRole == Constants.UserRole)
			{
				query = query.Where(p => p.CreatedById == user.Id);
			}

			return query.Select(c => new ProjectViewModel
			{
				Id = c.Id,
				Name = c.Title,
				Description = c.Description,
				AmountNeeded = c.AmountNeeded,
				AmountObtained = c.AmountObtained,
				Deleted = c.Deleted,
				DateCreated = c.DateCreated,
				CreatedById = c.CreatedById,
				AccountNumber = c.CreatedBy != null ? c.CreatedBy.AccountNumber : "",
				AccountName = c.CreatedBy != null ? c.CreatedBy.HoldersName : "",
				ShowForAudit = c.ShowForAudit,
				IsPublic = c.IsPublic,
				HasAccessCode = c.AccessCode != null,
				Type = c.Type.ToString(),
				CustomDate = c.CustomDate,
				RelatedImageUrl = c.RelatedImageUrl,
				Status = c.Status.ToString(),
				CreatedBy = c.CreatedBy != null ? c.CreatedBy.FullName : "",
				ProjectSupporters = c.ProjectSupporters.Where(x => x.Amount > 0).ToList(),
				SupportLink = $"{baseUrl}/Guest/View/{c.Id}",
			});
		}

		public async Task<List<ProjectViewModel>> GetUserProjectsAsync(string userId)
		{
			return await _context.Projects
				.Include(p => p.CreatedBy)
				.Where(p => p.CreatedById == userId && !p.Deleted && p.Status != ProjectStatus.Disabled)
				.Select(c => new ProjectViewModel
				{
					Id = c.Id,
					Name = c.Title,
					Description = c.Description,
					AmountNeeded = c.AmountNeeded,
					AmountObtained = c.AmountObtained,
					Deleted = c.Deleted,
					DateCreated = c.DateCreated,
					CreatedById = c.CreatedById,
					AccountNumber = c.CreatedBy.AccountNumber,
					AccountName = c.CreatedBy.HoldersName,
					Status = c.Status.ToString(),
					CreatedBy = c.CreatedBy.FirstName + " " + c.CreatedBy.LastName,
					ProjectSupporters = c.ProjectSupporters,
				})
				.ToListAsync();
		}

		public async Task<Project?> CreateProject(ProjectViewModel project, ApplicationUser user)
		{
			var newProject = new Project
			{
				Title = project.Name,
				Description = project.Description,
				AmountNeeded = project.AmountNeeded,
				CreatedById = user.Id,
				ShowForAudit = project.ShowForAudit,
				IsPublic = project.IsPublic,
				// A picture of what is being raised for is worth more to a
				// contributor than the description they will not read. It was
				// only ever stored on Type-B, so a money project had no way to
				// show one.
				RelatedImageUrl = project.RelatedImageUrl,
			};

			if (string.IsNullOrEmpty(user.SafeHavenAccountId))
			{
				var account = await _safeHaven.CreateSubAccount(user, $"USER_{user.Id}_{Guid.NewGuid()}");

				if (account == null || string.IsNullOrEmpty(account.data._id))
					return null;

				user.AccountNumber = account.data.accountNumber;
				user.HoldersName = account.data.accountName;
				user.SafeHavenAccountId = account.data._id;
				user.AccountBank = account.data._id;

				_context.ApplicationUsers.Update(user);
			}

			_context.Projects.Add(newProject);
			await _context.SaveChangesAsync();

			if (project.CustomFields != null && project.CustomFields.Count != 0)
			{
				foreach (var field in project.CustomFields)
				{
					_context.Add(new ProjectCustomField
					{
						ProjectId = newProject.Id,
						FieldName = field.FieldName,
						FieldType = field.FieldType,
						IsRequired = field.IsRequired
					});
				}

				await _context.SaveChangesAsync();
			}

			await ApplyPaymentListField(newProject, project);

			// The caller needs the new id: setting an access code is a separate
			// write, and the add form has to reach the project it just made.
			return newProject;
		}

		public async Task<bool> UpdateProject(ProjectViewModel project)
		{
			var projectVM = _context.Projects
				.Include(x => x.CreatedBy)
				.FirstOrDefault(p => p.Id == project.Id && !p.Deleted);

			if (projectVM == null)
				return false;

			// A Type-B project never takes a contribution, so it needs no bank
			// sub-account — and opening one would fail for a user who has not
			// done KYC they do not need.
			if (projectVM.Type == ProjectType.A
				&& projectVM.CreatedBy != null && string.IsNullOrEmpty(projectVM.CreatedBy.SafeHavenAccountId))
			{
				var account = await _safeHaven.CreateSubAccount(projectVM.CreatedBy, $"USER_{projectVM.CreatedById}_{Guid.NewGuid()}");

				if (account == null || string.IsNullOrEmpty(account.data._id))
					return false;

				projectVM.CreatedBy.AccountNumber = account.data.accountNumber;
				projectVM.CreatedBy.HoldersName = account.data.accountName;
				projectVM.CreatedBy.SafeHavenAccountId = account.data._id;
				projectVM.CreatedBy.AccountBank = account.data._id;
			}

			projectVM.Title = project.Name;
			projectVM.Description = project.Description;
			projectVM.AmountNeeded = project.AmountNeeded;
			projectVM.ShowForAudit = project.ShowForAudit;
			projectVM.IsPublic = project.IsPublic;

			// Both kinds carry a picture. Only when supplied, either way: an
			// edit that does not touch the image must not clear the one already
			// on the project.
			if (project.RelatedImageUrl != null)
				projectVM.RelatedImageUrl = project.RelatedImageUrl;

			if (projectVM.Type == ProjectType.B)
			{
				// The owner's own date is Type-B only, and the same rule applies:
				// an edit that leaves it alone must not blank what is recorded.
				if (project.CustomDate.HasValue)
					projectVM.CustomDate = project.CustomDate;
			}

			var existingFields = _context.ProjectCustomFields
				.Where(x => x.ProjectId == projectVM.Id)
				.ToList();

			var incomingIds = (project.CustomFields ?? [])
				.Where(f => f.Id != 0)
				.Select(f => f.Id)
				.ToHashSet();

			var fieldsToRemove = existingFields
				.Where(x => !incomingIds.Contains(x.Id))
				.ToList();

			if (fieldsToRemove.Count != 0)
				_context.ProjectCustomFields.RemoveRange(fieldsToRemove);

			if (project.CustomFields != null && project.CustomFields.Count > 0)
			{
				foreach (var field in project.CustomFields)
				{
					var existing = existingFields.FirstOrDefault(x => x.Id == field.Id);

					if (existing != null)
					{
						existing.FieldName = field.FieldName;
						existing.FieldType = field.FieldType;
						existing.IsRequired = field.IsRequired;
					}
					else
					{
						_context.ProjectCustomFields.Add(new ProjectCustomField
						{
							ProjectId = projectVM.Id,
							FieldName = field.FieldName,
							FieldType = field.FieldType,
							IsRequired = field.IsRequired
						});
					}
				}
			}
			else
			{
				_context.ProjectCustomFields.RemoveRange(existingFields);
			}

			_context.SaveChanges();

			// After the fields are saved, so a field added in this same edit
			// has an id to be chosen by.
			await ApplyPaymentListField(projectVM, project);
			return true;
		}

		public List<ProjectSupporter> GetContributors()
		{
			return _context.ProjectSupporters.Where(ps => !ps.Deleted).ToList();
		}

		public ProjectViewModel? GetProjectById(int id)
		{
			var project = _context.Projects
				.Include(p => p.CreatedBy)
				.Include(p => p.CustomFields)
				.Include(p => p.Contributions)
					.ThenInclude(x => x.ProjectSupporter)
				.Where(p => !p.Deleted && p.Id == id && p.Status != ProjectStatus.Disabled)
				.Select(c => new ProjectViewModel
				{
					Id = c.Id,
					Name = c.Title,
					Description = c.Description,
					AmountNeeded = c.AmountNeeded,
					AmountObtained = c.AmountObtained,
					Deleted = c.Deleted,
					DateCreated = c.DateCreated,
					CreatedById = c.CreatedById,
					CreatedBy = c.CreatedBy != null ? c.CreatedBy.FullName : "",
					CreatedByDateJoined = c.CreatedBy != null ? c.CreatedBy.DateCreated : DateTime.MinValue,
					CreatedByEmail = c.CreatedBy != null ? c.CreatedBy.Email : "",
					CreatedByPhoneNumber = c.CreatedBy != null ? c.CreatedBy.PhoneNumber : "",
					OwnersProfilePic = c.CreatedBy != null ? c.CreatedBy.ProfilePictureUrl : "",
					Comments = c.Comments,
					Status = c.Status.ToString(),

					Withdrawals = _context.Withdrawals
						.Where(w => w.ProjectId == c.Id)
						.Select(w => new WithdrawalViewModel
						{
							Amount = w.Amount,
							AccountWithdrawnTo = w.AccountWithdrawnTo,
							Status = w.Status,
							CreatedAt = w.CreatedAt,
							SentTo = w.AccountName,
						}).ToList(),

					AccountNumber = c.CreatedBy != null ? c.CreatedBy.AccountNumber : "",
					AccountName = c.CreatedBy != null ? c.CreatedBy.HoldersName : "",
					ShowForAudit = c.ShowForAudit,
					IsPublic = c.IsPublic,
				HasAccessCode = c.AccessCode != null,
				Type = c.Type.ToString(),
				CustomDate = c.CustomDate,
				RelatedImageUrl = c.RelatedImageUrl,
				PaymentListFieldId = c.PaymentListFieldId,
				AccountFeeStatus = c.AccountFeeStatus.ToString(),

					ProjectContributions = _context.Contributions
					.Include(x => x.ProjectSupporter)
						.Where(x => x.ProjectId == c.Id && x.PaymentStatus == PaymentStatus.Successful && x.ProjectSupporter != null)
						.Select(x => new ProjectContributionVM
						{
							ContributorsName = x.ProjectSupporter != null ? x.ProjectSupporter.FullName : "",
							Amount = x.Amount.ToString(),
							Date = x.Date,
							ProjectSupporterId = x.ProjectSupporterId
						}).ToList(),

					CustomFields = c.CustomFields.Select(f => new ProjectCustomFieldVM
					{
						Id = f.Id,
						FieldName = f.FieldName,
						FieldType = f.FieldType,
						IsRequired = f.IsRequired
					}).ToList()
				}).FirstOrDefault();

			// A Type-B project has no contributions behind it — its list is the
			// records themselves, typed in from the sheet being replaced. Filling
			// the same collection means every caller renders one list, not two.
			if (project != null && project.Type == ProjectType.B.ToString())
			{
				project.ProjectContributions = _context.ProjectSupporters
					.Where(r => r.ProjectId == id && !r.Deleted)
					.OrderByDescending(r => r.DateCreated)
					.Select(r => new ProjectContributionVM
					{
						ContributorsName = r.FullName,
						Amount = r.Amount.ToString(),
						Date = r.DateCreated,
						ProjectSupporterId = r.Id
					}).ToList();
			}

			if (project != null)
				FillPaymentListColumn(project);

			return project;
		}

		/// <summary>
		/// Puts the owner's chosen field beside each name on the payment list.
		/// A contributor who left that field blank shows their amount instead,
		/// rather than an empty space.
		/// </summary>
		private void FillPaymentListColumn(ProjectViewModel project)
		{
			var field = project.PaymentListFieldId is int fieldId
				? project.CustomFields?.FirstOrDefault(f => f.Id == fieldId)
				: null;

			if (field == null)
			{
				project.PaymentListFieldId = null;
				return;
			}

			project.PaymentListField = field.FieldName;

			if (project.ProjectContributions == null || project.ProjectContributions.Count == 0)
				return;

			var supporterIds = project.ProjectContributions
				.Where(c => c.ProjectSupporterId != null)
				.Select(c => c.ProjectSupporterId!.Value)
				.Distinct()
				.ToList();

			var answers = _context.ProjectCustomFieldValues
				.Where(v => v.ProjectCustomFieldId == field.Id
					&& v.ProjectSupporterId != null
					&& supporterIds.Contains(v.ProjectSupporterId.Value)
					&& v.Value != null && v.Value != "")
				.Select(v => new { SupporterId = v.ProjectSupporterId!.Value, v.Value, v.Id })
				.ToList()
				// A supporter who paid twice answered twice; the latest stands.
				.GroupBy(v => v.SupporterId)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Id).First().Value);

			foreach (var row in project.ProjectContributions)
			{
				if (row.ProjectSupporterId is int sid && answers.TryGetValue(sid, out var value))
				{
					row.DisplayLabel = field.FieldName;
					row.DisplayValue = value;
				}
			}
		}

		/// <summary>
		/// One page of a project's contributor list, for Type-A and Type-B alike.
		///
		/// The paging, filtering and ordering happen in SQL over the supporter
		/// rows alone; the money and the custom-field answers are stitched on
		/// afterwards from two flat follow-up queries. It used to be one LINQ
		/// statement that left-joined a grouped sub-select and then read the
		/// latest status out of the group — a shape EF cannot turn into SQL, so
		/// the page threw rather than rendered. Nothing is client-evaluated
		/// beyond the ten or so rows already on the page.
		/// </summary>
		public IPagedList<SupporterViewModel> GetProjectSupportersPaged(int projectId, IPageListModel<SupporterViewModel> filter, int page = 1, int pageSize = 10)
		{
			// On Type-A a supporter row is written the moment someone opens the
			// payment form, before a naira has moved, so a row alone is not a
			// supporter. Only those with a contribution that actually landed are
			// listed — an abandoned or failed payment is not support. Type-B
			// rows are records of money already paid elsewhere, so they stand.
			var query = _context.ProjectSupporters
				.Where(s => s.ProjectId == projectId
						 && s.Amount > 0
						 && !s.Deleted
						 && (s.Project!.Type == ProjectType.B
							 || _context.Contributions.Any(c => c.ProjectSupporterId == s.Id
								 && c.PaymentStatus == PaymentStatus.Successful)));

			if (!string.IsNullOrWhiteSpace(filter.Keyword))
			{
				var keyword = filter.Keyword.ToLower();

				query = query.Where(s =>
					(s.FullName != null && s.FullName.ToLower().Contains(keyword)) ||
					(s.PhoneNumber != null && s.PhoneNumber.Contains(keyword)));
			}

			if (filter.StartDate.HasValue)
			{
				var from = filter.StartDate.Value.Date;
				query = query.Where(s => s.DateCreated >= from);
			}

			if (filter.EndDate.HasValue)
			{
				var to = filter.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
				query = query.Where(s => s.DateCreated <= to);
			}

			var paged = query
				.OrderByDescending(s => s.DateCreated)
				.ThenByDescending(s => s.Id)
				.Select(s => new SupporterRow
				{
					Id = s.Id,
					ProjectId = s.ProjectId,
					FullName = s.FullName,
					PhoneNumber = s.PhoneNumber,
					DateCreated = s.DateCreated,
					RecordedAmount = s.Amount,
					ProjectTitle = s.Project != null ? s.Project.Title : null,
					IsTypeB = s.Project != null && s.Project.Type == ProjectType.B,
				})
				.ToPagedList(page, pageSize);

			var supporterIds = paged.Select(r => r.Id).ToList();

			// Loaded flat and grouped in memory. A "latest status per supporter"
			// is an ordered sub-select inside a group, which EF will not
			// translate — and over one page of rows it is not worth the trip.
			var contributions = _context.Contributions
				.Where(c => c.ProjectSupporterId != null
						 && c.ProjectId == projectId
						 && supporterIds.Contains(c.ProjectSupporterId.Value))
				.Select(c => new
				{
					SupporterId = c.ProjectSupporterId!.Value,
					c.Amount,
					c.PaymentStatus,
					c.Date,
				})
				.ToList();

			var contributionsBySupporter = contributions
				.GroupBy(c => c.SupporterId)
				.ToDictionary(g => g.Key, g => g.ToList());

			var fieldValues = _context.ProjectCustomFieldValues
				.Where(v => v.ProjectSupporterId != null
						 && supporterIds.Contains(v.ProjectSupporterId.Value)
						 && v.Value != null
						 && v.Value != "")
				.Select(v => new
				{
					SupporterId = v.ProjectSupporterId!.Value,
					v.ProjectCustomFieldId,
					FieldName = v.ProjectCustomField != null ? v.ProjectCustomField.FieldName : "",
					v.Value,
				})
				.ToList();

			var fieldsBySupporter = fieldValues
				.Where(v => !string.IsNullOrWhiteSpace(v.Value))
				.GroupBy(v => v.SupporterId)
				.ToDictionary(
					g => g.Key,
					g => g.Select(v => new ProjectCustomFieldValueVM
					{
						ProjectCustomFieldId = v.ProjectCustomFieldId,
						FieldName = v.FieldName ?? "",
						Value = v.Value,
					}).ToList());

			var supporters = paged.Select(row =>
			{
				contributionsBySupporter.TryGetValue(row.Id, out var paid);

				var hasSuccessful = paid != null
					&& paid.Any(c => c.PaymentStatus == PaymentStatus.Successful);

				PaymentStatus? status;
				decimal amount;

				if (row.IsTypeB)
				{
					// A Type-B row is a payment that already happened somewhere
					// else — that is the whole point of the record — so it reads
					// as settled, and its amount is the one on the record itself
					// rather than a contribution that will never exist.
					status = PaymentStatus.Successful;
					amount = row.RecordedAmount;
				}
				else
				{
					status = hasSuccessful
						? PaymentStatus.Successful
						: paid?.OrderByDescending(c => c.Date).FirstOrDefault()?.PaymentStatus
							?? PaymentStatus.Pending;

					// On Type-A the figure has to stay the amount actually paid,
					// never the amount pledged — and an attempt that was abandoned
					// or refunded was never paid.
					amount = paid?
						.Where(c => c.PaymentStatus == PaymentStatus.Successful)
						.Sum(c => c.Amount) ?? 0m;
				}

				return new SupporterViewModel
				{
					Id = row.Id,
					ProjectId = row.ProjectId,
					FullName = row.FullName ?? string.Empty,
					PhoneNumber = row.PhoneNumber,
					DateCreated = row.DateCreated,
					ProjectTitle = row.ProjectTitle ?? string.Empty,
					PaymentStatus = status,
					PaymentStatusInString = status?.ToString(),
					Amount = amount,
					CustomFields = fieldsBySupporter.TryGetValue(row.Id, out var fields)
						? fields
						: [],
				};
			}).ToList();

			return new StaticPagedList<SupporterViewModel>(
				supporters, paged.PageNumber, paged.PageSize, paged.TotalItemCount);
		}

		/// <summary>The supporter columns the page is built from, before the
		/// money and the custom fields are stitched on.</summary>
		private sealed class SupporterRow
		{
			public int Id { get; set; }
			public int ProjectId { get; set; }
			public string? FullName { get; set; }
			public string? PhoneNumber { get; set; }
			public DateTime? DateCreated { get; set; }
			public decimal RecordedAmount { get; set; }
			public string? ProjectTitle { get; set; }
			public bool IsTypeB { get; set; }
		}

		
		public IPagedList<PaymentDTO> GetPaymentsPaged(int projectId, IPageListModel<PaymentDTO> filter, int page = 1, int pageSize = 20)
		{
			var query = _context.Contributions
				.Include(c => c.ProjectSupporter)
				.Where(c => c.ProjectId == projectId && c.PaymentStatus == PaymentStatus.Successful)
				.Select(c => new PaymentDTO
				{
					Contributor = c.ProjectSupporter != null ? c.ProjectSupporter.FullName : "",
					ContributorPhone = c.ProjectSupporter != null ? c.ProjectSupporter.PhoneNumber : "",
					AmountPaid = c.Amount,
					PaymentStatus = c.PaymentStatus,
					PaymentStatusInString = c.PaymentStatus.ToString(),
					ProjectId = c.ProjectId,
					ContributorId = c.ProjectSupporterId,
					PaymentDate = c.Date,
					ApprovedDate = c.ApprovedDate,
					Reference = c.ExternalReference,
					PayerAccountName = c.PayerAccountName,
					PayerAccountNumber = c.PayerAccountNumber,
					PayerBankCode = c.PayerBankCode
				});

			if (!string.IsNullOrWhiteSpace(filter.Keyword))
			{
				var keyword = filter.Keyword.ToLower();
				query = query.Where(x =>
					x.Contributor.ToLower().Contains(keyword) ||
					(x.Reference != null && x.Reference.Contains(keyword)));
			}

			if (filter.StartDate.HasValue)
				query = query.Where(x => x.PaymentDate >= filter.StartDate.Value.Date);

			if (filter.EndDate.HasValue)
				query = query.Where(x => x.PaymentDate <= filter.EndDate.Value.Date.AddDays(1).AddSeconds(-1));

			var paged = query
				.OrderByDescending(x => x.PaymentDate)
				.ToPagedList(page, pageSize);

			// The full number is read from the database but never leaves this
			// method: everything downstream, the app included, gets it masked.
			foreach (var payment in paged)
				payment.PayerAccountNumber = MaskAccountNumber(payment.PayerAccountNumber);

			return paged;
		}

		/// <summary>"0081232382" becomes "008 *** 2382".</summary>
		public static string? MaskAccountNumber(string? number)
		{
			if (string.IsNullOrWhiteSpace(number))
				return null;

			number = number.Trim();

			return number.Length <= 7
				? new string('*', Math.Max(0, number.Length - 4)) + number[^Math.Min(4, number.Length)..]
				: $"{number[..3]} *** {number[^4..]}";
		}

		public ProjectPaymentDTO GetPaymentsByProjectId(int projectId)
		{
			var projectPaymentDTO = new ProjectPaymentDTO();
			var project = _context.Projects
			   .Where(p => p.Id == projectId)
			   .Include(p => p.Contributions)
					.ThenInclude(p => p.ProjectSupporter)
			   .FirstOrDefault();
			if (project == null)
				return projectPaymentDTO;
			var contributions = project.Contributions;
			projectPaymentDTO.ProjectName = project.Title;
			projectPaymentDTO.Payments = [.. contributions.Select(c => new PaymentDTO
			{
				Contributor = c.ProjectSupporter?.FullName,
				PaymentDate = c.Date,
				AmountPaid = c.Amount,
				Reference = c.ExternalReference
			})];
			return projectPaymentDTO;
		}

		public static byte[] GenerateQRCode(string link)
		{
			if (string.IsNullOrEmpty(link))
			{
				return null;
			}

			QRCodeGenerator qrGenerator = new();
			QRCodeData qrCodeData = qrGenerator.CreateQrCode(link, QRCodeGenerator.ECCLevel.Q);
			QRCode qrCode = new QRCode(qrCodeData);
			Bitmap qrCodeImage = qrCode.GetGraphic(5);

			using MemoryStream stream = new();
			qrCodeImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
			return stream.ToArray();
		}

		public bool AddComments(ProjectCommentsViewModel commentDetails)
		{
			var comments = new ProjectComment
			{
				Comment = commentDetails.Comment,
				FullName = commentDetails.FullName,
				ProjectId = commentDetails.ProjectId,
			};

			_context.Add(comments);
			_context.SaveChanges();
			return true;
		}

		public IPagedList<WithdrawalViewModel> WithdrawalHistory(IPageListModel<WithdrawalViewModel> model, int projectId, int page)
		{
			var query = _context.Withdrawals
					.Include(p => p.Project)
					.Where(p => p.ProjectId == projectId);

			if (!string.IsNullOrEmpty(model.Keyword))
			{
				var key = model.Keyword.ToLower();

				query = query.Where(p =>
					p.Amount.ToString().Contains(key) ||
					p.Project.Title.ToLower().Contains(key) ||
					p.Status.ToLower().Contains(key) ||
					p.TransferReference.ToLower().Contains(key)
				);
			}

			if (model.StartDate.HasValue)
			{
				var from = model.StartDate.Value.Date;
				query = query.Where(p => p.CreatedAt >= from);
			}

			if (model.EndDate.HasValue)
			{
				var to = model.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
				query = query.Where(p => p.CreatedAt <= to);
			}

			var projected = query.Select(c => new WithdrawalViewModel
			{
				Id = c.Id,
				ProjectName = c.Project.Title,
				Amount = c.Amount,
				AccountWithdrawnTo = c.AccountWithdrawnTo,
				Status = c.Status,
				CreatedAt = c.CreatedAt,
				SentTo = c.AccountName
			});

			return projected.ToPagedList(page, 25);
		}


		/// <summary>
		/// Creates a Type-B project: a record of payments that already happened,
		/// kept so a paper sheet does not have to be. It collects nothing, so it
		/// gets no wallet, no bank sub-account and no KYC gate — unlike
		/// CreateProject, which opens a sub-account before it can take money.
		/// </summary>
		public async Task<Project?> CreateTypeBProject(ProjectViewModel project, ApplicationUser user)
		{
			if (string.IsNullOrWhiteSpace(project.Name))
				return null;

			var newProject = new Project
			{
				Title = project.Name,
				Description = project.Description,
				Type = ProjectType.B,
				CustomDate = project.CustomDate,
				RelatedImageUrl = project.RelatedImageUrl,
				CreatedById = user.Id,
				ShowForAudit = false,
				IsPublic = project.IsPublic,
				// Filled in as records are added; there is no target to raise.
				AmountNeeded = 0,
				AmountObtained = 0,
			};

			_context.Projects.Add(newProject);
			await _context.SaveChangesAsync();

			if (project.CustomFields != null && project.CustomFields.Count != 0)
			{
				foreach (var field in project.CustomFields)
				{
					if (string.IsNullOrWhiteSpace(field.FieldName))
						continue;

					_context.ProjectCustomFields.Add(new ProjectCustomField
					{
						ProjectId = newProject.Id,
						FieldName = field.FieldName,
						FieldType = field.FieldType,
						IsRequired = field.IsRequired
					});
				}

				await _context.SaveChangesAsync();
			}

			await ApplyPaymentListField(newProject, project);

			return newProject;
		}

		/// <summary>
		/// Sets which custom field the public payment list shows beside each
		/// name. Matched by id when the form knows it, otherwise by name — a new
		/// project's fields have no ids until they are saved. Anything that does
		/// not match one of this project's own fields clears the choice, so the
		/// list falls back to the amount.
		/// </summary>
		private async Task ApplyPaymentListField(Project target, ProjectViewModel incoming)
		{
			var fields = await _context.ProjectCustomFields
				.Where(f => f.ProjectId == target.Id)
				.Select(f => new { f.Id, f.FieldName })
				.ToListAsync();

			int? chosen = null;

			if (incoming.PaymentListFieldId == null && incoming.PaymentListField == null)
			{
				// Not sent at all — an app version that predates the choice — so
				// it stands, as long as its field still exists. Choosing the
				// amount is sent as an empty name instead.
				chosen = fields.Any(f => f.Id == target.PaymentListFieldId) ? target.PaymentListFieldId : null;
			}
			else if (incoming.PaymentListFieldId is int id && fields.Any(f => f.Id == id))
			{
				chosen = id;
			}
			else if (!string.IsNullOrWhiteSpace(incoming.PaymentListField))
			{
				var name = incoming.PaymentListField.Trim();
				chosen = fields.FirstOrDefault(f =>
					string.Equals(f.FieldName?.Trim(), name, StringComparison.OrdinalIgnoreCase))?.Id;
			}

			if (target.PaymentListFieldId == chosen)
				return;

			target.PaymentListFieldId = chosen;
			await _context.SaveChangesAsync();
		}

		/// <summary>
		/// What the user has used of what they have paid for. Counted across all
		/// their Type-B projects together, which is what makes one pack cover the
		/// lot rather than one project each.
		/// </summary>
		public RecordQuotaStatus GetRecordQuota(string userId)
		{
			var used = _context.ProjectSupporters
				// Deleting a project is revocable, so its records still count.
				// Freeing the allowance on delete would let the same pack be
				// spent over and over: fill it, delete, fill it again, restore.
				.Count(r => !r.Deleted
					&& r.Project != null
					&& r.Project.Type == ProjectType.B
					&& r.Project.CreatedById == userId);

			var user = _context.ApplicationUsers.FirstOrDefault(u => u.Id == userId);

			return new RecordQuotaStatus
			{
				Used = used,
				Quota = user?.RecordQuota ?? 0,
				PersonalBalance = user?.PersonalBalance ?? 0m,
				Packs = [.. RecordQuotaConstants.Packs.Select(p => new RecordPackVM
				{
					Size = p.Size,
					PriceNaira = p.PriceNaira,
				})],
				PackSize = RecordQuotaConstants.DefaultPack.Size,
				PackPriceNaira = RecordQuotaConstants.DefaultPack.PriceNaira,
			};
		}

		/// <summary>
		/// Adds one record to a Type-B project. Returns the reason on failure so
		/// the caller can say what went wrong rather than "failed".
		/// </summary>
		public async Task<(bool ok, string? error)> AddRecord(int projectId, string ownerId, ProjectRecordViewModel record)
		{
			var project = await _context.Projects
				.Include(p => p.CustomFields)
				.FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted && p.CreatedById == ownerId);

			if (project == null)
				return (false, "We could not find that project under your account.");

			if (project.Type != ProjectType.B)
				return (false, "Records can only be added to a Type-B project.");

			if (string.IsNullOrWhiteSpace(record.FullName))
				return (false, "The contributor's name is required.");

			if (record.Amount <= 0)
				return (false, "Enter the amount paid.");

			var quota = GetRecordQuota(ownerId);
			if (quota.Remaining <= 0)
			{
				var smallest = RecordQuotaConstants.DefaultPack;

				return (false, $"You have used all {quota.Quota:N0} of your saved records. " +
					$"Add another {smallest.Size:N0} from ₦{smallest.PriceNaira:N0} to keep going.");
			}

			// Every required field the owner set on the project has to be answered,
			// or the sheet they are replacing ends up with holes in it.
			var values = record.FieldValues ?? [];

			foreach (var field in project.CustomFields.Where(f => f.IsRequired))
			{
				var answered = values.Any(v => v.ProjectCustomFieldId == field.Id
					&& !string.IsNullOrWhiteSpace(v.Value));

				if (!answered)
					return (false, $"\"{field.FieldName}\" is required.");
			}

			var supporter = new ProjectSupporter
			{
				ProjectId = project.Id,
				FullName = record.FullName.Trim(),
				Amount = record.Amount,
				PhoneNumber = string.IsNullOrWhiteSpace(record.PhoneNumber) ? null : record.PhoneNumber.Trim(),
			};

			_context.ProjectSupporters.Add(supporter);
			await _context.SaveChangesAsync();

			// Values are matched against the project's own fields, so a payload
			// naming a field from someone else's project cannot write here.
			var fieldIds = project.CustomFields.Select(f => f.Id).ToHashSet();

			foreach (var value in values)
			{
				if (!fieldIds.Contains(value.ProjectCustomFieldId) || string.IsNullOrWhiteSpace(value.Value))
					continue;

				_context.ProjectCustomFieldValues.Add(new ProjectCustomFieldValue
				{
					ProjectId = project.Id,
					ProjectSupporterId = supporter.Id,
					ProjectCustomFieldId = value.ProjectCustomFieldId,
					Value = value.Value.Trim()
				});
			}

			// Type-B has no contributions behind it, so the running total is kept
			// on the project as records land.
			project.AmountObtained += record.Amount;

			await _context.SaveChangesAsync();
			return (true, null);
		}

		/// <summary>
		/// Corrects a record on a Type-B project. Records are typed in by hand,
		/// so a misspelt name or a wrong figure has to be fixable. The same
		/// checks as adding apply, and the project's running total moves by the
		/// difference.
		/// </summary>
		public async Task<(bool ok, string? error)> UpdateRecord(int projectId, int recordId, string ownerId, ProjectRecordViewModel record)
		{
			var project = await _context.Projects
				.Include(p => p.CustomFields)
				.FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted && p.CreatedById == ownerId);

			if (project == null)
				return (false, "We could not find that project under your account.");

			if (project.Type != ProjectType.B)
				return (false, "Only records on a Type-B project can be edited.");

			var supporter = await _context.ProjectSupporters
				.Include(s => s.ProjectCustomFieldValues)
				.FirstOrDefaultAsync(s => s.Id == recordId && s.ProjectId == projectId && !s.Deleted);

			if (supporter == null)
				return (false, "That record could not be found.");

			if (string.IsNullOrWhiteSpace(record.FullName))
				return (false, "The contributor's name is required.");

			if (record.Amount <= 0)
				return (false, "Enter the amount paid.");

			var values = record.FieldValues ?? [];

			foreach (var field in project.CustomFields.Where(f => f.IsRequired))
			{
				var answered = values.Any(v => v.ProjectCustomFieldId == field.Id
					&& !string.IsNullOrWhiteSpace(v.Value));

				if (!answered)
					return (false, $"\"{field.FieldName}\" is required.");
			}

			project.AmountObtained += record.Amount - supporter.Amount;

			supporter.FullName = record.FullName.Trim();
			supporter.Amount = record.Amount;
			supporter.PhoneNumber = string.IsNullOrWhiteSpace(record.PhoneNumber) ? null : record.PhoneNumber.Trim();

			// The answers are replaced as a set: what the form sends is the
			// record as it should now read, blanks included.
			_context.ProjectCustomFieldValues.RemoveRange(supporter.ProjectCustomFieldValues);

			var fieldIds = project.CustomFields.Select(f => f.Id).ToHashSet();

			foreach (var value in values)
			{
				if (!fieldIds.Contains(value.ProjectCustomFieldId) || string.IsNullOrWhiteSpace(value.Value))
					continue;

				_context.ProjectCustomFieldValues.Add(new ProjectCustomFieldValue
				{
					ProjectId = project.Id,
					ProjectSupporterId = supporter.Id,
					ProjectCustomFieldId = value.ProjectCustomFieldId,
					Value = value.Value.Trim()
				});
			}

			await _context.SaveChangesAsync();
			return (true, null);
		}

		public ProjectViewModel? GetPublicProjectById(int id)
		{
			var project = GetProjectById(id);

			if (project == null)
				return null;

			// A Type-B project exists to be shared as a link, whether or not it is
			// also listed publicly, and the spec for that link withholds the email
			// either way. So the check is on the link being handed out, not on the
			// project being searchable.
			if (!project.IsPublic && project.Type != ProjectType.B.ToString())
				return project;

			// Contributors need to see who they are paying — a name and a phone
			// number are how someone in a congregation recognises their own
			// group's project and knows who to call if a transfer goes wrong.
			// The email address is the one detail withheld: it is the handle
			// that invites spam and phishing, and it is no use to a contributor.
			project.CreatedByEmail = null;

			// The access code, not redaction, is what keeps strangers out of a
			// public project: everything below this point is visible only after
			// the contributor has entered the code their project rep gave them.
			return project;
		}

		/// <summary>
		/// True when the owner has set a code, so the page must not open until
		/// a contributor enters it.
		/// </summary>
		public bool RequiresAccessCode(int id)
		{
			return _context.Projects
				.Any(p => p.Id == id && !p.Deleted && p.IsPublic && p.AccessCode != null);
		}

		public bool VerifyAccessCode(int id, string? code)
		{
			if (string.IsNullOrWhiteSpace(code))
				return false;

			var hash = _context.Projects
				.Where(p => p.Id == id && !p.Deleted && p.IsPublic)
				.Select(p => p.AccessCode)
				.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(hash))
				return false;

			return BCrypt.Net.BCrypt.Verify(code.Trim(), hash);
		}

		/// <summary>
		/// Sets or clears the 4-digit code. A null or empty code clears it,
		/// which reopens the project to anyone who finds it in search.
		/// </summary>
		public async Task<bool> SetAccessCode(int id, string ownerId, string? code)
		{
			var project = await _context.Projects
				.FirstOrDefaultAsync(p => p.Id == id && !p.Deleted && p.CreatedById == ownerId);

			if (project == null)
				return false;

			if (string.IsNullOrWhiteSpace(code))
			{
				project.AccessCode = null;
			}
			else
			{
				code = code.Trim();
				if (code.Length != 4 || !code.All(char.IsDigit))
					return false;

				project.AccessCode = BCrypt.Net.BCrypt.HashPassword(code);
			}

			await _context.SaveChangesAsync();
			return true;
		}

		public List<PublicProjectSearchResult> SearchPublicProjects(string? keyword, int take = 20)
		{
			// An empty query must not enumerate every public project — make the
			// contributor type something before anything comes back.
			if (string.IsNullOrWhiteSpace(keyword))
				return [];

			keyword = keyword.Trim();

			if (take is < 1 or > 50)
				take = 20;

			return _context.Projects
				.Where(p => p.IsPublic
					&& !p.Deleted
					&& p.Status == ProjectStatus.Active
					&& p.Title != null
					&& p.Title.Contains(keyword))
				.OrderByDescending(p => p.DateCreated)
				.Take(take)
				.Select(p => new PublicProjectSearchResult
				{
					Id = p.Id,
					Name = p.Title,
					Description = p.Description,
					AmountNeeded = p.AccessCode == null ? p.AmountNeeded : null,
					AmountObtained = p.AccessCode == null ? p.AmountObtained : null,
					HasAccessCode = p.AccessCode != null,
					Type = p.Type.ToString(),
					DateCreated = p.DateCreated,
				})
				.ToList();
		}
	}
}