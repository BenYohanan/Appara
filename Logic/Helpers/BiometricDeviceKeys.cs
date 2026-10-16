using System.Security.Cryptography;
using Core.DbContext;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logic.Helpers
{
	/// <summary>
	/// Issues and checks the keys that let a trusted phone authorise a
	/// withdrawal with a fingerprint or face instead of a typed PIN.
	///
	/// The rule this exists to enforce: a device key is a *convenience
	/// credential*, not a replacement for the PIN. It is narrower on purpose —
	/// it only works below <see cref="MaxAmountWithoutPin"/>, it dies when the
	/// password or PIN changes, and it can be revoked one handset at a time.
	/// Anything outside those bounds still requires the PIN the user types.
	/// </summary>
	public static class BiometricDeviceKeys
	{
		/// <summary>
		/// The most that may leave an account on a biometric alone. Above this
		/// the user types their PIN, however the app is configured.
		///
		/// Enforced here, server-side, because the app's own copy of this
		/// number is only a hint for laying out the form — a caller that skips
		/// the app entirely must still hit this wall.
		/// </summary>
		public const decimal MaxAmountWithoutPin = 50_000m;

		/// <summary>
		/// How long an unused key stays good. An abandoned phone stops being a
		/// way in without anyone having to remember to revoke it.
		/// </summary>
		public static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

		/// <summary>
		/// 256 bits from the OS CSPRNG, URL-safe so it survives JSON and
		/// headers unmangled.
		/// </summary>
		public static string GenerateKey()
		{
			var bytes = RandomNumberGenerator.GetBytes(32);
			return Convert.ToBase64String(bytes)
				.Replace('+', '-')
				.Replace('/', '_')
				.TrimEnd('=');
		}

		/// <summary>
		/// Lowercase hex SHA-256. Only this ever reaches the database, so a
		/// dump of the table cannot be replayed against the withdrawal
		/// endpoints.
		/// </summary>
		public static string Hash(string key)
		{
			var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
			return Convert.ToHexString(digest).ToLowerInvariant();
		}

		/// <summary>
		/// Resolves a key presented at a withdrawal.
		///
		/// Scoped to <paramref name="userId"/> deliberately: the key is matched
		/// against the caller's own devices, so a key belonging to someone else
		/// — one left behind on a shared handset, say — resolves to nothing
		/// rather than moving another person's money.
		/// </summary>
		public static async Task<BiometricDevice?> ResolveActiveDeviceAsync(
			AppDbContext context, string userId, string? presentedKey)
		{
			if (string.IsNullOrWhiteSpace(presentedKey))
				return null;

			var hash = Hash(presentedKey);
			var now = DateTime.UtcNow;

			return await context.BiometricDevices.FirstOrDefaultAsync(d =>
				d.UserId == userId &&
				d.KeyHash == hash &&
				d.RevokedAt == null &&
				d.ExpiresAt > now);
		}

		/// <summary>
		/// Kills every key a user holds. Called when the password or the
		/// transaction PIN changes: both are the credentials that were traded
		/// for those keys in the first place, so changing either — the thing
		/// someone does when they fear their account is compromised — has to
		/// take the shortcuts down with it.
		/// </summary>
		public static async Task<int> RevokeAllForUserAsync(
			AppDbContext context, string userId, string reason)
		{
			var active = await context.BiometricDevices
				.Where(d => d.UserId == userId && d.RevokedAt == null)
				.ToListAsync();

			foreach (var device in active)
			{
				device.RevokedAt = DateTime.UtcNow;
				device.RevokedReason = reason;
			}

			return active.Count;
		}
	}
}
