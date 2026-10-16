using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apparcus.Areas.Api.Controllers
{
	[Route("api/notifications")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
	public class NotificationsController : ControllerBase
	{
		private readonly AppDbContext _context;

		public NotificationsController(AppDbContext context)
		{
			_context = context;
		}

		private string CurrentUserId =>
			User.FindFirstValue(ClaimTypes.NameIdentifier)
			?? throw new UnauthorizedAccessException();

		[HttpGet]
		public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
		{
			var query = _context.Notifications
				.Where(n => n.UserId == CurrentUserId)
				.OrderByDescending(n => n.DateCreated);

			var totalCount = await query.CountAsync();
			var items = await query
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return Ok(new PagedApiResponse<Notification>
			{
				Items = items,
				Page = page,
				PageSize = pageSize,
				TotalCount = totalCount,
				TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			});
		}

		[HttpGet("unread-count")]
		public async Task<IActionResult> UnreadCount()
		{
			var count = await _context.Notifications
				.CountAsync(n => n.UserId == CurrentUserId && !n.Read);

			return Ok(new { unreadCount = count });
		}

		[HttpPost("{id:int}/read")]
		public async Task<IActionResult> MarkRead(int id)
		{
			var notification = await _context.Notifications
				.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
			if (notification == null) return NotFound();

			notification.Read = true;
			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("Marked as read");
		}

		[HttpPost("read-all")]
		public async Task<IActionResult> MarkAllRead()
		{
			var unread = await _context.Notifications
				.Where(n => n.UserId == CurrentUserId && !n.Read)
				.ToListAsync();

			foreach (var n in unread)
				n.Read = true;

			await _context.SaveChangesAsync();
			return ResponseHelper.JsonSuccess("All marked as read");
		}
	}
}
