using Apparcus.Models;
using Core.DbContext;
using Core.ViewModels;
using Logic.IHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace Apparcus.Controllers
{
    public class GuestController(IProjectHelper projectHelper, IMemoryCache cache) : Controller
    {
        private readonly IProjectHelper _projectHelper = projectHelper;
        private readonly IMemoryCache _cache = cache;

        private static string SessionKeyFor(int projectId) => $"project-access-{projectId}";

        private string? Caller => HttpContext.Connection.RemoteIpAddress?.ToString();

        private bool HasUnlocked(int projectId) =>
            HttpContext.Session.GetString(SessionKeyFor(projectId)) == "1";

        [HttpGet]
        public IActionResult View(int id)
        {
            // The owner may have put a code on the project. Until it is entered
            // the page does not render at all — not a redacted version of it.
            if (_projectHelper.RequiresAccessCode(id) && !HasUnlocked(id))
                return RedirectToAction(nameof(AccessCode), new { id });

            // Public projects come back with the organiser's email withheld;
            // everything else is unchanged.
            var project = _projectHelper.GetPublicProjectById(id);

            // The view dereferences the model throughout, so a link to a
            // project that has gone should read as gone, not as a server error.
            if (project == null)
                return NotFound();

            return View(project);
        }

        /// <summary>
        /// The "ask your project rep for the code" page. Carries the project's
        /// name only — enough for a contributor to know they are in the right
        /// place, and nothing that would reward guessing an id.
        /// </summary>
        [HttpGet]
        public IActionResult AccessCode(int id)
        {
            if (!_projectHelper.RequiresAccessCode(id))
                return RedirectToAction(nameof(View), new { id });

            if (HasUnlocked(id))
                return RedirectToAction(nameof(View), new { id });

            var project = _projectHelper.GetPublicProjectById(id);

            ViewBag.ProjectId = id;
            ViewBag.ProjectName = project?.Name;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyAccessCode(int id, string? code)
        {
            if (AccessCodeThrottle.IsLockedOut(_cache, id, Caller))
                return ResponseHelper.JsonError("Too many incorrect codes. Please wait a few minutes and try again.");

            if (!_projectHelper.VerifyAccessCode(id, code))
            {
                AccessCodeThrottle.RecordFailure(_cache, id, Caller);
                return ResponseHelper.JsonError("That code is not correct. Please check with your project rep.");
            }

            AccessCodeThrottle.Clear(_cache, id, Caller);
            HttpContext.Session.SetString(SessionKeyFor(id), "1");

            return ResponseHelper.JsonSuccessWithReturnUrl(Url.Action(nameof(View), "Guest", new { id })!);
        }

        /// <summary>
        /// Name search over projects their owners chose to make public.
        /// Deliberately open — a contributor in a congregation needs to find a
        /// project without an account.
        /// </summary>
        [HttpGet]
        public IActionResult Search(string? q)
        {
            var results = _projectHelper.SearchPublicProjects(q);

            return ResponseHelper.JsonSuccessWithReturnData(results.Select(r => new
            {
                r.Id,
                r.Name,
                r.AmountNeeded,
                r.AmountObtained,
                r.HasAccessCode,
                r.Type,
                // Trimmed for the dropdown; the full text is on the project page.
                Description = r.Description != null && r.Description.Length > 120
                    ? r.Description[..120] + "…"
                    : r.Description,
                Url = Url.Action("View", "Guest", new { id = r.Id }),
            }));
        }

        [HttpGet]
        public IActionResult Success(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return Redirect("/");

            return View();
        }

        [HttpPost]
        public IActionResult Comments(string commentContents)
        {
            if (string.IsNullOrEmpty(commentContents))
            {
                return ResponseHelper.JsonError("Invalid comment details");
            }
            var comment = JsonConvert.DeserializeObject<ProjectCommentsViewModel>(commentContents);
            if (comment == null)
            {
                return ResponseHelper.JsonError("comment details could not be processed");
            }
            var isCreated = _projectHelper.AddComments(comment);
            if (isCreated)
            {
                return ResponseHelper.JsonSuccess("Comment sent Successfully");
            }
            return ResponseHelper.JsonError("Failed to create project");
        }
    }
}
