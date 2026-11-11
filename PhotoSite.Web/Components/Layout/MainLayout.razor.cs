using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;

namespace PhotoSite.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _overlayVisible = true;
    private bool _gateArm;
    private int _gateEpoch;

    private readonly MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0a0a0a",
            PrimaryContrastText = "#ffffff",
            Secondary = "#444444",
            SecondaryContrastText = "#ffffff",
            Background = "#ffffff",
            Surface = "#ffffff",
            AppbarBackground = "rgba(255,255,255,0.96)",
            AppbarText = "#0a0a0a",
            DrawerBackground = "#ffffff",
            DrawerText = "#0a0a0a",
            TextPrimary = "#0a0a0a",
            TextSecondary = "#666666",
            ActionDefault = "#444444",
            Divider = "#e0e0e0",
            TableLines = "#e0e0e0",
            LinesDefault = "#e0e0e0",
            OverlayDark = "rgba(10,10,10,0.6)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" },
                FontSize = "0.9375rem",
                FontWeight = "400",
                LineHeight = "1.65",
                LetterSpacing = "0",
            },
            H1 = new H1Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "3.5rem", FontWeight = "400" },
            H2 = new H2Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "2.5rem", FontWeight = "400" },
            H3 = new H3Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "2rem", FontWeight = "400" },
            H4 = new H4Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "1.5rem", FontWeight = "400" },
            H5 = new H5Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "1.15rem", FontWeight = "400" },
            H6 = new H6Typography { FontFamily = new[] { "Merriweather", "Chiron GoRound TC", "Segoe UI", "system-ui", "sans-serif" }, FontSize = "1rem", FontWeight = "400" },
            Button = new ButtonTypography { FontWeight = "500", LetterSpacing = "0.06em", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
    };

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        _gateArm = true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _gateEpoch++;
        _overlayVisible = true;
        _gateArm = true;
        _ = InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_gateArm)
            return;

        _gateArm = false;
        var epoch = _gateEpoch;

        try
        {
            await JS.InvokeVoidAsync("photosite.waitForMainImages", ".js-main-image-gate__body", 15000, true);
        }
        catch
        {
            /* prerender / navigation */
        }

        if (epoch != _gateEpoch)
            return;

        _overlayVisible = false;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
