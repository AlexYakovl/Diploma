using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace PhotoSite.Web.Components.Services
{
    // ── DTOs ────────────────────────────────────────────────────────────────────

    public record AlbumDto(int Id, int CategoryId, string Title, string? Description, string? CoverPath, DateTime CreatedAt);

    public record CategoryDto(int Id, string Title, string? CoverPath, int Order, DateTime CreatedAt, List<AlbumDto> Albums);

    public record CategoryListItemDto(int Id, string Title, string? CoverPath, int Order, DateTime CreatedAt);

    public record PhotoDto(int Id, string OriginalFileName, string Url, int? Width, int? Height);

    public record ReviewDto(
        int Id,
        string Author,
        string Text,
        string? AvatarPath,
        DateTime CreatedAt,
        bool IsHidden,
        int? AlbumId,
        string? AlbumTitle);

    public record HeroPhotoDto(int Id, int Order, string Url);

    public record AboutPhotoListItem(int Id, string Url);

    // ── Service ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Centralises all HTTP calls to PhotoSite.Api.
    /// Inject via DI; token is read from localStorage on demand.
    /// </summary>
    public class ApiService
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _configuration;

        public ApiService(IHttpClientFactory factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        /// <summary>
        /// URL для выдачи в разметку браузера (атрибут src у img и т.п.). Если Api:PublicUrl не задан,
        /// возвращается путь вида /api/... или /Storage/... с тем же origin, что и сайт (сценарий nginx + Docker).
        /// Для локальной разработки с API на другом порту задайте Api:PublicUrl.
        /// </summary>
        public string ToBrowserAssetUrl(string pathStartingWithSlash)
        {
            if (string.IsNullOrWhiteSpace(pathStartingWithSlash))
                return pathStartingWithSlash;
            var path = pathStartingWithSlash.StartsWith('/') ? pathStartingWithSlash : "/" + pathStartingWithSlash;
            var pub = _configuration["Api:PublicUrl"]?.TrimEnd('/');
            if (string.IsNullOrEmpty(pub))
                return path;
            return $"{pub}{path}";
        }

        /// <summary>
        /// Абсолютный URL вызова API из браузера (XHR/fetch). Без Api:PublicUrl — относительный путь (прокси nginx на том же хосте).
        /// </summary>
        public string BrowserApiAbsoluteUrl(string pathAndQuery)
        {
            var path = pathAndQuery.StartsWith('/') ? pathAndQuery : "/" + pathAndQuery;
            var pub = _configuration["Api:PublicUrl"]?.TrimEnd('/');
            return string.IsNullOrEmpty(pub) ? path : pub + path;
        }

        public sealed class BrowserXhrResult
        {
            public bool Ok { get; set; }
            public int Status { get; set; }
            public string Body { get; set; } = "";
        }

        public sealed class AboutSliderBatchResult
        {
            public bool Ok { get; set; }
            public int Uploaded { get; set; }
            public int Total { get; set; }
            public int Status { get; set; }
            public string Body { get; set; } = "";
        }

        public Task<string?> GetJwtFromBrowserAsync(IJSRuntime js)
            => js.InvokeAsync<string?>("localStorage.getItem", "jwt").AsTask();

        private static string SerializeFormFields(IReadOnlyList<(string Name, string Value)> fields)
            => JsonSerializer.Serialize(fields.Select(f => new { name = f.Name, value = f.Value }).ToArray());

        /// <summary>Прямая отправка multipart из браузера (минуя SignalR для тела файла).</summary>
        public Task<BrowserXhrResult> UploadMultipartFieldsOnlyFromBrowserAsync(
            IJSRuntime js,
            string httpMethod,
            string pathAndQuery,
            IReadOnlyList<(string Name, string Value)> fields,
            string bearerToken)
        {
            var fieldsJson = SerializeFormFields(fields);
            var url = BrowserApiAbsoluteUrl(pathAndQuery);
            return js.InvokeAsync<BrowserXhrResult>(
                "photosite.uploadMultipartFieldsOnly",
                httpMethod,
                url,
                bearerToken,
                fieldsJson).AsTask();
        }

        public Task<BrowserXhrResult> UploadMultipartFromBrowserAsync(
            IJSRuntime js,
            string httpMethod,
            string pathAndQuery,
            IReadOnlyList<(string Name, string Value)> fields,
            ElementReference fileInput,
            string fileFieldName,
            object? dotNetProgressReceiver,
            string? progressMethodName,
            string bearerToken)
        {
            var fieldsJson = SerializeFormFields(fields);
            var url = BrowserApiAbsoluteUrl(pathAndQuery);
            return js.InvokeAsync<BrowserXhrResult>(
                "photosite.uploadMultipartForm",
                httpMethod,
                url,
                bearerToken,
                fieldsJson,
                fileInput,
                fileFieldName,
                dotNetProgressReceiver,
                progressMethodName ?? "").AsTask();
        }

        public Task<AboutSliderBatchResult> UploadAboutSliderBatchFromBrowserAsync(
            IJSRuntime js,
            ElementReference fileInput,
            string bearerToken,
            object? dotNetProgressReceiver,
            string? progressMethodName)
        {
            var url = BrowserApiAbsoluteUrl("/api/About/upload");
            return js.InvokeAsync<AboutSliderBatchResult>(
                "photosite.uploadAboutSliderBatch",
                url,
                bearerToken,
                fileInput,
                dotNetProgressReceiver,
                progressMethodName ?? "").AsTask();
        }

        /// <summary>Пакетная загрузка слайдера «О себе» без индикатора (обратная совместимость).</summary>
        public Task<AboutSliderBatchResult> UploadAboutSliderBatchFromBrowserAsync(
            IJSRuntime js,
            ElementReference fileInput,
            string bearerToken)
            => UploadAboutSliderBatchFromBrowserAsync(js, fileInput, bearerToken, null, null);

        /// <summary>Загрузка в альбом из браузера: несколько файлов, строго по одному POST за раз (очередь).</summary>
        public Task<AboutSliderBatchResult> UploadAlbumPhotosSequentialFromBrowserAsync(
            IJSRuntime js,
            int albumId,
            ElementReference fileInput,
            object? dotNetProgressReceiver,
            string? progressMethodName,
            string bearerToken)
        {
            var url = BrowserApiAbsoluteUrl($"/api/Photos/upload?albumId={albumId}");
            return js.InvokeAsync<AboutSliderBatchResult>(
                "photosite.uploadAlbumPhotosSequential",
                url,
                bearerToken,
                fileInput,
                dotNetProgressReceiver,
                progressMethodName ?? "").AsTask();
        }

        public Task ClearBrowserFileInputAsync(IJSRuntime js, ElementReference input)
            => js.InvokeVoidAsync("photosite.clearFileInput", input).AsTask();

        public async Task<(bool Ok, HttpStatusCode Status, string? Body)> SubmitReviewFromBrowserAsync(
            IJSRuntime js,
            string author,
            string text,
            int? albumId,
            ElementReference? avatarInput,
            object? dotNetProgressReceiver = null,
            string? progressMethodName = null)
        {
            var fields = new List<(string Name, string Value)>
            {
                ("Author", author),
                ("Text", text),
            };
            if (albumId.HasValue)
                fields.Add(("AlbumId", albumId.Value.ToString()));

            BrowserXhrResult r;
            if (avatarInput is { } el)
            {
                r = await UploadMultipartFromBrowserAsync(
                    js,
                    "POST",
                    "/api/Reviews",
                    fields,
                    el,
                    "Avatar",
                    dotNetProgressReceiver,
                    progressMethodName,
                    bearerToken: "");
            }
            else
                r = await UploadMultipartFieldsOnlyFromBrowserAsync(js, "POST", "/api/Reviews", fields, bearerToken: "");

            return (r.Ok, (HttpStatusCode)r.Status, string.IsNullOrEmpty(r.Body) ? null : r.Body);
        }

        private HttpClient Anon() => _factory.CreateClient("API");

        private async Task<HttpClient> Auth(IJSRuntime js)
        {
            var client = _factory.CreateClient("API");
            var token = await js.InvokeAsync<string?>("localStorage.getItem", "jwt");
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public string GetAboutMePhotoUrl()
            => ToBrowserAssetUrl("/Storage/others/aboutme.jpg");

        public async Task<bool> AboutMePhotoExistsAsync()
        {
            try
            {
                using var r = await Anon().GetAsync("Storage/others/aboutme.jpg", HttpCompletionOption.ResponseHeadersRead);
                return r.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UploadAboutMePhotoAsync(IBrowserFile file, IJSRuntime js)
        {
            const long maxFileSize = 50_000_000;
            if (file == null)
                return false;

            var client = await Auth(js);
            await using var stream = file.OpenReadStream(maxFileSize);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "file", file.Name);
            var r = await client.PostAsync("api/AdminMaintenance/aboutme-photo", content);
            return r.IsSuccessStatusCode;
        }

        public async Task<(bool Ok, HttpStatusCode Status, string? Body)> UploadAboutMePhotoDetailedAsync(IBrowserFile file, IJSRuntime js)
        {
            const long maxFileSize = 50_000_000;
            if (file == null)
                return (false, 0, "Файл не выбран");

            var client = await Auth(js);
            await using var stream = file.OpenReadStream(maxFileSize);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "file", file.Name);

            using var r = await client.PostAsync("api/AdminMaintenance/aboutme-photo", content);
            string? body = null;
            try { body = await r.Content.ReadAsStringAsync(); } catch { /* ignore */ }
            return (r.IsSuccessStatusCode, r.StatusCode, body);
        }

        public async Task<List<AboutPhotoListItem>> GetAboutPhotosAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<AboutPhotoListItem>>("api/About");
            return r ?? new();
        }

        public string GetAboutPhotoFileUrl(int id)
            => ToBrowserAssetUrl($"/api/About/{id}/file");

        public async Task<bool> UploadAboutSliderPhotoAsync(IBrowserFile file, IJSRuntime js)
        {
            const long maxFileSize = 50_000_000;
            if (file == null)
                return false;

            var client = await Auth(js);
            await using var stream = file.OpenReadStream(maxFileSize);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "file", file.Name);
            var r = await client.PostAsync("api/About/upload", content);
            return r.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAboutSliderPhotoAsync(int id, IJSRuntime js)
        {
            var client = await Auth(js);
            var r = await client.DeleteAsync($"api/About/{id}");
            return r.IsSuccessStatusCode;
        }

        // ── Albums ───────────────────────────────────────────────────────────────

        public async Task<List<AlbumDto>> GetAlbumsAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<AlbumDto>>("api/Albums");
            return r ?? new();
        }

        public async Task<AlbumDto?> GetAlbumAsync(int id)
            => await Anon().GetFromJsonAsync<AlbumDto>($"api/Albums/{id}");

        public string GetCoverUrl(int albumId)
            => ToBrowserAssetUrl($"/api/Albums/cover/{albumId}");

        // ── Categories ───────────────────────────────────────────────────────────

        public async Task<List<CategoryDto>> GetCategoriesWithAlbumsAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<CategoryDto>>("api/Categories/with-albums");
            return r ?? new();
        }

        public async Task<List<CategoryListItemDto>> GetCategoriesAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<CategoryListItemDto>>("api/Categories");
            return r ?? new();
        }

        public string GetCategoryCoverUrl(int categoryId)
            => ToBrowserAssetUrl($"/api/Categories/cover/{categoryId}");

        public async Task<List<AlbumDto>> GetAlbumsByCategoryAsync(int categoryId)
        {
            var r = await Anon().GetFromJsonAsync<List<AlbumDto>>($"api/Albums?categoryId={categoryId}");
            return r ?? new();
        }

        // ── Photos ───────────────────────────────────────────────────────────────

        public async Task<List<PhotoDto>> GetAlbumPhotosAsync(int albumId)
        {
            var r = await Anon().GetFromJsonAsync<List<PhotoDto>>($"api/Photos/album/{albumId}");
            return r ?? new();
        }

        public string GetPhotoUrl(int photoId)
            => ToBrowserAssetUrl($"/api/Photos/{photoId}");

        public async Task<bool> DeletePhotoAsync(int photoId, IJSRuntime js)
        {
            var client = await Auth(js);
            var r = await client.DeleteAsync($"api/Photos/{photoId}");
            return r.IsSuccessStatusCode;
        }

        public async Task<bool> UploadPhotosAsync(int albumId, IEnumerable<(Stream Stream, string FileName)> files, IJSRuntime js)
        {
            var client = await Auth(js);
            using var content = new MultipartFormDataContent();
            foreach (var (stream, name) in files)
                content.Add(new StreamContent(stream), "files", name);
            var r = await client.PostAsync($"api/Photos/upload?albumId={albumId}", content);
            return r.IsSuccessStatusCode;
        }

        public record UploadProgress(
            long SentBytes,
            long TotalBytes,
            int CurrentFileIndex,
            int FileCount,
            string? CurrentFileName,
            bool Completed);

        public async Task<bool> UploadPhotosWithProgressAsync(
            int albumId,
            IReadOnlyList<IBrowserFile> files,
            IJSRuntime js,
            Action<UploadProgress> onProgress,
            CancellationToken cancellationToken = default)
        {
            if (files == null || files.Count == 0)
                return true;

            const long maxFileSize = 50_000_000;

            var client = await Auth(js);
            using var content = new MultipartFormDataContent();

            var totalBytes = files.Sum(f => (long)f.Size);
            var fileCount = files.Count;
            long offsetBeforeThisFile = 0;

            for (var i = 0; i < fileCount; i++)
            {
                var file = files[i];
                var fileIndex = i + 1;
                var fileName = file.Name;
                var fileLength = (long)file.Size;
                var fileOffset = offsetBeforeThisFile;

                var stream = file.OpenReadStream(maxFileSize, cancellationToken);
                var part = new ProgressStreamContent(
                    stream,
                    fileLength,
                    bytesSentForThisFile =>
                    {
                        var overallSent = fileOffset + bytesSentForThisFile;
                        onProgress(new UploadProgress(
                            SentBytes: overallSent,
                            TotalBytes: totalBytes,
                            CurrentFileIndex: fileIndex,
                            FileCount: fileCount,
                            CurrentFileName: fileName,
                            Completed: false));
                    });

                // Important: name must be "files" to bind to IFormFileCollection on API.
                content.Add(part, "files", fileName);

                offsetBeforeThisFile += fileLength;
            }

            onProgress(new UploadProgress(0, totalBytes, 0, fileCount, null, false));

            var r = await client.PostAsync($"api/Photos/upload?albumId={albumId}", content, cancellationToken);

            onProgress(new UploadProgress(totalBytes, totalBytes, fileCount, fileCount, files[^1].Name, true));
            return r.IsSuccessStatusCode;
        }

        public async Task<bool> UploadSinglePhotoWithProgressAsync(
            int albumId,
            IBrowserFile file,
            IJSRuntime js,
            Action<long, long> onProgressBytes,
            CancellationToken cancellationToken = default)
        {
            const long maxFileSize = 50_000_000;
            var length = (long)file.Size;

            var client = await Auth(js);
            using var content = new MultipartFormDataContent();

            var stream = file.OpenReadStream(maxFileSize, cancellationToken);
            var part = new ProgressStreamContent(
                stream,
                length,
                bytesSent => onProgressBytes(bytesSent, length));

            content.Add(part, "files", file.Name);

            var r = await client.PostAsync($"api/Photos/upload?albumId={albumId}", content, cancellationToken);
            return r.IsSuccessStatusCode;
        }

        private sealed class ProgressStreamContent : HttpContent
        {
            private readonly Stream _source;
            private readonly long _length;
            private readonly Action<long> _progressBytesSent;
            private readonly int _bufferSize;

            public ProgressStreamContent(Stream source, long length, Action<long> progressBytesSent, int bufferSize = 64 * 1024)
            {
                _source = source;
                _length = length;
                _progressBytesSent = progressBytesSent;
                _bufferSize = bufferSize;
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _length;
                return true;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                var buffer = new byte[_bufferSize];
                long totalRead = 0;
                int read;
                while ((read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await stream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;
                    _progressBytesSent(totalRead);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _source.Dispose();
                base.Dispose(disposing);
            }
        }

        // ── Reviews ──────────────────────────────────────────────────────────────

        public async Task<List<ReviewDto>> GetPublicReviewsAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<ReviewDto>>("api/Reviews");
            return r ?? new();
        }

        public async Task<bool> SubmitReviewAsync(string author, string text, int? albumId,
            Stream? avatarStream, string? avatarFileName)
        {
            var result = await SubmitReviewDetailedAsync(author, text, albumId, avatarStream, avatarFileName);
            return result.Ok;
        }

        public async Task<(bool Ok, HttpStatusCode Status, string? Body)> SubmitReviewDetailedAsync(
            string author,
            string text,
            int? albumId,
            Stream? avatarStream,
            string? avatarFileName)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(author), "Author");
            content.Add(new StringContent(text), "Text");
            if (albumId.HasValue)
                content.Add(new StringContent(albumId.Value.ToString()), "AlbumId");
            if (avatarStream != null && avatarFileName != null)
                content.Add(new StreamContent(avatarStream), "Avatar", avatarFileName);

            using var r = await Anon().PostAsync("api/Reviews", content);
            string? body = null;
            try { body = await r.Content.ReadAsStringAsync(); } catch { /* ignore */ }
            return (r.IsSuccessStatusCode, r.StatusCode, body);
        }

        public string GetReviewAvatarUrl(int reviewId)
            => ToBrowserAssetUrl($"/api/Reviews/{reviewId}/avatar");

        // ── Hero Carousel ────────────────────────────────────────────────────────

        public async Task<List<HeroPhotoDto>> GetHeroPhotosAsync()
        {
            var r = await Anon().GetFromJsonAsync<List<HeroPhotoDto>>("api/Hero");
            return r ?? new();
        }

        public string GetHeroPhotoUrl(int heroId)
            => ToBrowserAssetUrl($"/api/Hero/{heroId}/file");

        public async Task<bool> UploadHeroPhotoAsync(Stream stream, string fileName, IJSRuntime js)
        {
            var client = await Auth(js);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "file", fileName);
            var r = await client.PostAsync("api/Hero/upload", content);
            return r.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteHeroPhotoAsync(int heroId, IJSRuntime js)
        {
            var client = await Auth(js);
            var r = await client.DeleteAsync($"api/Hero/{heroId}");
            return r.IsSuccessStatusCode;
        }
    }
}
