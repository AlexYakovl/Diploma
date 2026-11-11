using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoSite.Api.Data;
using PhotoSite.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;

namespace PhotoSite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HeroController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HeroController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Hero
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var photos = await _context.HeroPhotos
                .OrderBy(h => h.Order)
                .ThenBy(h => h.UploadedAt)
                .Select(h => new
                {
                    h.Id,
                    h.Order,
                    Url = $"/api/Hero/{h.Id}/file"
                })
                .ToListAsync();

            return Ok(photos);
        }

        // GET: api/Hero/{id}/file
        [HttpGet("{id:int}/file")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFile(int id)
        {
            var photo = await _context.HeroPhotos.FindAsync(id);
            if (photo == null)
                return NotFound();

            var storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "hero");
            var fullPath = Path.GetFullPath(Path.Combine(storageRoot, photo.FileName));
            var fullRoot = Path.GetFullPath(storageRoot);

            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Некорректный путь");

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Файл не найден");

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            var hash = MD5.HashData(fileBytes);
            var etag = Convert.ToBase64String(hash);

            if (Request.Headers["If-None-Match"] == etag)
                return StatusCode(304);

            Response.Headers["ETag"] = etag;
            Response.Headers["Cache-Control"] = "public,max-age=0,must-revalidate";

            return File(fileBytes, contentType);
        }

        // POST: api/Hero/upload
        [Authorize]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Недопустимый формат файла");

            var storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "hero");
            if (!Directory.Exists(storageFolder))
                Directory.CreateDirectory(storageFolder);

            var fileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(storageFolder, fileName);

            try
            {
                await using var input = file.OpenReadStream();
                using var image = await Image.LoadAsync(input);

                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(1920, 1080)
                }));

                await using var output = new FileStream(filePath, FileMode.Create);

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsync(output, new JpegEncoder { Quality = 85 });
                        break;
                    case ".png":
                        await image.SaveAsync(output, new PngEncoder());
                        break;
                    case ".webp":
                        await image.SaveAsync(output, new WebpEncoder { Quality = 85 });
                        break;
                    default:
                        await image.SaveAsync(output, new JpegEncoder { Quality = 85 });
                        break;
                }
            }
            catch
            {
                return BadRequest("Файл не является корректным изображением");
            }

            var maxOrder = await _context.HeroPhotos.AnyAsync()
                ? await _context.HeroPhotos.MaxAsync(h => h.Order)
                : -1;

            var hero = new HeroPhoto
            {
                FileName = fileName,
                Order = maxOrder + 1
            };

            _context.HeroPhotos.Add(hero);
            await _context.SaveChangesAsync();

            return Ok(new { hero.Id, hero.Order, Url = $"/api/Hero/{hero.Id}/file" });
        }

        // DELETE: api/Hero/{id}
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await _context.HeroPhotos.FindAsync(id);
            if (photo == null)
                return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "hero", photo.FileName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _context.HeroPhotos.Remove(photo);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
