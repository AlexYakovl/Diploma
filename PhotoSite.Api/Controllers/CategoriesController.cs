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

namespace PhotoSite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _context.AlbumCategories
                .AsNoTracking()
                .OrderBy(c => c.Order)
                .ThenBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.CoverPath,
                    c.Order,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("cover/{id:int}")]
        [AllowAnonymous]
        public IActionResult GetCover(int id)
        {
            var category = _context.AlbumCategories.Find(id);
            if (category == null || string.IsNullOrEmpty(category.CoverPath))
                return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", category.CoverPath);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileBytes = System.IO.File.ReadAllBytes(filePath);

            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(fileBytes);
            var etag = Convert.ToBase64String(hash);

            if (Request.Headers["If-None-Match"] == etag)
                return StatusCode(304);

            Response.Headers["ETag"] = etag;
            Response.Headers["Cache-Control"] = "public,max-age=0,must-revalidate";

            return File(fileBytes, contentType);
        }

        [HttpGet("with-albums")]
        [AllowAnonymous]
        public async Task<IActionResult> GetWithAlbums()
        {
            var categories = await _context.AlbumCategories
                .AsNoTracking()
                .OrderBy(c => c.Order)
                .ThenBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.CoverPath,
                    c.Order,
                    c.CreatedAt,
                    Albums = c.Albums
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => new
                        {
                            a.Id,
                            a.CategoryId,
                            a.Title,
                            a.Description,
                            a.CoverPath,
                            a.CreatedAt
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(categories);
        }

        public record CategoryUpsertDto(string Title, int Order = 0);

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CategoryUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title is required");

            var category = new AlbumCategory
            {
                Title = dto.Title.Trim(),
                Order = dto.Order
            };

            _context.AlbumCategories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = category.Id }, category);
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryUpsertDto dto)
        {
            var category = await _context.AlbumCategories.FindAsync(id);
            if (category == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title is required");

            category.Title = dto.Title.Trim();
            category.Order = dto.Order;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.AlbumCategories.FindAsync(id);
            if (category == null)
                return NotFound();

            // Все альбомы и фото каталога лежат под Storage/categories/{id}/...
            // Каскад EF удаляет только строки в БД — без этого каталог на диске остаётся «осиротевшим».
            var categoryDir = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "categories", id.ToString());
            if (Directory.Exists(categoryDir))
            {
                try
                {
                    Directory.Delete(categoryDir, recursive: true);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Не удалось удалить файлы категории на диске: {ex.Message}");
                }
            }

            _context.AlbumCategories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id:int}/cover")]
        [Authorize]
        public async Task<IActionResult> UploadCover(int id, [FromForm] IFormFile file)
        {
            var category = await _context.AlbumCategories.FindAsync(id);
            if (category == null)
                return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Недопустимый формат файла");

            var storagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Storage",
                "categories",
                category.Id.ToString());
            if (!Directory.Exists(storagePath))
                Directory.CreateDirectory(storagePath);

            var fileName = "cover" + extension;
            var filePath = Path.Combine(storagePath, fileName);

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
                return BadRequest("Обложка не является корректным изображением");
            }

            category.CoverPath = Path.Combine(
                    "categories",
                    category.Id.ToString(),
                    fileName)
                .Replace("\\", "/");
            await _context.SaveChangesAsync();

            return Ok(new { category.Id, category.CoverPath });
        }
    }
}

