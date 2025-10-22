using Decatron.Core.Helpers;
using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Decatron.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;


namespace Decatron.Services
{
    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserRepository _userRepository;
        private readonly TwitchSettings _twitchSettings;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IHttpClientFactory httpClientFactory,
            IUserRepository userRepository,
            IOptions<TwitchSettings> twitchSettings,
            ILogger<AuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _userRepository = userRepository;
            _twitchSettings = twitchSettings.Value;
            _logger = logger;
        }

        public string GetLoginUrl()
        {
            var state = Guid.NewGuid().ToString("N");
            var loginUrl = $"https://id.twitch.tv/oauth2/authorize" +
                $"?client_id={_twitchSettings.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_twitchSettings.RedirectUri)}" +
                $"&response_type=code" +
                $"&scope={Uri.EscapeDataString(_twitchSettings.Scopes)}" +
                $"&state={state}";

            _logger.LogInformation($"Generated login URL for Twitch");
            return loginUrl;
        }

        public async Task<TokenResponse> ExchangeCodeForTokenAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("Authorization code is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _twitchSettings.ClientId },
                    { "client_secret", _twitchSettings.ClientSecret },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", _twitchSettings.RedirectUri }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to exchange code for token: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Failed to obtain token: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                _logger.LogInformation($"Successfully exchanged code for token");
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for token");
                throw;
            }
        }

        public async Task<TwitchUser> GetUserInfoAsync(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("Access token is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("Client-ID", _twitchSettings.ClientId);

                var response = await client.GetAsync("https://api.twitch.tv/helix/users");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get user info: {response.StatusCode}");
                    throw new Exception($"Failed to get user information: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var userResponse = JsonConvert.DeserializeObject<TwitchUserResponse>(content);

                if (userResponse?.Data == null || userResponse.Data.Length == 0)
                {
                    throw new Exception("No user data found");
                }

                _logger.LogInformation($"Retrieved user info for {userResponse.Data[0].Login}");
                return userResponse.Data[0];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user information");
                throw;
            }
        }

        public async Task<User> AuthenticateUserAsync(TwitchUser twitchUser, TokenResponse tokenResponse)
        {
            try
            {
                var user = await _userRepository.GetByTwitchIdAsync(twitchUser.Id);

                if (user != null)
                {
                    // Update existing user
                    user.AccessToken = tokenResponse.AccessToken;
                    user.RefreshToken = tokenResponse.RefreshToken;
                    user.TokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    user.UpdatedAt = DateTime.UtcNow;
                    user.IsActive = true;
                    user.DisplayName = twitchUser.DisplayName;
                    user.Email = twitchUser.Email;
                    user.ProfileImageUrl = twitchUser.ProfileImageUrl;
                    user.OfflineImageUrl = twitchUser.OfflineImageUrl;
                    user.ViewCount = twitchUser.ViewCount;
                    user.Description = twitchUser.Description;

                    user = await _userRepository.UpdateAsync(user);
                    _logger.LogInformation($"Updated existing user: {user.Login}");
                }
                else
                {
                    // Create new user
                    user = new User
                    {
                        TwitchId = twitchUser.Id,
                        Login = twitchUser.Login,
                        DisplayName = twitchUser.DisplayName,
                        Email = twitchUser.Email,
                        ProfileImageUrl = twitchUser.ProfileImageUrl,
                        OfflineImageUrl = twitchUser.OfflineImageUrl,
                        BroadcasterType = twitchUser.BroadcasterType,
                        ViewCount = twitchUser.ViewCount,
                        Description = twitchUser.Description,
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken,
                        TokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true,
                        UniqueId = UniqueIdGenerator.Generate()  
                    };

                    user = await _userRepository.CreateAsync(user);
                    _logger.LogInformation($"Created new user: {user.Login}");
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user");
                throw;
            }
        }

        public async Task<User> GetUserByIdAsync(long userId)
        {
            try
            {
                return await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by ID {userId}");
                return null;
            }
        }

        public async Task<User> GetUserByLoginAsync(string login)
        {
            return await _userRepository.GetByLoginAsync(login);
        }

        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"OAuth {accessToken}");

                var response = await client.GetAsync("https://id.twitch.tv/oauth2/validate");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new ArgumentException("Refresh token is required");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _twitchSettings.ClientId },
                    { "client_secret", _twitchSettings.ClientSecret },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to refresh token: {response.StatusCode}");
                    throw new Exception($"Failed to refresh token: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                _logger.LogInformation("Token refreshed successfully");
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                throw;
            }
        }
    }
}