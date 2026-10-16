using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	/// <summary>
	/// A pending "is this really you?" challenge, standing between a correct
	/// password on an unrecognised device and a session on it.
	///
	/// It exists because the one-session rule has a sharp edge: signing in
	/// signs the previous device out. Without this, a password on its own
	/// would be enough to throw the rightful owner out of their own account
	/// over and over — a stolen password stops being a way in and becomes a
	/// way to lock someone out. So the takeover is held here until whoever is
	/// asking proves they can read the account's email.
	///
	/// Nothing is revoked while a challenge is outstanding. The device that
	/// was already signed in keeps working, and keeps working if the challenge
	/// is never answered.
	/// </summary>
	[Index(nameof(UserId))]
	public class DeviceVerification
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		[MaxLength(450)]
		public string UserId { get; set; } = string.Empty;

		/// <summary>
		/// BCrypt of the emailed code.
		///
		/// BCrypt rather than the SHA-256 used for device keys, and for the
		/// opposite reason: six digits is a million guesses, small enough that
		/// a fast hash over a leaked table would fall in seconds. The work
		/// factor is what makes the short code safe to store. <see
		/// cref="AttemptCount"/> is what makes it safe to expose.
		/// </summary>
		[MaxLength(100)]
		public string CodeHash { get; set; } = string.Empty;

		/// <summary>
		/// The device this challenge was raised for, and the only one it can
		/// let in. Binding it means a code phished out of the user cannot be
		/// spent from a third handset that was not part of the exchange.
		/// </summary>
		[MaxLength(200)]
		public string? DeviceId { get; set; }

		[MaxLength(120)]
		public string? DeviceLabel { get; set; }

		/// <summary>Shown in the email, so the owner can judge whether the
		/// attempt looks like them.</summary>
		[MaxLength(45)]
		public string? IpAddress { get; set; }

		public DateTime DateCreated { get; set; } = DateTime.UtcNow;

		/// <summary>Short on purpose — a code that outlives the moment the
		/// user is standing there reading it is only a longer window for
		/// someone else to use it.</summary>
		public DateTime ExpiresAt { get; set; }

		/// <summary>
		/// Wrong guesses so far. Past the cap the challenge is dead and a new
		/// login is needed, which is what keeps a million-guess space out of
		/// reach of anyone who cannot read the inbox.
		/// </summary>
		public int AttemptCount { get; set; }

		/// <summary>Set the moment the code is spent; single use.</summary>
		public DateTime? ConsumedAt { get; set; }

		/// <summary>
		/// Set when a newer challenge replaces this one, so only the most
		/// recent code in the user's inbox works.
		/// </summary>
		public DateTime? SupersededAt { get; set; }

		[NotMapped]
		public bool IsPending =>
			ConsumedAt == null && SupersededAt == null && ExpiresAt > DateTime.UtcNow;

		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }
	}
}
