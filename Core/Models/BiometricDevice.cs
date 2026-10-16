using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models
{
	/// <summary>
	/// One phone that has been trusted to authorise withdrawals with a face or
	/// fingerprint instead of a typed transaction PIN.
	///
	/// The phone proves ownership once (password + transaction PIN) and gets a
	/// random key in return. That key — not the PIN — is what it stores and
	/// replays, which is what makes this revocable: killing a lost handset is a
	/// row update here, where making the user change a stored PIN would mean
	/// changing it everywhere they use it.
	///
	/// Only the SHA-256 of the key is kept. A leak of this table hands an
	/// attacker nothing usable.
	/// </summary>
	[Index(nameof(KeyHash))]
	public class BiometricDevice
	{
		[Key]
		public int Id { get; set; }

		[MaxLength(450)]
		public string UserId { get; set; } = string.Empty;

		/// <summary>
		/// Hex SHA-256 of the issued key.
		///
		/// SHA-256 rather than BCrypt on purpose: BCrypt's work factor exists to
		/// slow down guessing of low-entropy secrets people choose themselves,
		/// like the 6-digit PIN. This key is 256 bits from a CSPRNG, so there is
		/// nothing to guess, and a fast hash keeps the withdrawal path quick.
		/// </summary>
		[MaxLength(64)]
		public string KeyHash { get; set; } = string.Empty;

		/// <summary>Shown to the user when they review or revoke devices.</summary>
		[MaxLength(120)]
		public string? DeviceLabel { get; set; }

		public DateTime DateCreated { get; set; } = DateTime.UtcNow;
		public DateTime? LastUsedAt { get; set; }

		/// <summary>Set when revoked; a non-null value disables the key for good.</summary>
		public DateTime? RevokedAt { get; set; }

		/// <summary>
		/// Why it was revoked — "user", "password_changed", "pin_changed". Kept
		/// so support can answer "why did my phone stop working?".
		/// </summary>
		[MaxLength(60)]
		public string? RevokedReason { get; set; }

		/// <summary>
		/// Keys age out even if nobody revokes them, so an abandoned phone
		/// stops being a way in.
		/// </summary>
		public DateTime ExpiresAt { get; set; }

		[NotMapped]
		public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;

		[ForeignKey(nameof(UserId))]
		public virtual ApplicationUser? User { get; set; }
	}
}
