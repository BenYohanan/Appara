using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	/// <summary>
	/// One signed-in device. An account is allowed exactly one of these alive
	/// at a time — signing in somewhere new ends the session that was running
	/// wherever the user was before.
	///
	/// The row, not the token, is what says a session is still good. A JWT is
	/// self-contained and cannot be recalled once handed out, so without
	/// something server-side to check against, a token issued to a phone the
	/// user has since replaced would keep working until it expired on its own
	/// — a week, by the default <c>Jwt:ExpiryMinutes</c>. Each token carries
	/// the id of the session it was minted for and every authenticated request
	/// looks it up here, so revoking is a single row update that takes effect
	/// on the very next call.
	/// </summary>
	[Index(nameof(UserId))]
	public class UserSession
	{
		/// <summary>
		/// Also the <c>sid</c> claim inside the token. A GUID rather than a
		/// sequence: it travels in a token a client holds, so it must not be
		/// guessable or let anyone count the accounts on the platform.
		/// </summary>
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		[MaxLength(450)]
		public string UserId { get; set; } = string.Empty;

		/// <summary>
		/// The app's own per-install identifier, when it sends one.
		///
		/// Only used to tell "the same phone signing in again" from "a second
		/// phone": the first replaces its own session quietly, the second is
		/// what the user is told about. Never trusted as proof of anything —
		/// a caller can send whatever it likes here, which is why it gates
		/// wording and not access.
		/// </summary>
		[MaxLength(200)]
		public string? DeviceId { get; set; }

		/// <summary>Shown to the user: "Signed in on iPhone 14".</summary>
		[MaxLength(120)]
		public string? DeviceLabel { get; set; }

		[MaxLength(45)]
		public string? IpAddress { get; set; }

		public DateTime DateCreated { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// Refreshed at most once an hour, not on every request: this is here
		/// to answer "when was this phone last used", and a write on every
		/// call would put a row update in front of every read in the app.
		/// </summary>
		public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

		/// <summary>Set when the session ends; a non-null value kills it for good.</summary>
		public DateTime? RevokedAt { get; set; }

		/// <summary>
		/// Why it ended — "signed_in_elsewhere", "logout", "password_changed",
		/// "password_reset". Kept so support can answer "why was I signed
		/// out?" without guessing.
		/// </summary>
		[MaxLength(60)]
		public string? RevokedReason { get; set; }

		/// <summary>
		/// Mirrors the expiry of the token issued for this session, so an
		/// abandoned row stops being usable at the same moment the token does.
		/// </summary>
		public DateTime ExpiresAt { get; set; }

		[NotMapped]
		public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;

		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }
	}
}
