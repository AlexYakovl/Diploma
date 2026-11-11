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
    public class AboutController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AboutController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/About
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _context.AboutPhotos
                .AsNoTracking()
                .OrderBy(p => p.UploadedAt)
                .ThenBy(p => p.Id)
                .Select(p => new { p.Id })
                .ToListAsync();

            return Ok(rows.Select(p => new { p.Id, Url = $"/api/About/{p.Id}/file" }));
        }

        // GET: api/About/5/file
        [HttpGet("{id:int}/file")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFile(int id)
        {
            var photo = await _context.AboutPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
                return NotFound();

            var storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "about");
            var fullPath = Path.GetFullPath(Path.Combine(storageRoot, photo.FileName));
            var fullStorageRoot = Path.GetFullPath(storageRoot);

            if (!fullPath.StartsWith(fullStorageRoot, StringComparison.OrdinalIgnoreCase))
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

        // POST: api/About/upload
        [Authorize]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Недопустимый формат файла");

            var storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "about");
            Directory.CreateDirectory(storageFolder);

            var fileName = Guid.NewGuid() + extension;
            var filePath = Path.Combine(storageFolder, fileName);

            try
            {
                await using var input = file.OpenReadStream();
                using var image = await Image.LoadAsync(input);
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(1600, 1600)
                }));
                await using var output = new FileStream(filePath, FileMode.Create);
                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsync(output, new JpegEncoder { Quality = 88 });
                        break;
                    case ".png":
                        await image.SaveAsync(output, new PngEncoder());
                        break;
                    case ".webp":
                        await image.SaveAsync(output, new WebpEncoder { Quality = 88 });
                        break;
                }
            }
            catch
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                return BadRequest("Файл не является корректным изображением");
            }

            var entity = new AboutPhoto
            {
                FileName = fileName,
                UploadedAt = DateTime.UtcNow
            };
            _context.AboutPhotos.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { entity.Id, Url = $"/api/About/{entity.Id}/file" });
        }

        // DELETE: api/About/5
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await _context.AboutPhotos.FindAsync(id);
            if (photo == null)
                return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "about", photo.FileName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _context.AboutPhotos.Remove(photo);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
