using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using PhotoSite.Web.Components.Services;

namespace PhotoSite.Web.Components.UI;

public partial class AppNavbar : ComponentBase, IDisposable
{
    [Inject] private AuthStateService AuthState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _drawerOpen;
    private bool _isAdmin;

    protected override void OnInitialized()
    {
        Nav.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (!_drawerOpen) return;
        _drawerOpen = false;
        _ = InvokeAsync(StateHasChanged);
    }

    private int _brandSecretClicks;
    private DateTime _brandSecretLastUtc;
    private CancellationTokenSource? _brandHomeDebounceCts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _isAdmin = await AuthState.IsAdminAsync(JS);
        StateHasChanged();
    }

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    private void CloseDrawer() => _drawerOpen = false;

    /// <summary>
    /// Five consecutive clicks within 2s each → /login (hidden). Single click → home after a short quiet period.
    /// </summary>
    private void OnBrandTitleClick()
    {
        var now = DateTime.UtcNow;
        if (now - _brandSecretLastUtc > TimeSpan.FromSeconds(2))
            _brandSecretClicks = 0;

        _brandSecretLastUtc = now;
        _brandSecretClicks++;

        _brandHomeDebounceCts?.Cancel();
        _brandHomeDebounceCts?.Dispose();
        _brandHomeDebounceCts = new CancellationTokenSource();

        if (_brandSecretClicks >= 5)
        {
            _brandSecretClicks = 0;
            _brandHomeDebounceCts.Cancel();
            _brandHomeDebounceCts.Dispose();
            _brandHomeDebounceCts = null;
            CloseDrawer();
            Nav.NavigateTo("/login");
            return;
        }

        var ct = _brandHomeDebounceCts.Token;
        _ = DebouncedNavigateHomeIfSingleClickAsync(ct);
    }

    private async Task DebouncedNavigateHomeIfSingleClickAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(700, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_brandSecretClicks == 1)
            Nav.NavigateTo("/");

        _brandSecretClicks = 0;
    }

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        _brandHomeDebounceCts?.Cancel();
        _brandHomeDebounceCts?.Dispose();
    }
}
