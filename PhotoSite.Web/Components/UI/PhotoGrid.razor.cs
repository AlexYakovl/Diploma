using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PhotoSite.Web.Components.Services;

namespace PhotoSite.Web.Components.UI;

public partial class PhotoGrid : ComponentBase, IAsyncDisposable
{
    [Inject] private ApiService Api { get; set; } = null!;
    [Inject] private AuthStateService AuthState { get; set; } = null!;


    [Parameter, EditorRequired] public List<PhotoDto> Photos { get; set; } = new();
    [Parameter, EditorRequired] public int AlbumId { get; set; }
    [Parameter] public EventCallback Refresh { get; set; }

    private bool _isAdmin;
    private int? _lightboxIndex;
    private string? _uploadError;
    private bool _photoUploadBusy;
    private double _uploadPct;
    private string _uploadStatusLabel = "";
    private ElementReference _lightboxRef;
    private IReadOnlyList<IReadOnlyList<PhotoDto>> _rows = Array.Empty<IReadOnlyList<PhotoDto>>();
    private ElementReference _photoInputRef;
    private readonly string _albumPhotoInputId = $"album-photo-{Guid.NewGuid():n}";
    private DotNetObjectReference<PhotoGrid>? _uploadProgressRef;

    private bool _focusLightbox;
    private double? _pointerStartX;

    [JSInvokable]
    public void OnAlbumPhotoUploadProgress(long loaded, long total)
    {
        if (total > 0)
            _uploadPct = Math.Clamp((double)loaded / total * 100.0, 0, 100);
        else
            _uploadPct = Math.Clamp(Math.Log10(Math.Max(1, loaded)) * 22.0, 8, 92);
        _uploadStatusLabel = $"{_uploadPendingFileName} · {_uploadPct:0}%";
        _ = InvokeAsync(StateHasChanged);
    }

    private string _uploadPendingFileName = "";

    private static bool IsFullWidthByAspect(PhotoDto p)
    {
        if (p.Width is not > 0 || p.Height is not > 0)
            return false;
        return (double)p.Width.Value / p.Height.Value > WideRowAspectThreshold;
    }

    /// <summary>Landscape / wide shots: one per row. Below this ratio → pair with the next (portrait, square, mild horizontal).</summary>
    private const double WideRowAspectThreshold = 1.15;

    private static IReadOnlyList<IReadOnlyList<PhotoDto>> BuildRows(IReadOnlyList<PhotoDto> photos)
    {
        if (photos.Count == 0)
            return Array.Empty<IReadOnlyList<PhotoDto>>();

        var rows = new List<IReadOnlyList<PhotoDto>>();
        var buffer = new List<PhotoDto>();
        var pendingWide = new List<PhotoDto>();

        void FlushBuffer()
        {
            if (buffer.Count == 0)
                return;
            rows.Add(SnapshotRow(buffer));
            buffer.Clear();
        }

        void FlushPendingWide()
        {
            if (pendingWide.Count == 0)
                return;
            foreach (var w in pendingWide)
                rows.Add(new[] { w });
            pendingWide.Clear();
        }

        foreach (var p in photos)
        {
            if (IsFullWidthByAspect(p))
            {
                // Если перед широкой уже есть одна «узкая», то откладываем широкую,
                // чтобы следующая узкая могла подняться наверх и встать парой.
                if (buffer.Count == 1)
                {
                    pendingWide.Add(p);
                }
                else
                {
                    FlushBuffer();
                    rows.Add(new[] { p });
                }
            }
            else
            {
                buffer.Add(p);
                if (buffer.Count >= 2)
                {
                    rows.Add(SnapshotRow(buffer));
                    buffer.Clear();
                    // После того как собрали пару узких — показываем все отложенные широкие в исходном порядке.
                    FlushPendingWide();
                }
            }
        }

        FlushBuffer();
        FlushPendingWide();
        return rows;
    }

    private static IReadOnlyList<PhotoDto> SnapshotRow(List<PhotoDto> b) =>
        b.Count == 1
            ? (IReadOnlyList<PhotoDto>)new[] { b[0] }
            : new[] { b[0], b[1] };

    /// <summary>Класс строки: две в ряд, одна панорама, или одна портрет/квадрат без пары (не тянем на 100% ширины).</summary>
    private static string RowClass(IReadOnlyList<PhotoDto> row)
    {
        if (row.Count == 2)
            return "photo-mosaic__row photo-mosaic__row--two";
        if (row.Count == 1)
        {
            if (IsFullWidthByAspect(row[0]))
                return "photo-mosaic__row photo-mosaic__row--one";
            return "photo-mosaic__row photo-mosaic__row--one photo-mosaic__row--one-solo-partial";
        }
        return "photo-mosaic__row";
    }

    protected override void OnParametersSet()
    {
        _rows = BuildRows(Photos);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isAdmin = await AuthState.IsAdminAsync(JS);
            StateHasChanged();
        }

        if (_focusLightbox)
        {
            _focusLightbox = false;
            try
            {
                await _lightboxRef.FocusAsync();
            }
            catch
            {
                // focus may fail if ref not yet in DOM
            }
        }
    }

    private string? LightboxUrl =>
        _lightboxIndex is { } i && i >= 0 && i < Photos.Count
            ? Api.GetPhotoUrl(Photos[i].Id)
            : null;

    private string? LightboxFileName =>
        _lightboxIndex is { } i && i >= 0 && i < Photos.Count
            ? Photos[i].OriginalFileName
            : null;

    private int LightboxNumber =>
        _lightboxIndex is { } i && Photos.Count > 0 ? i + 1 : 0;

    private void OpenLightbox(int index)
    {
        if (index < 0 || index >= Photos.Count)
            return;
        _lightboxIndex = index;
        _focusLightbox = true;
        _ = JS.InvokeVoidAsync("photosite.setBodyScrollLock", true);
    }

    private void CloseLightbox()
    {
        _lightboxIndex = null;
        _ = JS.InvokeVoidAsync("photosite.setBodyScrollLock", false);
    }

    private void LightboxNext()
    {
        if (_lightboxIndex is not { } i || Photos.Count == 0)
            return;
        _lightboxIndex = (i + 1) % Photos.Count;
    }

    private void LightboxPrev()
    {
        if (_lightboxIndex is not { } i || Photos.Count == 0)
            return;
        _lightboxIndex = (i - 1 + Photos.Count) % Photos.Count;
    }

    private void OnLightboxKey(KeyboardEventArgs e)
    {
        if (e.Key is "ArrowLeft" or "Left")
            LightboxPrev();
        else if (e.Key is "ArrowRight" or "Right")
            LightboxNext();
        else if (e.Key == "Escape")
            CloseLightbox();
    }

    private void OnLightboxPointerDown(PointerEventArgs e)
    {
        if (e.Button != 0)
            return;
        _pointerStartX = e.ClientX;
    }

    private void OnLightboxPointerUp(PointerEventArgs e)
    {
        if (_pointerStartX is not { } start)
            return;
        _pointerStartX = null;
        var dx = e.ClientX - start;
        if (dx < -48)
            LightboxNext();
        else if (dx > 48)
            LightboxPrev();
    }

    private string AspectStyle(PhotoDto p)
    {
        if (p.Width is > 0 && p.Height is > 0)
            return FormattableString.Invariant($"--photo-ar:{p.Width} / {p.Height};");
        return "--photo-ar:1 / 1;";
    }

    public async ValueTask DisposeAsync()
    {
        if (_lightboxIndex != null)
        {
            try
            {
                await JS.InvokeVoidAsync("photosite.setBodyScrollLock", false);
            }
            catch
            {
                // ignore dispose errors
            }
        }
    }

    // ── upload / delete (from original) ─────────────────────────────────────

    private async Task OnDeleteAsync(int photoId)
    {
        if (_photoUploadBusy)
            return;

        await Api.DeletePhotoAsync(photoId, JS);
        await Refresh.InvokeAsync();
    }

    private async Task OnPhotoNativeChanged()
    {
        if (_photoUploadBusy)
            return;

        var name = await JS.InvokeAsync<string>("photosite.inputFirstFileName", _photoInputRef);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _uploadError = null;
        _photoUploadBusy = true;
        _uploadPct = 0;
        _uploadPendingFileName = name;
        _uploadStatusLabel = name;
        await InvokeAsync(StateHasChanged);

        _uploadProgressRef = DotNetObjectReference.Create(this);
        try
        {
            var token = await Api.GetJwtFromBrowserAsync(JS) ?? "";
            var r = await Api.UploadMultipartFromBrowserAsync(
                JS,
                "POST",
                $"/api/Photos/upload?albumId={AlbumId}",
                Array.Empty<(string, string)>(),
                _photoInputRef,
                "files",
                _uploadProgressRef,
                nameof(OnAlbumPhotoUploadProgress),
                token);

            if (!r.Ok)
                _uploadError = "Не удалось загрузить фото.";
            else
                await Refresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            _uploadError = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            _uploadProgressRef?.Dispose();
            _uploadProgressRef = null;
            try
            {
                await Api.ClearBrowserFileInputAsync(JS, _photoInputRef);
            }
            catch
            {
                /* ignore */
            }

            _photoUploadBusy = false;
            _uploadPct = 0;
            _uploadStatusLabel = "";
            _uploadPendingFileName = "";
            await InvokeAsync(StateHasChanged);
        }
    }
}
