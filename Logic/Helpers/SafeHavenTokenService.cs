using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Logic.Helpers
{
	public interface ISafeHavenTokenService
	{
		Task<string> GetAccessToken(bool forceRefresh = false);
	}

	public class SafeHavenTokenService : ISafeHavenTokenService
	{
		private const string AccessTokenCacheKey = "SafeHaven_AccessToken";
		private const string AssertionCacheKey = "SafeHaven_ClientAssertion";

		private static readonly SemaphoreSlim _lock = new(1, 1);

		private readonly IConfiguration _config;
		private readonly IMemoryCache _cache;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<SafeHavenTokenService> _logger;

		private readonly string _clientId;
		private readonly string _baseUrl;
		private readonly RSA _rsa;

		public SafeHavenTokenService(
			IConfiguration config,
			IMemoryCache cache,
			IHttpClientFactory httpClientFactory,
			ILogger<SafeHavenTokenService> logger,
			IWebHostEnvironment env)
		{
			_config = config;
			_cache = cache;
			_httpClientFactory = httpClientFactory;
			_logger = logger;

			_clientId = _config["SafeHaven:ClientId"] ?? "";
			_baseUrl = _config["SafeHaven:BaseUrl"] ?? "";

			var path = Path.Combine(env.ContentRootPath, "keys", "privatekey.pem");

			if (!File.Exists(path))
				throw new FileNotFoundException("Private key not found", path);

			_rsa = RSA.Create();
			_rsa.ImportFromPem(File.ReadAllText(path));
		}

		public async Task<string> GetAccessToken(bool forceRefresh = false)
		{
			if (!forceRefresh && TryGetValidAccessToken(out var cached))
				return cached.AccessToken;

			await _lock.WaitAsync();

			try
			{
				if (!forceRefresh && TryGetValidAccessToken(out cached))
					return cached.AccessToken;

				var token = await RequestAccessToken();

				_cache.Set(
					AccessTokenCacheKey,
					token,
					new MemoryCacheEntryOptions
					{
						AbsoluteExpiration = token.ExpiresAtUtc
					});

				return token.AccessToken;
			}
			finally
			{
				_lock.Release();
			}
		}

		private bool TryGetValidAccessToken(out SafeHavenTokenResponse token)
		{
			token = null;

			if (!_cache.TryGetValue(AccessTokenCacheKey, out SafeHavenTokenResponse cached))
				return false;

			if (cached?.AccessToken == null)
				return false;

			token = cached;

			return DateTimeOffset.UtcNow < cached.ExpiresAtUtc.AddMinutes(-2);
		}

		private async Task<SafeHavenTokenResponse> RequestAccessToken()
		{
			var client = _httpClientFactory.CreateClient("SafeHaven");

			var assertion = GetClientAssertion();

			var content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "client_credentials",
				["client_id"] = _clientId,
				["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
				["client_assertion"] = assertion
			});

			using var response = await client.PostAsync($"{_baseUrl}/oauth2/token", content);
			var json = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError($"SafeHaven token failed: {json}");
				throw new Exception("Token request failed");
			}

			var token = JsonSerializer.Deserialize<SafeHavenTokenResponse>(
				json,
				new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

			if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
				throw new Exception("Invalid token response");

			token.ExpiresAtUtc =
				DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

			_logger.LogInformation(
				$"Token Issued At UTC={DateTimeOffset.UtcNow} | Expires At UTC={token.ExpiresAtUtc}",
				DateTimeOffset.UtcNow,
				token.ExpiresAtUtc);

			return token;
		}

		private string GetClientAssertion()
		{
			if (_cache.TryGetValue(AssertionCacheKey, out string cached))
				return cached;

			var jwt = GenerateClientAssertion();

			var handler = new JwtSecurityTokenHandler();
			var parsed = handler.ReadJwtToken(jwt);

			var exp = DateTimeOffset.FromUnixTimeSeconds(
				long.Parse(parsed.Payload.Exp.ToString()));

			_cache.Set(AssertionCacheKey, jwt, exp);

			_logger.LogWarning(
				$"SafeHaven Client Assertion cached until {exp}",
				exp);

			return jwt;
		}

		private string GenerateClientAssertion()
		{
			var key = new RsaSecurityKey(_rsa)
			{
				KeyId = Guid.NewGuid().ToString()
			};

			var now = DateTimeOffset.UtcNow;

			var descriptor = new SecurityTokenDescriptor
			{
				Issuer = _clientId,
				Audience = _baseUrl,

				Subject = new ClaimsIdentity(
				[
					new Claim(JwtRegisteredClaimNames.Sub, _clientId),
					new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
				]),

				IssuedAt = now.AddMinutes(-2).UtcDateTime,
				NotBefore = now.AddMinutes(-2).UtcDateTime,
				Expires = now.AddMonths(1).UtcDateTime,

				SigningCredentials = new SigningCredentials(
					key,
					SecurityAlgorithms.RsaSha256
				)
			};

			var handler = new JwtSecurityTokenHandler();
			var token = handler.CreateToken(descriptor);

			var jwt = handler.WriteToken(token);

			_logger.LogWarning(
				$"Client Assertion Generated | iat={descriptor.IssuedAt} exp={descriptor.Expires}",
				descriptor.IssuedAt,
				descriptor.Expires);

			return jwt;
		}

		public sealed class SafeHavenTokenResponse
		{
			[JsonPropertyName("access_token")]
			public string? AccessToken { get; set; }
			[JsonPropertyName("expires_in")] 
			public int ExpiresIn { get; set; }
			public DateTimeOffset ExpiresAtUtc { get; set; }
		}
	}
}