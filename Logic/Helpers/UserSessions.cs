using Core.DbContext;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logic.Helpers
{
	/// <summary>
	/// One account, one signed-in device.
	///
	/// The rule this exists to enforce: at any moment an account has at most
	/// one live session. Signing in on a second handset does not fail — it
	/// takes over, and the handset that was signed in before is signed out on
	/// its very next request.
	///
	/// Takeover rather than refusal is deliberate. Refusing the new login
	/// would mean a user who lost, sold or wiped their phone is locked out of
	/// their own money until support intervenes, and the person best placed to
	/// notice a stranger signing in is the legitimate owner — who gets thrown
	/// out of the app the moment it happens, which is the alarm. What it stops
	/// is the thing the rule is actually about: one set of credentials being
	/// used from two places at once, shared or stolen.
	/// </summary>
	public static class UserSessions
	{
		/// <summary>
		/// How stale <see cref="UserSession.LastSeenAt"/> is allowed to get
		/// before a request pays for a write. Every authenticated call already
		/// reads the row; touching it on each one would turn every read in the
		/// app into a write.
		/// </summary>
		private static readonly TimeSpan LastSeenPrecision = TimeSpan.FromHours(1);

		/// <summary>
		/// The claim carrying the session id inside the JWT.
		///
		/// Deliberately not the conventional "sid": JwtSecurityTokenHandler
		/// rewrites a set of short claim names to their long WS-* forms as it
		/// reads a token, and "sid" is one of them — it would arrive back as
		/// ClaimTypes.Sid and a lookup for "sid" would find nothing, which
		/// fails *open* here. A name outside that map arrives as it was
		/// written. (MapInboundClaims stays on: turning it off would strip the
		/// mapping this codebase already relies on for NameIdentifier.)
		/// </summary>
		public const string SessionClaimType = "session_id";

		/// <summary>
		/// What the API answers with when a token was properly signed but its
		/// session lost to a sign-in somewhere else. The app keys its "signed
		/// in on another device" notice off this, so it must stay in step with
		/// the mobile client.
		/// </summary>
		public const string RevokedResponseCode = "session_revoked";

		/// <summary>
		/// The session is over for some reason other than a takeover — the
		/// user logged out, changed their password, or is holding a token
		/// issued before sessions existed. Worth keeping distinct: telling
		/// someone their account was used elsewhere when it wasn't teaches
		/// them to ignore the one warning that matters.
		/// </summary>
		public const string EndedResponseCode = "session_ended";

		public const string ReasonSignedInElsewhere = "signed_in_elsewhere";
		public const string ReasonLogout = "logout";
		public const string ReasonPasswordChanged = "password_changed";
		public const string ReasonPasswordReset = "password_reset";

		/// <summary>
		/// Opens a session for a login, ending whatever else the account had
		/// open.
		///
		/// The returned session is tracked, not saved: the caller sets
		/// <see cref="UserSession.ExpiresAt"/> from the token it mints against
		/// the new id, then calls <c>SaveChangesAsync</c> once.
		/// </summary>
		/// <returns>
		/// The new session, and whether a *different* device was displaced by
		/// it — so login can tell the user "your other phone has been signed
		/// out" without saying it to someone who simply logged in again on the
		/// same handset.
		/// </returns>
		public static async Task<(UserSession session, bool displacedAnotherDevice)> StartAsync(
			AppDbContext context,
			string userId,
			string? deviceId = null,
			string? deviceLabel = null,
			string? ipAddress = null)
		{
			deviceId = Normalise(deviceId, 200);
			deviceLabel = Normalise(deviceLabel, 120);

			var superseded = await context.UserSessions
				.Where(s => s.UserId == userId && s.RevokedAt == null)
				.ToListAsync();

			// A device that sends no id cannot be recognised on its next
			// login, so it always reads as "somewhere else". Treating an
			// unidentified session as the same phone would be the wrong way
			// round: it would quietly hide a real second sign-in.
			var displacedAnotherDevice = superseded.Any(s =>
				s.ExpiresAt > DateTime.UtcNow &&
				(deviceId == null || s.DeviceId == null || s.DeviceId != deviceId));

			foreach (var session in superseded)
			{
				session.RevokedAt = DateTime.UtcNow;
				session.RevokedReason = ReasonSignedInElsewhere;
			}

			var fresh = new UserSession
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				DeviceId = deviceId,
				DeviceLabel = deviceLabel,
				IpAddress = Normalise(ipAddress, 45),
				DateCreated = DateTime.UtcNow,
				LastSeenAt = DateTime.UtcNow,
			};

			context.UserSessions.Add(fresh);
			return (fresh, displacedAnotherDevice);
		}

		/// <summary>
		/// Decides whether a presented token may still be honoured, and if
		/// not, why — so the client can say something true rather than a
		/// generic "please log in again".
		///
		/// Matched on <paramref name="userId"/> as well as the id, so a token
		/// whose two claims disagree — the shape a forged or spliced token
		/// takes — resolves to nothing.
		/// </summary>
		public static async Task<SessionCheck> CheckAsync(
			AppDbContext context, string? userId, string? sessionId)
		{
			// No session claim at all means a token minted before this rule
			// existed. It is not honoured — that is the point of the rule —
			// but it is not a takeover either, so the user is asked to sign in
			// again rather than warned about a device that never existed.
			if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(sessionId, out var id))
				return SessionCheck.Ended;

			var session = await context.UserSessions
				.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

			if (session == null)
				return SessionCheck.Ended;

			if (session.RevokedAt != null)
			{
				return session.RevokedReason == ReasonSignedInElsewhere
					? SessionCheck.SignedInElsewhere
					: SessionCheck.Ended;
			}

			var now = DateTime.UtcNow;

			if (session.ExpiresAt <= now)
				return SessionCheck.Ended;

			if (now - session.LastSeenAt > LastSeenPrecision)
			{
				session.LastSeenAt = now;
				await context.SaveChangesAsync();
			}

			return SessionCheck.Active;
		}

		/// <summary>
		/// Ends every session the user holds. Called on logout and on any
		/// password change: the credential those sessions were traded for is
		/// gone, so they go with it.
		/// </summary>
		/// <param name="exceptSessionId">
		/// Left alive — the session doing the revoking. A user changing their
		/// password on their own phone means "throw everyone else out", not
		/// "throw me out mid-task".
		/// </param>
		public static async Task<int> RevokeAllForUserAsync(
			AppDbContext context, string userId, string reason, Guid? exceptSessionId = null)
		{
			var live = await context.UserSessions
				.Where(s => s.UserId == userId && s.RevokedAt == null)
				.ToListAsync();

			var revoked = 0;

			foreach (var session in live)
			{
				if (exceptSessionId.HasValue && session.Id == exceptSessionId.Value)
					continue;

				session.RevokedAt = DateTime.UtcNow;
				session.RevokedReason = reason;
				revoked++;
			}

			return revoked;
		}

		private static string? Normalise(string? value, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			var trimmed = value.Trim();
			return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
		}
	}

	/// <summary>The verdict on a token's session — see <see cref="UserSessions.CheckAsync"/>.</summary>
	public enum SessionCheck
	{
		/// <summary>Still the account's one live session; the request proceeds.</summary>
		Active,

		/// <summary>Displaced by a sign-in on another device.</summary>
		SignedInElsewhere,

		/// <summary>Over for any other reason — logout, password change, expiry.</summary>
		Ended,
	}
}
