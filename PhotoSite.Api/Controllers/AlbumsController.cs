using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoSite.Api.Data;
using PhotoSite.Api.DTOs;
using PhotoSite.Api.Models;
using System.IO;
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
    public class AlbumsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AlbumsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/albums
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] int? categoryId)
        {
            var q = _context.Albums.AsQueryable();
            if (categoryId.HasValue)
                q = q.Where(a => a.CategoryId == categoryId.Value);

            var albums = await q
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(albums);
        }

        // GET: api/albums/5
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var album = await _context.Albums
                .Include(a => a.Photos)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (album == null)
                return NotFound();

            return Ok(album);
        }

        // GET: api/albums/cover/4
        [HttpGet("cover/{id:int}")]
        [AllowAnonymous]
        public IActionResult GetCover(int id)
        {
            var album = _context.Albums.Find(id);
            if (album == null || string.IsNullOrEmpty(album.CoverPath))
                return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", album.CoverPath);
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

            // 🔐 ETag
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(fileBytes);
            var etag = Convert.ToBase64String(hash);

            if (Request.Headers["If-None-Match"] == etag)
                return StatusCode(304);

            Response.Headers["ETag"] = etag;
            Response.Headers["Cache-Control"] = "public,max-age=0,must-revalidate";

            return File(fileBytes, contentType);
        }

        // POST: api/albums
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateAlbumDto dto)
        {
            var categoryExists = await _context.AlbumCategories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
                return BadRequest("Категория не найдена");

            // 1️⃣ создаём альбом БЕЗ обложки
            var album = new Album
            {
                CategoryId = dto.CategoryId,
                Title = dto.Title,
                Description = dto.Description
            };

            _context.Albums.Add(album);
            await _context.SaveChangesAsync(); // получаем album.Id

            // 2️⃣ если есть файл — сохраняем
            if (dto.CoverFile != null && dto.CoverFile.Length > 0)
            {
                // проверка расширения
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(dto.CoverFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return BadRequest("Недопустимый формат файла");

                // путь к папке Storage
                var storagePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Storage",
                    "categories",
                    album.CategoryId.ToString(),
                    "albums",
                    album.Id.ToString());
                if (!Directory.Exists(storagePath))
                    Directory.CreateDirectory(storagePath);

                var fileName = "cover" + extension;
                var filePath = Path.Combine(storagePath, fileName);

                // сохраняем файл, но предварительно уменьшаем изображение до FullHD (по большей стороне)
                try
                {
                    await using var input = dto.CoverFile.OpenReadStream();
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

                // сохраняем путь в БД
                album.CoverPath = Path.Combine(
                        "categories",
                        album.CategoryId.ToString(),
                        "albums",
                        album.Id.ToString(),
                        fileName)
                    .Replace("\\", "/");
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetById), new { id = album.Id }, album);
        }


        // PUT: api/albums/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateAlbumDto dto, IFormFile? coverFile)
        {
            // 1️⃣ ищем альбом
            var album = await _context.Albums.FindAsync(id);
            if (album == null)
                return NotFound("Альбом не найден");

            // 2️⃣ обновляем текстовые поля
            album.Title = dto.Title;
            album.Description = dto.Description;

            // 3️⃣ если пришёл файл обложки
            if (coverFile != null && coverFile.Length > 0)
            {
                // проверка расширения
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return BadRequest("Недопустимый формат файла");

                // путь к папке Storage
                var storagePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Storage",
                    "categories",
                    album.CategoryId.ToString(),
                    "albums",
                    album.Id.ToString());
                if (!Directory.Exists(storagePath))
                    Directory.CreateDirectory(storagePath);

                var fileName = "cover" + extension;
                var filePath = Path.Combine(storagePath, fileName);

                // сохраняем файл (перезаписываем если был), но предварительно уменьшаем изображение до FullHD (по большей стороне)
                try
                {
                    await using var input = coverFile.OpenReadStream();
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

                // обновляем путь в БД
                album.CoverPath = Path.Combine(
                        "categories",
                        album.CategoryId.ToString(),
                        "albums",
                        album.Id.ToString(),
                        fileName)
                    .Replace("\\", "/");
            }

            // 4️⃣ сохраняем изменения
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // DELETE: api/albums/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var album = await _context.Albums.FindAsync(id);
            if (album == null)
                return NotFound("Альбом не найден");

            // путь к папке альбома
            var albumFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Storage",
                "categories",
                album.CategoryId.ToString(),
                "albums",
                album.Id.ToString());

            // удаляем папку вместе с содержимым (если она существует)
            if (Directory.Exists(albumFolder))
            {
                Directory.Delete(albumFolder, true); // true = рекурсивно
            }

            // удаляем запись из БД
            _context.Albums.Remove(album);
            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
