using Microsoft.Extensions.Caching.Memory;

namespace Apparcus.Models
{
	/// <summary>
	/// Shared brute-force guard for project access codes.
	///
	/// Four digits is 10,000 guesses, which a script exhausts in seconds, so
	/// the code is only a real gate if guessing is capped.
	///
	/// The cap is per project per caller, and generous enough that someone
	/// fumbling a digit at the back of a hall is not locked out.
	/// </summary>
	public static class AccessCodeThrottle
	{
		private const int MaxAttempts = 8;
		private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

		private static string KeyFor(int projectId, string? caller) =>
			$"project-code-attempts-{projectId}-{caller}";

		public static bool IsLockedOut(IMemoryCache cache, int projectId, string? caller) =>
			cache.Get<int>(KeyFor(projectId, caller)) >= MaxAttempts;

		public static void RecordFailure(IMemoryCache cache, int projectId, string? caller)
		{
			var key = KeyFor(projectId, caller);
			// Re-sliding the window on each failure is deliberate: a caller who
			// keeps guessing keeps waiting.
			cache.Set(key, cache.Get<int>(key) + 1, Window);
		}

		public static void Clear(IMemoryCache cache, int projectId, string? caller) =>
			cache.Remove(KeyFor(projectId, caller));
	}
}
