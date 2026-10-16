using Apparcus.Models;
using Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Apparcus.Areas.Api.Controllers
{
	/// <summary>
	/// Stores the picture that goes with a Type-B project — usually a photo of
	/// the sheet the records were copied from. Returns the path, which the
	/// create or edit call then sends with the rest of the project, so those
	/// stay plain JSON.
	///
	/// Both surfaces post here: the website over the Identity cookie and the
	/// app over a JWT.
	/// </summary>
	[Route("api/project-image")]
	[ApiController]
	[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
	public class ProjectImageController : ControllerBase
	{
		private const long MaxBytes = 5 * 1024 * 1024;
		private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

		[HttpPost]
		[RequestSizeLimit(MaxBytes)]
		public async Task<IActionResult> Upload(IFormFile file)
		{
			if (file == null || file.Length == 0)
				return ResponseHelper.JsonError("Choose an image first.");

			if (file.Length > MaxBytes)
				return ResponseHelper.JsonError("That image is larger than 5MB.");

			// Whatever the form claims the type is, the extension is what ends
			// up on disk, so that is what gets checked.
			var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

			if (!AllowedExtensions.Contains(extension))
				return ResponseHelper.JsonError("Use a JPG, PNG, GIF or WEBP image.");

			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "projects");
			Directory.CreateDirectory(uploadsFolder);

			// The stored name is generated, never taken from the upload: a
			// caller-supplied name is a caller-chosen path.
			var fileName = Guid.NewGuid().ToString() + extension;
			var filePath = Path.Combine(uploadsFolder, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			return ResponseHelper.JsonSuccessWithReturnData(new { url = "/uploads/projects/" + fileName });
		}
	}
}
