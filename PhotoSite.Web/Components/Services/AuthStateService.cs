using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace PhotoSite.Web.Components.Services
{
    /// <summary>
    /// Scoped service (per-circuit) that caches admin status for the current session.
    /// </summary>
    public class AuthStateService
    {
        private readonly IHttpClientFactory _httpFactory;
        private bool? _cachedIsAdmin;

        public AuthStateService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        /// <summary>
        /// Returns true if the current user has a valid JWT (i.e. is the photographer/admin).
        /// Result is cached for the lifetime of the Blazor circuit.
        /// </summary>
        public async Task<bool> IsAdminAsync(IJSRuntime js)
        {
            if (_cachedIsAdmin.HasValue)
                return _cachedIsAdmin.Value;

            var token = await js.InvokeAsync<string>("localStorage.getItem", "jwt");
            if (string.IsNullOrWhiteSpace(token))
            {
                _cachedIsAdmin = false;
                return false;
            }

            try
            {
                var client = _httpFactory.CreateClient("API");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync("api/Auth/me");
                _cachedIsAdmin = response.IsSuccessStatusCode;
            }
            catch
            {
                _cachedIsAdmin = false;
            }

            return _cachedIsAdmin.Value;
        }

        /// <summary>
        /// Returns stored JWT token or null.
        /// </summary>
        public static async Task<string?> GetTokenAsync(IJSRuntime js)
            => await js.InvokeAsync<string?>("localStorage.getItem", "jwt");

        /// <summary>
        /// Invalidate cache (call after login/logout).
        /// </summary>
        public void Invalidate() => _cachedIsAdmin = null;
    }
}
