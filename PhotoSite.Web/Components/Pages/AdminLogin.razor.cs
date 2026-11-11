using Microsoft.JSInterop;

namespace PhotoSite.Web.Components.Pages
{
    public partial class AdminLogin
    {
        LoginModel loginModel = new();
        string? error;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // Проверяем токен в localStorage
            var token = await JS.InvokeAsync<string>("localStorage.getItem", "jwt");
            if (!string.IsNullOrEmpty(token))
            {
                // Если токен есть — сразу редиректим на админку
                Nav.NavigateTo("/admin", forceLoad: true);
            }

            StateHasChanged();

        }

        async Task HandleLogin()
        {
            var http = HttpFactory.CreateClient("API");
            var response = await http.PostAsJsonAsync("api/Auth/login", loginModel);

            if (!response.IsSuccessStatusCode)
            {
                error = "Неверный логин или пароль";
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            // Сохраняем токен сразу в localStorage
            await JS.InvokeVoidAsync("localStorage.setItem", "jwt", result!.Token);

            // Перенаправление
            Nav.NavigateTo("/admin");
        }

        class LoginModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }

        class TokenResponse
        {
            public string Token { get; set; } = "";
        }
    }
}
