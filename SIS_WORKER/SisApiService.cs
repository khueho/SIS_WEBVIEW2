using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SIS_WORKER
{
    public class DiagnosticImagingPatientDto
    {
        [JsonPropertyName("patientCode")]
        public string PatientCode { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("sentDate")]
        public string SentDate { get; set; } = string.Empty;
    }

    public class TokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;
    }

    public class DiagnosticImagingApiResponseDto
    {
        [JsonPropertyName("data")]
        public List<DiagnosticImagingPatientDto> Data { get; set; } = new List<DiagnosticImagingPatientDto>();
    }

    public class SisApiService
    {
        private readonly HttpClient _httpClient;
        public string BaseUrl { get; set; } = "https://dhs.sisvietnam.vn";
        public string TenantId { get; set; } = "3a0f747c-57b6-753b-ba21-3987e8e93b9a";
        public string ClientId { get; set; } = "DigitalHealthSolutions_HospitalIntegration";
        public string ClientSecret { get; set; } = "XPWFParRQldtZopqdDFLPYblIzauxuWT";
        public string Scope { get; set; } = "HospitalIntegration";

        private string? _cachedToken;
        private DateTime _tokenExpiryTimeUtc = DateTime.MinValue;

        public SisApiService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// Lấy Bearer token qua OAuth 2.0 Client Credentials (tự động cache và tái sử dụng)
        /// </summary>
        public async Task<string> GetAccessTokenAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTimeUtc.AddMinutes(-5))
            {
                return _cachedToken;
            }

            if (string.IsNullOrWhiteSpace(ClientSecret))
            {
                throw new InvalidOperationException("Chưa cấu hình ClientSecret cho SisApiService!");
            }

            var tokenUrl = $"{BaseUrl.TrimEnd('/')}/connect/token";

            var formData = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "scope", Scope }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Lỗi lấy Token OAuth 2.0 ({(int)response.StatusCode}): {responseContent}");
            }

            var tokenData = JsonSerializer.Deserialize<TokenResponseDto>(responseContent);
            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                throw new InvalidOperationException("Không thể phân tích access_token từ phản hồi hệ thống.");
            }

            _cachedToken = tokenData.AccessToken;
            int validSeconds = tokenData.ExpiresIn > 0 ? tokenData.ExpiresIn : 3600;
            _tokenExpiryTimeUtc = DateTime.UtcNow.AddSeconds(validSeconds);

            return _cachedToken;
        }

        /// <summary>
        /// Truy vấn danh sách bệnh nhân chẩn đoán hình ảnh trong ngày
        /// </summary>
        public async Task<List<DiagnosticImagingPatientDto>> GetTodayDiagnosticImagingPatientsAsync()
        {
            string token = await GetAccessTokenAsync();
            var url = $"{BaseUrl.TrimEnd('/')}/api/portal/his/patients/diagnostic-imaging/today?__tenant={Uri.EscapeDataString(TenantId)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Lỗi gọi API danh sách bệnh nhân ({(int)response.StatusCode}): {content}");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<DiagnosticImagingPatientDto>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var apiResult = JsonSerializer.Deserialize<DiagnosticImagingApiResponseDto>(content, options);
            return apiResult?.Data ?? new List<DiagnosticImagingPatientDto>();
        }
    }
}
