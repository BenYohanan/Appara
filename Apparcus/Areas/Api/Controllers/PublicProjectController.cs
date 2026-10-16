using Apparcus.Models;
using Core.Models;
using Logic.IHelpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Apparcus.Areas.Api.Controllers
{
	/// <summary>
	/// A project as a contributor sees it — the same thing /Guest/View shows,
	/// as data, so the app can draw it natively instead of sending people to
	/// the website.
	///
	/// Open, like the page it mirrors: a contributor needs no account. The
	/// access code is checked on every call rather than remembered in a
	/// session, because the app keeps no cookie; it holds the code for as long
	/// as the screen is open and sends it back.
	/// </summary>
	[Route("api/public/project")]
	[ApiController]
	[AllowAnonymous]
	public class PublicProjectController(IProjectHelper projectHelper, IMemoryCache cache) : ControllerBase
	{
		private readonly IProjectHelper _projectHelper = projectHelper;
		private readonly IMemoryCache _cache = cache;

		private string? Caller => HttpContext.Connection.RemoteIpAddress?.ToString();

		[HttpGet("{id:int}")]
		public IActionResult Get(int id, [FromQuery] string? code)
		{
			if (_projectHelper.RequiresAccessCode(id))
			{
				if (AccessCodeThrottle.IsLockedOut(_cache, id, Caller))
					return ResponseHelper.JsonError("Too many incorrect codes. Please wait a few minutes and try again.");

				if (!_projectHelper.VerifyAccessCode(id, code))
				{
					// No code yet is not a wrong guess; only count real attempts.
					if (!string.IsNullOrWhiteSpace(code))
						AccessCodeThrottle.RecordFailure(_cache, id, Caller);

					// The name only, as on the web's code page — enough to know
					// this is the right project, nothing that rewards guessing ids.
					return Ok(new
					{
						isError = false,
						requiresCode = true,
						codeRejected = !string.IsNullOrWhiteSpace(code),
						data = new { id, name = _projectHelper.GetPublicProjectById(id)?.Name },
					});
				}

				AccessCodeThrottle.Clear(_cache, id, Caller);
			}

			var project = _projectHelper.GetPublicProjectById(id);
			if (project == null)
				return ResponseHelper.JsonError("This project is no longer available.");

			var isRecordSheet = project.Type == ProjectType.B.ToString();

			return Ok(new
			{
				isError = false,
				requiresCode = false,
				data = new
				{
					project.Id,
					project.Name,
					project.Description,
					project.AmountNeeded,
					project.AmountObtained,
					project.Type,
					project.Status,
					project.RelatedImageUrl,
					project.CreatedBy,
					project.CreatedByPhoneNumber,
					project.OwnersProfilePic,
					project.CustomDate,
					// Only an active money project takes contributions.
					acceptsContributions = !isRecordSheet && project.Status == ProjectStatus.Active.ToString(),
					customFields = project.CustomFields,
					paymentListField = project.PaymentListField,
					payments = (project.ProjectContributions ?? []).Select(c => new
					{
						name = c.ContributorsName,
						c.Amount,
						c.Date,
						c.DisplayValue,
						c.DisplayLabel,
					}),
					// The audit tab, when the owner turned it on.
					withdrawals = project.ShowForAudit && !isRecordSheet
						? (project.Withdrawals ?? []).Select(w => new
						{
							w.Amount,
							w.SentTo,
							w.Status,
							w.CreatedAt,
						})
						: null,
				},
			});
		}
	}
}
