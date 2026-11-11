using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using PhotoSite.Web.Components;
using PhotoSite.Web.Components.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HubOptions>(options =>
{
    // Файлы через InputFile на серверном рендере идут по SignalR — без лимита большие JPEG обрывают цепочку.
    options.MaximumReceiveMessageSize = 52 * 1024 * 1024;
});

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(15);
    });

builder.Services.AddMudServices();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<AlbumCardRevealState>();

// ��������� HttpClient ��� API
builder.Services.AddHttpClient("API", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing config: Api:BaseUrl");

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

// Local dev convenience: serve API Storage through Web app at /Storage/*
// so <img src="/Storage/others/aboutme.jpg"> works even when API is on another port.
var apiStorageRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "PhotoSite.Api", "Storage"));
if (Directory.Exists(apiStorageRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(apiStorageRoot),
        RequestPath = "/Storage"
    });
}

app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();