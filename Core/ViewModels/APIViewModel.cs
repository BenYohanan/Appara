using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace Core.ViewModels
{
	// ---------------------------------------------------------------
	// Auth
	// ---------------------------------------------------------------
	public class LoginRequest
	{
		[Required, EmailAddress]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// The app's per-install identifier. Optional, and never trusted as
		/// proof of anything — an account is limited to one live session
		/// whether or not this is sent. All it decides is the wording: with an
		/// id the server can tell the same phone signing in again from a
		/// second phone, and only says "your other device was signed out"
		/// when it really was another device.
		/// </summary>
		[MaxLength(200)]
		public string? DeviceId { get; set; }

		/// <summary>Human-readable, for "Signed in on iPhone 14".</summary>
		[MaxLength(120)]
		public string? DeviceName { get; set; }
	}

	public class LoginResponse
	{
		public string Token { get; set; } = string.Empty;
		public DateTime ExpiresAt { get; set; }

		// The app is user-facing, so login returns the whole profile rather than
		// just an identity. Without this the client would have to make a second
		// call before it could render anything about the user's money.
		public string UserId { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string? PhoneNumber { get; set; }
		public string? ProfilePictureUrl { get; set; }
		public string Role { get; set; } = string.Empty;

		public bool KycVerified { get; set; }
		public bool TransactionPinSet { get; set; }

		public decimal BeadsBalance { get; set; }
		public decimal PersonalBalance { get; set; }
		public string? PersonalAccountNumber { get; set; }
		public string? PersonalHoldersName { get; set; }

		/// <summary>
		/// True when this login ended a session that was running on a
		/// different device. The app shows it as a notice, because a user who
		/// did not just sign in somewhere else is being told their password is
		/// in someone else's hands.
		/// </summary>
		public bool SignedOutOtherDevice { get; set; }
	}


	/// <summary>
	/// What login answers with when the device is not one the account knows.
	///
	/// Carries no token and nothing secret. Deliberately shaped so the client
	/// cannot mistake it for a session: the code has been mailed, and until it
	/// comes back the account is untouched — still signed in wherever it
	/// already was.
	/// </summary>
	public class DeviceVerificationRequiredResponse
	{
		/// <summary>Always true, so a client can branch on one field.</summary>
		public bool VerificationRequired { get; set; } = true;

		/// <summary>
		/// Where the code went, masked. Enough for the user to know which
		/// inbox to open; not enough to hand the address to someone who only
		/// had the password.
		/// </summary>
		public string MaskedEmail { get; set; } = string.Empty;

		public string Msg { get; set; } = string.Empty;

		/// <summary>Drives the resend countdown in the app.</summary>
		public int ResendAfterSeconds { get; set; }

		public int CodeLength { get; set; }
	}

	/// <summary>
	/// Finishes a login that was held for verification. The email and password
	/// come again with the code: the pending challenge is not, on its own,
	/// permission to open a session.
	/// </summary>
	public class VerifyDeviceRequest
	{
		[Required, EmailAddress]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string Password { get; set; } = string.Empty;

		[Required]
		public string Code { get; set; } = string.Empty;

		/// <summary>
		/// Must be the device the code was issued to, or it is refused — a
		/// code read over a shoulder cannot be spent from somewhere else.
		/// </summary>
		[MaxLength(200)]
		public string? DeviceId { get; set; }

		[MaxLength(120)]
		public string? DeviceName { get; set; }
	}

	public class UserKycInitiateRequest
	{
		public string IdentityType { get; set; }
		public string IdentityNumber { get; set; }
		public string UserId { get; set; }
	}


	public class KycValidateRequest
	{
		public string IdentityId { get; set; }
		public string IdentityType { get; set; }
		public string Otp { get; set; }
		public string UserId { get; set; }
	}

	public class KycSetTransactionPinRequest
	{
		public string? TransactionPin { get; set; }
		public string? UserId { get; set; }
	}

	public class RegisterRequest
	{
		[Required]
		public string FirstName { get; set; } = string.Empty;

		[Required]
		public string LastName { get; set; } = string.Empty;

		[Required, EmailAddress]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string PhoneNumber { get; set; } = string.Empty;

		[Required, MinLength(3)]
		public string Password { get; set; } = string.Empty;

		public DateTime? DateOfBirth { get; set; }

		/// <summary>As on <see cref="LoginRequest"/> — registering signs the
		/// new account in, so it opens a session like any other login.</summary>
		[MaxLength(200)]
		public string? DeviceId { get; set; }

		[MaxLength(120)]
		public string? DeviceName { get; set; }
	}

	public class ForgotPasswordRequest
	{
		[Required, EmailAddress]
		public string Email { get; set; } = string.Empty;
	}

	public class ResetPasswordApiRequest
	{
		[Required]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string Token { get; set; } = string.Empty;

		[Required, MinLength(3)]
		public string NewPassword { get; set; } = string.Empty;
	}

	// ---------------------------------------------------------------
	// Account / profile
	// ---------------------------------------------------------------
	public class ProfileViewModel
	{
		public string Id { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string? PhoneNumber { get; set; }
		public string? ProfilePictureUrl { get; set; }
		public bool KycVerified { get; set; }
		public bool TransactionPinSet { get; set; }
		public decimal BeadsBalance { get; set; }
		public decimal PersonalBalance { get; set; }
		public string? PersonalAccountNumber { get; set; }
		public string? PersonalHoldersName { get; set; }
	}

	public class UpdateProfileRequest
	{
		[Required]
		public string FirstName { get; set; } = string.Empty;

		[Required]
		public string LastName { get; set; } = string.Empty;

		public string? PhoneNumber { get; set; }
	}

	public class SetTransactionPinRequest
	{
		/// <summary>Required only when changing an existing PIN, not when setting the first one.</summary>
		public string? CurrentPin { get; set; }

		[Required, StringLength(6, MinimumLength = 6)]
		public string NewPin { get; set; } = string.Empty;
	}

	/// <summary>
	/// Trades the two things only the account owner knows — the password and
	/// the transaction PIN — for a device key the phone can seal behind its
	/// biometric sensor. Both are required: the PIN alone is short enough to
	/// shoulder-surf, and the password alone is not what authorises money.
	/// </summary>
	public class BiometricEnrollRequest
	{
		[Required]
		public string Password { get; set; } = string.Empty;

		[Required, StringLength(6, MinimumLength = 6)]
		public string TransactionPin { get; set; } = string.Empty;

		/// <summary>Free text shown back to the user, e.g. "Ada's Pixel 8".</summary>
		public string? DeviceLabel { get; set; }
	}

	public class BiometricEnrollResponse
	{
		/// <summary>
		/// Returned exactly once, at enrolment. The server keeps only a hash,
		/// so it can never be shown again — if the phone loses it, the user
		/// enrols afresh.
		/// </summary>
		public string DeviceKey { get; set; } = string.Empty;

		public DateTime ExpiresAt { get; set; }

		/// <summary>
		/// Mirrors the server's own cap so the app can ask for a PIN before
		/// the user fills in a form that was always going to be refused. It is
		/// a hint for the UI, never the thing that enforces the limit.
		/// </summary>
		public decimal MaxAmountWithoutPin { get; set; }
	}

	public class BiometricStatusResponse
	{
		public bool Enrolled { get; set; }
		public string? DeviceLabel { get; set; }
		public DateTime? EnrolledAt { get; set; }
		public DateTime? ExpiresAt { get; set; }
		public decimal MaxAmountWithoutPin { get; set; }
	}

	/// <summary>
	/// Closing the account asks for the password again — the session alone is
	/// not enough authority for something this final.
	/// </summary>
	public class DeleteAccountRequest
	{
		[Required]
		public string Password { get; set; } = string.Empty;
	}

	public class ChangePasswordRequest
	{
		[Required]
		public string CurrentPassword { get; set; } = string.Empty;

		[Required, MinLength(3)]
		public string NewPassword { get; set; } = string.Empty;
	}

	// ---------------------------------------------------------------
	// KYC — adjust method names/payload once you confirm the exact
	// ISafeHavenHelper signatures for BVN/NIN + OTP verification.
	// ---------------------------------------------------------------
	public class KycInitiateRequest
	{
		[Required]
		public string KycType { get; set; } = string.Empty; // "BVN" or "NIN"

		[Required]
		public string Number { get; set; } = string.Empty;
	}

	public class KycVerifyOtpRequest
	{
		[Required]
		public string IdentityId { get; set; } = string.Empty;

		[Required]
		public string Otp { get; set; } = string.Empty;

		[Required]
		public string IdentityType { get; set; } = string.Empty; // "BVN" or "NIN"
	}


	// ---------------------------------------------------------------
	// Generic paged response — IPagedList<T> serializes as a bare JSON
	// array by default (System.Text.Json only sees the IEnumerable<T>
	// side), which silently drops page/totalCount metadata. Wrap every
	// paged endpoint's result in this instead of returning Ok(pagedList).
	// ---------------------------------------------------------------
	public class PagedApiResponse<T>
	{
		public IEnumerable<T> Items { get; set; } = [];
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalCount { get; set; }
		public int TotalPages { get; set; }

		public static PagedApiResponse<T> From(IPagedList<T> paged) => new()
		{
			Items = paged,
			Page = paged.PageNumber,
			PageSize = paged.PageSize,
			TotalCount = paged.TotalItemCount,
			TotalPages = paged.PageCount,
		};
	}
}
