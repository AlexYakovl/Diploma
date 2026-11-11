using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using PhotoSite.Web.Components.Services;

namespace PhotoSite.Web.Components.Pages
{
    public partial class Home : ComponentBase, IAsyncDisposable
    {
        [Inject] private ApiService Api { get; set; } = default!;
        [Inject] private AuthStateService AuthState { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;

        private DotNetObjectReference<Home>? _dotnetRef;

        private record Stat(string Value, string Label);

        private readonly List<Stat> _stats = new()
        {
            new("10+",  "лет опыта"),
            new("500+", "съёмок"),
            new("12k+", "кадров"),
            new("30+",  "проектов"),
        };

        private bool _isAdmin;
        private bool _aboutPhotoExists;
        private bool _aboutPhotoUploading;
        private long _aboutPhotoCacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private ElementReference _aboutMePhotoInputRef;

        private double _aboutMePhotoUploadPct;
        private string _aboutMePhotoUploadLabel = "";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            _dotnetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("photosite.homeHeader.init", _dotnetRef);

            _isAdmin = await AuthState.IsAdminAsync(JS);
            if (_isAdmin)
                _aboutPhotoExists = await Api.AboutMePhotoExistsAsync();

            StateHasChanged();
        }

        [JSInvokable]
        public void OnHomeHeaderVisibility(bool visible)
        {
            // no-op: CSS class drives visibility; this hook keeps future flexibility.
        }

        [JSInvokable]
        public void OnAboutMePhotoUploadProgress(long loaded, long total)
        {
            if (total > 0)
                _aboutMePhotoUploadPct = Math.Clamp((double)loaded / total * 100.0, 0, 100);
            else
                _aboutMePhotoUploadPct = Math.Clamp(Math.Log10(Math.Max(1, loaded)) * 22.0, 10, 92);
            _aboutMePhotoUploadLabel = $"Загрузка фото · {_aboutMePhotoUploadPct:0}%";
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task PickAboutMePhotoAsync()
        {
            if (!_isAdmin || _aboutPhotoUploading)
                return;
            await JS.InvokeVoidAsync("photosite.clickById", "aboutMePhotoUpload");
        }

        private async Task AboutMePhotoNativeChanged()
        {
            if (!_isAdmin)
                return;

            var name = await JS.InvokeAsync<string>("photosite.inputFirstFileName", _aboutMePhotoInputRef);
            if (string.IsNullOrWhiteSpace(name))
                return;

            _aboutPhotoUploading = true;
            _aboutMePhotoUploadPct = 0;
            _aboutMePhotoUploadLabel = "Подготовка…";
            StateHasChanged();
            try
            {
                if (_dotnetRef is null)
                    return;

                var token = await Api.GetJwtFromBrowserAsync(JS) ?? "";
                var r = await Api.UploadMultipartFromBrowserAsync(
                    JS,
                    "POST",
                    "/api/AdminMaintenance/aboutme-photo",
                    Array.Empty<(string, string)>(),
                    _aboutMePhotoInputRef,
                    "file",
                    _dotnetRef,
                    nameof(OnAboutMePhotoUploadProgress),
                    token);

                if (r.Ok)
                {
                    _aboutPhotoExists = true;
                    _aboutPhotoCacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Snackbar.Add("Фото обновлено.", Severity.Success);
                }
                else
                    Snackbar.Add($"Не удалось загрузить фото (HTTP {r.Status}).", Severity.Error);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Не удалось загрузить фото: {ex.Message}", Severity.Error);
            }
            finally
            {
                try
                {
                    await Api.ClearBrowserFileInputAsync(JS, _aboutMePhotoInputRef);
                }
                catch
                {
                    /* ignore */
                }

                _aboutMePhotoUploadPct = 0;
                _aboutMePhotoUploadLabel = "";
                _aboutPhotoUploading = false;
                StateHasChanged();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("photosite.homeHeader.dispose");
            }
            catch
            {
                // ignore (navigation / disconnect)
            }
            _dotnetRef?.Dispose();
        }
    }
}
