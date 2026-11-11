using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoSite.Api.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PhotoSite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminMaintenanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private const string ConfirmHeaderName = "X-Confirm-Purge";
        private const string ConfirmHeaderValue = "PURGE_ALBUMS";

        public AdminMaintenanceController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost("aboutme-photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAboutMePhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Недопустимый формат файла");

            var storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "others");
            Directory.CreateDirectory(storageFolder);

            var fileName = "aboutme.jpg";
            var filePath = Path.Combine(storageFolder, fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            try
            {
                await using var input = file.OpenReadStream();
                using var image = await Image.LoadAsync(input);

                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(1400, 1400)
                }));

                await using var output = new FileStream(filePath, FileMode.Create);
                await image.SaveAsync(output, new JpegEncoder { Quality = 85 });
            }
            catch
            {
                return BadRequest("Файл не является корректным изображением");
            }

            return Ok(new { url = "/Storage/others/aboutme.jpg" });
        }

        [HttpPost("purge-albums")]
        public async Task<IActionResult> PurgeAlbums()
        {
            if (!_env.IsDevelopment())
                return Forbid();

            if (!Request.Headers.TryGetValue(ConfirmHeaderName, out var confirm) ||
                !string.Equals(confirm.ToString(), ConfirmHeaderValue, StringComparison.Ordinal))
            {
                return BadRequest($"Missing confirmation header '{ConfirmHeaderName}: {ConfirmHeaderValue}'.");
            }

            await _context.Photos.ExecuteDeleteAsync();
            await _context.Albums.ExecuteDeleteAsync();

            var root = Directory.GetCurrentDirectory();
            var storageRoot = Path.Combine(root, "Storage");

            var legacyAlbums = Path.Combine(storageRoot, "albums");
            if (Directory.Exists(legacyAlbums))
                Directory.Delete(legacyAlbums, recursive: true);

            var categoriesRoot = Path.Combine(storageRoot, "categories");
            if (Directory.Exists(categoriesRoot))
                Directory.Delete(categoriesRoot, recursive: true);

            return Ok(new
            {
                message = "All albums and photos purged. Storage folders removed.",
                removedFolders = new[] { legacyAlbums, categoriesRoot }
            });
        }
    }
}

