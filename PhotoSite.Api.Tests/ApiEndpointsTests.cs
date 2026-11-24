using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PhotoSite.Api.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PhotoSite.Api.Tests;

[TestFixture]
[NonParallelizable]
public sealed class ApiEndpointsTests
{
    private PhotoSiteWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new PhotoSiteWebApplicationFactory();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Auth", "true");
        await _factory.InitializeAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Login_WithValidCredentials_ReturnsJwt()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = "test-admin", password = "Test-Password123!" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.That(document!.RootElement.GetProperty("token").GetString(),
            Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = "test-admin", password = "wrong-password" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_WithTestAuthentication_ReturnsCurrentUser()
    {
        using var response = await _client.GetAsync("/api/Auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.That(document!.RootElement.GetProperty("username").GetString(),
            Is.EqualTo("test-admin"));
    }

    [Test]
    public async Task Categories_GetAll_ReturnsSeededCategory()
    {
        using var response = await _client.GetAsync("/api/Categories");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Wedding"));
    }

    [Test]
    public async Task Categories_GetWithAlbums_ReturnsNestedAlbum()
    {
        using var response = await _client.GetAsync("/api/Categories/with-albums");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Summer wedding"));
    }

    [Test]
    public async Task Categories_CreateValidCategory_ReturnsCreated()
    {
        using var content = new StringContent(
            """{"title":"Portrait","order":2}""",
            Encoding.UTF8,
            "application/json");
        using var response = await _client.PostAsync("/api/Categories", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Categories_CreateBlankTitle_ReturnsBadRequest(string title)
    {
        using var content = new StringContent(
            $$"""{"title":"{{title}}","order":0}""",
            Encoding.UTF8,
            "application/json");
        using var response = await _client.PostAsync("/api/Categories", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Categories_UpdateAndDelete_WorkForExistingCategory()
    {
        using var updateContent = new StringContent(
            """{"title":"Updated wedding","order":4}""",
            Encoding.UTF8,
            "application/json");
        using var updateResponse = await _client.PutAsync("/api/Categories/1", updateContent);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var deleteResponse = await _client.DeleteAsync("/api/Categories/1");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Categories_DeleteMissing_ReturnsNotFound()
    {
        using var response = await _client.DeleteAsync("/api/Categories/999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Albums_GetAll_ReturnsSeededAlbum()
    {
        using var response = await _client.GetAsync("/api/Albums");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Summer wedding"));
    }

    [Test]
    public async Task Albums_FilterByCategory_ReturnsOnlyMatchingAlbum()
    {
        using var response = await _client.GetAsync("/api/Albums?categoryId=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Summer wedding"));
    }

    [Test]
    public async Task Albums_CreateWithValidCategory_ReturnsCreated()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("1"), "CategoryId" },
            { new StringContent("Birthday"), "Title" },
            { new StringContent("Birthday shoot"), "Description" }
        };
        using var response = await _client.PostAsync("/api/Albums", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Albums_CreateWithMissingCategory_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("999"), "CategoryId" },
            { new StringContent("Orphan"), "Title" }
        };
        using var response = await _client.PostAsync("/api/Albums", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Albums_UpdateExisting_ReturnsNoContent()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Updated album"), "Title" },
            { new StringContent("Updated description"), "Description" }
        };
        using var response = await _client.PutAsync("/api/Albums/1", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Albums_DeleteMissing_ReturnsNotFound()
    {
        using var response = await _client.DeleteAsync("/api/Albums/999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Photos_GetByAlbumWithoutPhotos_ReturnsEmptyArray()
    {
        using var response = await _client.GetAsync("/api/Photos/album/1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload.Trim(), Is.EqualTo("[]"));
    }

    [Test]
    public async Task Photos_GetMissingPhoto_ReturnsNotFound()
    {
        using var response = await _client.GetAsync("/api/Photos/999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Photos_UploadWithoutFiles_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        using var response = await _client.PostAsync("/api/Photos/upload?albumId=1", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Reviews_PublicListExcludesHiddenReview()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Reviews.AddRange(
            new PhotoSite.Api.Models.Review
            {
                Author = "Visible",
                Text = "Public review",
                IsHidden = false
            },
            new PhotoSite.Api.Models.Review
            {
                Author = "Hidden",
                Text = "Pending review",
                IsHidden = true
            });
        await db.SaveChangesAsync();

        using var response = await _client.GetAsync("/api/Reviews");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Visible"));
        Assert.That(payload, Does.Not.Contain("\"Author\":\"Hidden\""));
    }

    [Test]
    public async Task Reviews_CreateWithoutAvatar_ReturnsCreatedLikeOk()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Test author"), "Author" },
            { new StringContent("Excellent work"), "Text" }
        };
        using var response = await _client.PostAsync("/api/Reviews", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Reviews_CreateWithMissingAlbum_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Test author"), "Author" },
            { new StringContent("Review text"), "Text" },
            { new StringContent("999"), "AlbumId" }
        };
        using var response = await _client.PostAsync("/api/Reviews", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Hero_GetAll_ReturnsSuccess()
    {
        using var response = await _client.GetAsync("/api/Hero");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task About_GetAll_ReturnsSuccess()
    {
        using var response = await _client.GetAsync("/api/About");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MissingEndpoint_ReturnsNotFound()
    {
        using var response = await _client.GetAsync("/api/not-a-real-endpoint");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutTestAuthentication_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Auth");

        using var content = new StringContent(
            """{"title":"Unauthorized","order":0}""",
            Encoding.UTF8,
            "application/json");
        using var response = await _client.PostAsync("/api/Categories", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Categories_UpdateMissing_ReturnsNotFound()
    {
        using var content = new StringContent(
            """{"title":"Missing","order":1}""",
            Encoding.UTF8,
            "application/json");
        using var response = await _client.PutAsync("/api/Categories/999", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Albums_GetMissing_ReturnsNotFound()
    {
        using var response = await _client.GetAsync("/api/Albums/999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Albums_UpdateMissing_ReturnsNotFound()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Missing"), "Title" },
            { new StringContent("Missing"), "Description" }
        };
        using var response = await _client.PutAsync("/api/Albums/999", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Albums_CreateWithInvalidCoverExtension_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("1"), "CategoryId" },
            { new StringContent("Invalid cover"), "Title" }
        };
        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("not-an-image")),
            "CoverFile",
            "cover.txt");

        using var response = await _client.PostAsync("/api/Albums", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Albums_CreateWithCorruptCoverImage_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("1"), "CategoryId" },
            { new StringContent("Corrupt cover"), "Title" }
        };
        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("corrupt-image-data")),
            "CoverFile",
            "cover.jpg");

        using var response = await _client.PostAsync("/api/Albums", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Photos_UploadToMissingAlbum_ReturnsNotFound()
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(CreatePngBytes()),
            "files",
            "photo.png");

        using var response = await _client.PostAsync("/api/Photos/upload?albumId=999", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Photos_UploadValidPng_ReturnsCreatedPayload()
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(CreatePngBytes()),
            "files",
            "photo.png");

        using var response = await _client.PostAsync("/api/Photos/upload?albumId=1", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Files").Or.Contain("Файлы").Or.Contain("photo.png"));
    }

    [Test]
    public async Task Reviews_AdminListContainsHiddenReview()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Reviews.Add(new PhotoSite.Api.Models.Review
        {
            Author = "Pending admin review",
            Text = "Awaiting moderation",
            IsHidden = true
        });
        await db.SaveChangesAsync();

        using var response = await _client.GetAsync("/api/Reviews/admin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Pending admin review"));
    }

    [Test]
    public async Task Reviews_SetVisibilityAndDelete_WorkForExistingReview()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = new PhotoSite.Api.Models.Review
        {
            Author = "Moderated",
            Text = "Visibility test",
            IsHidden = true
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        using var visibilityResponse =
            await _client.PutAsync($"/api/Reviews/{review.Id}/visibility?hidden=false", null);
        Assert.That(visibilityResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var deleteResponse = await _client.DeleteAsync($"/api/Reviews/{review.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Reviews_AdminEndpointsRejectRequestWithoutTestAuthentication()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Auth");

        using var response = await _client.GetAsync("/api/Reviews/admin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task HeroUploadInvalidImage_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("not-an-image")),
            "file",
            "hero.jpg");

        using var response = await _client.PostAsync("/api/Hero/upload", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AboutUploadInvalidImage_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("not-an-image")),
            "file",
            "about.jpg");

        using var response = await _client.PostAsync("/api/About/upload", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AdminMaintenancePurgeRequiresConfirmationHeader()
    {
        using var missingConfirmation = await _client.PostAsync(
            "/api/AdminMaintenance/purge-albums",
            null);

        Assert.That(missingConfirmation.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AdminMaintenancePurgeWithConfirmationReturnsSuccess()
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Confirm-Purge",
            "PURGE_ALBUMS");

        using var response = await _client.PostAsync(
            "/api/AdminMaintenance/purge-albums",
            null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static byte[] CreatePngBytes()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
