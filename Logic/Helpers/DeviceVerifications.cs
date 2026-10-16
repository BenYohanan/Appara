using System.Security.Cryptography;
using Core.DbContext;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logic.Helpers
{
	/// <summary>
	/// The consent step in front of a sign-in on an unrecognised device.
	///
	/// The one-session rule alone would have made a stolen password more
	/// dangerous than it already is: signing in signs the previous device out,
	/// so anyone holding the password could throw the rightful owner off their
	/// own phone at will, repeatedly, without ever having to stay logged in
	/// themselves. That is a cheaper attack than theft — nothing needs to be
	/// stolen, only denied.
	///
	/// So the takeover is not the login's to perform. A login from a device
	/// the account does not know mints a code to the registered email and
	/// stops there: no token, and — this is the part that matters — nothing
	/// revoked. The device already signed in carries on as if the attempt had
	/// never happened, and carries on if the code is never answered. Only the
	/// person who can read the account's inbox can move the session.
	/// </summary>
	public static class DeviceVerifications
	{
		/// <summary>
		/// Long enough to fetch a phone and read an email, short enough that a
		/// code left sitting in an unattended inbox goes stale.
		/// </summary>
		public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

		/// <summary>
		/// Wrong guesses allowed before the challenge dies and the user has to
		/// start a fresh login.
		///
		/// Six digits is a million combinations, which sounds ample until it
		/// is guessed by a machine. Five attempts against a ten-minute window
		/// leaves an attacker a one-in-two-hundred-thousand chance per
		/// challenge, and every challenge they burn sends the real owner
		/// another email saying someone is trying.
		/// </summary>
		public const int MaxAttempts = 5;

		/// <summary>
		/// How long before a new code can be posted to the same account.
		/// Without it, "resend" is a button that mails a stranger's inbox as
		/// fast as it can be tapped.
		/// </summary>
		public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

		/// <summary>
		/// Whether signing in on this device needs the owner's say-so.
		///
		/// What the code defends is a *takeover*: signing in signs the other
		/// device out, so the question is whether there is a live session this
		/// login would end. If the account has none — its first ever sign-in,
		/// a user who signed out before reinstalling, a session that simply
		/// expired — then nothing is being displaced, nobody is being thrown
		/// off their own phone, and there is nothing for a code to protect.
		/// Those logins go straight through.
		///
		/// Asked against the most recent session *ever* instead, a reinstall
		/// always failed the test: the app mints a new per-install id, so a
		/// user coming back to a phone they had signed out of was sent to
		/// their inbox for no gain. A stale row is not a device anyone is
		/// still using.
		///
		/// What still asks: a live session on a different handset, or one this
		/// caller cannot show it owns. A caller sending no id at all cannot be
		/// matched to the live session, so it reads as somewhere else — the
		/// safe direction, and the one that keeps a stolen password from
		/// evicting the owner at will.
		/// </summary>
		public static async Task<bool> IsDeviceUnrecognisedAsync(
			AppDbContext context, string userId, string? deviceId)
		{
			var now = DateTime.UtcNow;

			var live = await context.UserSessions
				.Where(s => s.UserId == userId
					&& s.RevokedAt == null
					&& s.ExpiresAt > now)
				.Select(s => s.DeviceId)
				.ToListAsync();

			if (live.Count == 0)
				return false;

			var thisDevice = Normalise(deviceId, 200);

			if (thisDevice == null)
				return true;

			// Recognised when the device already holds one of the live
			// sessions — that is the same phone signing in again, which
			// displaces only itself.
			return !live.Any(id => string.Equals(id, thisDevice, StringComparison.Ordinal));
		}

		/// <summary>
		/// Raises a challenge and returns the plaintext code for the email —
		/// the only moment it exists in readable form. The caller still has to
		/// <c>SaveChangesAsync</c>.
		/// </summary>
		/// <returns>
		/// The challenge and its code, or <c>null</c> when the last code went
		/// out too recently to send another. A null is not an error the caller
		/// should surface as one: the earlier code is still in the inbox and
		/// still valid.
		/// </returns>
		public static async Task<(DeviceVerification challenge, string code)?> IssueAsync(
			AppDbContext context,
			string userId,
			string? deviceId,
			string? deviceLabel,
			string? ipAddress)
		{
			var now = DateTime.UtcNow;

			var outstanding = await context.DeviceVerifications
				.Where(v => v.UserId == userId
					&& v.ConsumedAt == null
					&& v.SupersededAt == null)
				.ToListAsync();

			var lastSent = outstanding
				.Where(v => v.ExpiresAt > now)
				.OrderByDescending(v => v.DateCreated)
				.FirstOrDefault();

			if (lastSent != null && now - lastSent.DateCreated < ResendCooldown)
				return null;

			// Only the newest code works. Leaving older ones live would mean a
			// user who asked twice has two valid codes in their inbox, and an
			// attacker two chances to catch one.
			foreach (var stale in outstanding)
				stale.SupersededAt = now;

			var code = GenerateCode();

			var challenge = new DeviceVerification
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
				DeviceId = Normalise(deviceId, 200),
				DeviceLabel = Normalise(deviceLabel, 120),
				IpAddress = Normalise(ipAddress, 45),
				DateCreated = now,
				ExpiresAt = now.Add(Lifetime),
			};

			context.DeviceVerifications.Add(challenge);
			return (challenge, code);
		}

		/// <summary>
		/// Spends a code against the device it was raised for.
		///
		/// A wrong guess is counted before anything else, so the cap cannot be
		/// sidestepped by abandoning each attempt. The caller still has to
		/// <c>SaveChangesAsync</c> — including after a failure, or the count
		/// never rises.
		/// </summary>
		public static async Task<DeviceVerificationResult> RedeemAsync(
			AppDbContext context, string userId, string? deviceId, string? code)
		{
			if (string.IsNullOrWhiteSpace(code))
				return DeviceVerificationResult.Invalid;

			var now = DateTime.UtcNow;

			var challenge = await context.DeviceVerifications
				.Where(v => v.UserId == userId
					&& v.ConsumedAt == null
					&& v.SupersededAt == null)
				.OrderByDescending(v => v.DateCreated)
				.FirstOrDefaultAsync();

			if (challenge == null || challenge.ExpiresAt <= now)
				return DeviceVerificationResult.Expired;

			if (challenge.AttemptCount >= MaxAttempts)
				return DeviceVerificationResult.TooManyAttempts;

			// The code is only good from the device that asked for it, so one
			// read over a shoulder or talked out of the user on the phone
			// cannot be spent somewhere else.
			var sameDevice = string.Equals(
				Normalise(deviceId, 200), challenge.DeviceId, StringComparison.Ordinal);

			if (!sameDevice || !BCrypt.Net.BCrypt.Verify(code.Trim(), challenge.CodeHash))
			{
				challenge.AttemptCount++;

				return challenge.AttemptCount >= MaxAttempts
					? DeviceVerificationResult.TooManyAttempts
					: DeviceVerificationResult.Invalid;
			}

			challenge.ConsumedAt = now;
			return DeviceVerificationResult.Verified;
		}

		/// <summary>
		/// Drops any outstanding challenge. Used once a session is granted, so
		/// a code that was in flight cannot be spent afterwards.
		/// </summary>
		public static async Task SupersedeAllAsync(AppDbContext context, string userId)
		{
			var outstanding = await context.DeviceVerifications
				.Where(v => v.UserId == userId
					&& v.ConsumedAt == null
					&& v.SupersededAt == null)
				.ToListAsync();

			foreach (var challenge in outstanding)
				challenge.SupersededAt = DateTime.UtcNow;
		}

		/// <summary>
		/// Six digits from the OS CSPRNG, leading zeros kept.
		///
		/// <see cref="RandomNumberGenerator"/> rather than <c>Random</c>: the
		/// cheap generator is seeded predictably enough that codes minted in
		/// the same moment can be reproduced, which would hand an attacker the
		/// answer without a single guess.
		/// </summary>
		public static string GenerateCode() =>
			RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

		/// <summary>
		/// Enough of the address to recognise, not enough to learn. The app
		/// shows it so the user knows which inbox to open; someone holding
		/// only the password should not be handed the address itself.
		/// </summary>
		public static string MaskEmail(string? email)
		{
			if (string.IsNullOrWhiteSpace(email))
				return string.Empty;

			var at = email.IndexOf('@');
			if (at <= 0)
				return "***";

			var name = email[..at];
			var domain = email[at..];

			var visible = name.Length <= 2 ? name[..1] : name[..2];
			return $"{visible}{new string('*', Math.Max(3, name.Length - visible.Length))}{domain}";
		}

		private static string? Normalise(string? value, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			var trimmed = value.Trim();
			return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
		}
	}

	public enum DeviceVerificationResult
	{
		/// <summary>Code accepted; the caller may now open a session.</summary>
		Verified,

		/// <summary>Wrong code, or the right code from the wrong device.</summary>
		Invalid,

		/// <summary>Nothing outstanding, or it ran out of time.</summary>
		Expired,

		/// <summary>Guessed at too often; this challenge is finished.</summary>
		TooManyAttempts,
	}
}
