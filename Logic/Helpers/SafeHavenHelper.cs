using Core.Models;
using Core.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Logic.Helpers
{
	public interface ISafeHavenHelper
	{
		Task<(string identityId, string message)> InitiateIdentityVerification(ApplicationUser user, string identityType, string identityNumber);
		Task<(string? identityId, string? message)> ValidateIdentityVerification(string identityId, string identityType, string otp);
        Task<NameEnquiryResponse> NameEnquiry(string accountNumber, string bankCode);
		Task<SafeHavenTransferResponse> InitiateTransfer(string sourceAccount, string beneficiaryAccountNumber, string beneficiaryBankCode, decimal amount, string narration, string externalReference, string nameEnquiryReference);
		Task<List<Bank>> GetBanksAsync();
		Task<SubAccountResponse> CreateSubAccount(ApplicationUser user, string externalReference);
		Task<SafeHavenTransferResponse> RefundTransfer(string beneficiaryAccountNumber, string beneficiaryInstitutionCode, decimal amount, string narration);
		Task<SafeHavenTransferResponse> MoveToProjectOwnerSubAccount(string fromProjectAccountNumber, decimal amount);
		Task<SafeHavenVirtualAccountResponse> CreateVirtualAccount(CreateVirtualAccountRequest request);
		Task<SafeHavenTransferResponse> TransferFromCorporateAccount(string beneficiaryAccountNumber, string beneficiaryBankCode, decimal amount, string narration, string externalReference);
		Task<SafeHavenTransferResponse> InternalTransfer(string fromAccountNumber, string toAccountNumber, decimal amount, string narration, string externalReference);
		Task<SafeHavenTransferResponse> CollectFeeToCorporateAccount(string fromAccountNumber, decimal amount, string narration, string externalReference);
	}

	public class SafeHavenHelper : ISafeHavenHelper
	{
		private readonly IConfiguration _config;
		private readonly HttpClient _client;
		private readonly ILogger<SafeHavenHelper> _logger;

		private readonly IWebHostEnvironment _env;
		private readonly IMemoryCache _cache;

		private const string CacheKey = "nigeria_banks";
		private readonly string ClientId = "";
		private readonly string BaseUrl = "";
		private readonly string DebitAccountNumber = "";
		private readonly string BankCode = Core.Constants.SafeHavenConstants.BankCode;
		private readonly ISafeHavenTokenService _tokenService;

		public SafeHavenHelper(HttpClient client,
			IConfiguration config,
			IWebHostEnvironment env,
			IMemoryCache cache,
			ILogger<SafeHavenHelper> logger,
			ISafeHavenTokenService tokenService)
		{
			_config = config;
			_env = env;
			_cache = cache;
			_client = client;
			_logger = logger;
			_tokenService = tokenService;

			ClientId = _config["SafeHaven:ClientId"] ?? "";
			BaseUrl = _config["SafeHaven:BaseUrl"] ?? "";
			DebitAccountNumber = _config["SafeHaven:DebitAccountNumber"] ?? "";
		}
		
		

		public async Task<(string identityId, string message)> InitiateIdentityVerification(ApplicationUser user, string identityType, string identityNumber)
		{
			try
			{
				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/identity/v2",
					new
					{
						debitAccountNumber = DebitAccountNumber,
						type = identityType,
						number = identityNumber,
						firstName = user.FirstName,
						lastName = user.LastName,
						phoneNumber = user.PhoneNumber,
						emailAddress = user.Email
					});

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception(json);

				var result =
					JsonSerializer.Deserialize<IdentityInitiateResponse>(
						json,
						new JsonSerializerOptions
						{
							PropertyNameCaseInsensitive = true
						});

				return (result?.data?._id, result?.message);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during Initiate Identity Verification");
				throw;
			}
		}

		/// <summary>
		/// Spends the OTP SafeHaven texted to the number behind the NIN or BVN.
		/// </summary>
		/// <returns>
		/// The confirmed identity id on success, or null with SafeHaven's own
		/// reason for declining.
		///
		/// The reason is carried out rather than swallowed: a wrong code, an
		/// expired one and an identity that was already verified are three
		/// different things for the person holding the phone, and telling all
		/// three "OTP verification failed. Please try again." sends someone
		/// retyping a code that was never going to work.
		/// </returns>
		public async Task<(string? identityId, string? message)> ValidateIdentityVerification(string identityId, string identityType, string otp)
		{
			try
			{
				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/identity/v2/validate",
					new
					{
						identityId,
						type = identityType,
						otp
					});

				var json = await response.Content.ReadAsStringAsync();

				var result = TryReadValidateResponse(json);

				// A refusal is an answer, not a fault: SafeHaven declines a bad
				// or stale OTP with a non-2xx and a message, and throwing here
				// turned that into a 500 the app could only render as "something
				// went wrong".
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning(
						"SafeHaven declined an identity OTP. Status={Status} Body={Body}",
						(int)response.StatusCode, json);

					return (null, result?.message);
				}

				// A 2xx with no identity id is also a refusal — the status and
				// message in the body are what say so.
				if (string.IsNullOrEmpty(result?.data?._id))
				{
					_logger.LogWarning(
						"SafeHaven returned no identity id for a validated OTP. Body={Body}", json);

					return (null, result?.message);
				}

				return (result.data._id, result.message);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during Validate Identity Verification");
				throw;
			}
		}

		/// <summary>
		/// Reads the validate response without letting a shape we did not
		/// expect become the error the user sees.
		/// </summary>
		private static IdentityValidateResponse? TryReadValidateResponse(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<IdentityValidateResponse>(
					json,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			}
			catch (JsonException)
			{
				return null;
			}
		}

		public async Task<SafeHavenTransferResponse> InitiateTransfer(string sourceAccount, string beneficiaryAccountNumber, string beneficiaryBankCode, decimal amount, string narration, string externalReference, string nameEnquiryReference)
		{
			try
			{
				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/transfers",
					new
					{
						debitAccountNumber = sourceAccount,
						beneficiaryAccountNumber,
						beneficiaryBankCode,
						amount,
						narration,
						paymentReference = externalReference,
						nameEnquiryReference,
						saveBeneficiary = false
					});

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"SafeHaven Transfer Error: {json}");

				return JsonSerializer.Deserialize<SafeHavenTransferResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during Initiate Transfer");
				throw;
			}
		}

		public async Task<NameEnquiryResponse> NameEnquiry(string accountNumber, string bankCode)
		{
			try
			{
				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/transfers/name-enquiry",
					new
					{
						accountNumber,
						bankCode
					});

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"Name Enquiry Error: {json}");

				return JsonSerializer.Deserialize<NameEnquiryResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting SafeHaven Name Enquiry");
				throw;
			}
		}

		public async Task<SubAccountResponse> CreateSubAccount(ApplicationUser user, string externalReference)
		{
			try
			{
				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/accounts/subaccount",
					new
					{
						phoneNumber = user.PhoneNumber,
						identityType = "vID",
						identityId = user.SafeHavenIdentityId,
						emailAddress = user.Email,
						externalReference,
						autoSweep = false
					});

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception(json);

				return JsonSerializer.Deserialize<SubAccountResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating SafeHaven sub account");
				throw;
			}
		}
		public async Task<List<Bank>> GetBanksAsync()
		{
			return await _cache.GetOrCreateAsync(CacheKey, async entry =>
			{
				entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

				var path = Path.Combine(_env.WebRootPath, "data/nigeria-banks.json");

				if (!File.Exists(path))
					return new List<Bank>();

				var json = await File.ReadAllTextAsync(path);

				var banks = JsonSerializer.Deserialize<List<Bank>>(json,
			   new JsonSerializerOptions
			   {
				   PropertyNameCaseInsensitive = true
			   }) ?? new List<Bank>();

				return banks
					.OrderBy(b => b.name.Trim())
					.ToList();
			});
		}

		public async Task<SafeHavenTransferResponse> RefundTransfer(string beneficiaryAccountNumber, string beneficiaryInstitutionCode, decimal amount, string narration)
		{
			try
			{
				var nameEnquiry = await NameEnquiry(beneficiaryAccountNumber, beneficiaryInstitutionCode);

				if (nameEnquiry?.data?.sessionId == null)
					throw new Exception("Unable to resolve beneficiary account (name enquiry failed).");

				var payload = new
				{
					debitAccountNumber = DebitAccountNumber,

					beneficiaryAccountNumber,

					beneficiaryBankCode = beneficiaryInstitutionCode,

					amount,

					narration,

					nameEnquiryReference =
						nameEnquiry.data.sessionId,

					saveBeneficiary = false
				};

				_logger.LogInformation( $"Refund payload :{JsonSerializer.Serialize(payload)}");

				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/transfers",
					payload);

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"Refund Error: {json}");

				return JsonSerializer.Deserialize<SafeHavenTransferResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error on Refund Transfer");
				throw;
			}
		}

		public async Task<SafeHavenTransferResponse> MoveToProjectOwnerSubAccount(string fromProjectAccountNumber, decimal amount)
		{
			try
			{
				var nameEnquiry = await NameEnquiry(
					fromProjectAccountNumber,
					BankCode);

				if (nameEnquiry?.data?.sessionId == null)
					throw new Exception("Unable to resolve beneficiary account (name enquiry failed).");

				var payload = new
				{
					debitAccountNumber = DebitAccountNumber,

					beneficiaryAccountNumber = fromProjectAccountNumber,

					beneficiaryBankCode = BankCode,

					amount,

					narration = "Project Owner Settlement",

					nameEnquiryReference = nameEnquiry.data.sessionId,

					saveBeneficiary = false
				};

				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/transfers",
					payload);

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"Fee Transfer Error: {json}");

				return JsonSerializer.Deserialize<SafeHavenTransferResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error making platform fee");
				throw;
			}
		}

		private static bool IsTokenError(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			return text.Contains("Expired token", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("Invalid token", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("jwt", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("access token", StringComparison.OrdinalIgnoreCase);
		}

		public async Task<SafeHavenVirtualAccountResponse> CreateVirtualAccount(CreateVirtualAccountRequest request)
		{
			try
			{
				var payload = new
				{
					accountName = request.AccountName,
					externalReference = request.ExternalReference,
					amount = request.Amount,
					amountControl = request.AmountControl,
					validFor = request.ValidFor,
					callbackUrl = request.CallbackUrl,

					settlementAccount = new
					{
						bankCode = BankCode,
						accountNumber = DebitAccountNumber
					}
				};

				var response = await SendSafeHavenRequest(
					HttpMethod.Post,
					$"{BaseUrl}/virtual-accounts",
					payload);

				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"Virtual Account Error: {json}");

				return JsonSerializer.Deserialize<SafeHavenVirtualAccountResponse>(
					json,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating SafeHaven virtual account");
				throw;
			}
		}

		public async Task<SafeHavenTransferResponse> TransferFromCorporateAccount(string beneficiaryAccountNumber, string beneficiaryBankCode, decimal amount, string narration, string externalReference)
		{
			try
			{
				var nameEnquiry = await NameEnquiry(beneficiaryAccountNumber, beneficiaryBankCode);

				if (nameEnquiry?.data?.sessionId == null)
					throw new Exception("Unable to resolve beneficiary account (name enquiry failed).");

				var payload = new
				{
					debitAccountNumber = DebitAccountNumber,
					beneficiaryAccountNumber,
					beneficiaryBankCode,
					amount,
					narration,
					paymentReference = externalReference,
					nameEnquiryReference = nameEnquiry.data.sessionId,
					saveBeneficiary = false
				};

				var response = await SendSafeHavenRequest(HttpMethod.Post, $"{BaseUrl}/transfers", payload);
				var json = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
					throw new Exception($"Corporate Transfer Error: {json}");

				return JsonSerializer.Deserialize<SafeHavenTransferResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error transferring from corporate account");
				throw;
			}
		}

		public async Task<SafeHavenTransferResponse> InternalTransfer(string fromAccountNumber, string toAccountNumber, decimal amount, string narration, string externalReference)
		{
			try
			{
				var nameEnquiry = await NameEnquiry(toAccountNumber, BankCode);

				if (nameEnquiry?.data?.sessionId == null)
					throw new Exception("Unable to resolve destination account for internal transfer.");

				return await InitiateTransfer(fromAccountNumber, toAccountNumber, BankCode, amount, narration, externalReference, nameEnquiry.data.sessionId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during internal transfer from {From} to {To}", fromAccountNumber, toAccountNumber);
				throw;
			}
		}

		public async Task<SafeHavenTransferResponse> CollectFeeToCorporateAccount(string fromAccountNumber, decimal amount, string narration, string externalReference)
		{
			try
			{
				var nameEnquiry = await NameEnquiry(DebitAccountNumber, BankCode);

				if (nameEnquiry?.data?.sessionId == null)
					throw new Exception("Unable to resolve corporate account for fee collection.");

				return await InitiateTransfer(fromAccountNumber, DebitAccountNumber, BankCode, amount, narration, externalReference, nameEnquiry.data.sessionId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error collecting fee from {From}", fromAccountNumber);
				throw;
			}
		}

		private async Task<HttpResponseMessage> SendSafeHavenRequest(HttpMethod method, string url, object body = null)
		{
			async Task<HttpResponseMessage> Send(string token)
			{
				var request = new HttpRequestMessage(method, url);

				request.Headers.Authorization =
					new AuthenticationHeaderValue("Bearer", token);

				request.Headers.Add("ClientID", ClientId);

				if (body != null)
					request.Content = JsonContent.Create(body);

				var response = await _client.SendAsync(request);

				return response;
			}

			var token = await _tokenService.GetAccessToken();

			var response = await Send(token);

			if (!IsAuthFailure(response))
			{
				return response;
			}

			var error = await response.Content.ReadAsStringAsync();

			if (!IsTokenError(error))
			{
				return response;
			}

			token = await _tokenService.GetAccessToken(forceRefresh: true);

			response = await Send(token);

			return response;
		}

		private static bool IsAuthFailure(HttpResponseMessage response)
		{
			return response.StatusCode == HttpStatusCode.Unauthorized
				|| response.StatusCode == HttpStatusCode.Forbidden;
		}
	}
}
