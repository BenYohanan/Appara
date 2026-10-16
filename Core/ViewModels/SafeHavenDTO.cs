using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Core.ViewModels
{
	public class NameEnquiryResponse { public bool status { get; set; } public string message { get; set; } public NameEnquiryData data { get; set; } }
	public class NameEnquiryData { public string accountNumber { get; set; } public string accountName { get; set; } public string bankCode { get; set; } public string sessionId { get; set; } }
	public class IdentityResponse { public bool status { get; set; } public IdentityData data { get; set; } }
	public class IdentityData { public string id { get; set; } }
	public class TokenResponse { public string access_token { get; set; } public int expires_in { get; set; } }
	// message and statusCode matter as much as the data here: when SafeHaven
	// declines an OTP it says why — expired, already used, wrong code — and
	// without somewhere to put that, every refusal read as the same
	// "verification failed".
	public class IdentityValidateResponse { public bool status { get; set; } public int statusCode { get; set; } public string message { get; set; } public IdentityValidateData data { get; set; } }
	public class IdentityValidateData { public string _id { get; set; } }
	public class IdentityInitiateResponse { public bool status { get; set; } public IdentityInitiateData data { get; set; } public string message { get; set; } }
	public class IdentityInitiateData { public string _id { get; set; } }
	public class SafeHavenTransferResponse { public bool status { get; set; } public string message { get; set; } public SafeHavenTransferData data { get; set; } }
	public class SafeHavenTransferData { public string reference { get; set; } public string status { get; set; } }
	public class Bank
	{
		public string name { get; set; } = null!;

		[JsonPropertyName("bankCode")]
		public string code { get; set; } = null!;
	}

	public class SubAccountResponse
	{
		public bool status { get; set; }
		public SubAccountData data { get; set; }
	}

	public class SubAccountData
	{
		public string? accountNumber { get; set; }
		public string? accountName { get; set; }
		public string? _id { get; set; }
		public string? externalReference { get; set; }
	}
	public class WithdrawRequestDto
	{
		public int ProjectId { get; set; }
		public decimal Amount { get; set; }
	}

	public class SafeHavenWebhookModel
	{
		public string? EventType { get; set; }
		public string? Type { get; set; }
		public SafeHavenTransactionData? Data { get; set; }
	}

	public class SafeHavenTransactionData
	{
		public string? Id { get; set; }
		public string? Client { get; set; }
		public string? Account { get; set; }

		public string? Type { get; set; }
		public string? SessionId { get; set; }

		public string? PaymentReference { get; set; }

		public string? CreditAccountName { get; set; }
		public string? CreditAccountNumber { get; set; }

		public string? DebitAccountName { get; set; }
		public string? DebitAccountNumber { get; set; }
		public string? DestinationInstitutionCode { get; set; }

		public decimal Amount { get; set; }
		public decimal Fees { get; set; }
		public decimal Vat { get; set; }

		public string? ResponseCode { get; set; }
		public string? ResponseMessage { get; set; }
		public string? Status { get; set; }
		public string? ExternalReference { get; set; }

		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}

	public class SafeHavenVirtualAccountResponse
	{
		public string message { get; set; }
		public bool status { get; set; }
		public VirtualAccountData data { get; set; }
	}

	public class VirtualAccountData
	{
		public string _id { get; set; }
		public string accountNumber { get; set; }
		public string accountName { get; set; }
		public string bankName { get; set; }
		public string externalReference { get; set; }
		public decimal amount { get; set; }
	}

	public class CreateVirtualAccountRequest
	{
		public string AccountName { get; set; }
		public string ExternalReference { get; set; }
		public decimal Amount { get; set; }
		public string AmountControl { get; set; } = "Fixed";
		public int ValidFor { get; set; } = 1200;
		public string CallbackUrl { get; set; }
	}
}
