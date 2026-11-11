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
    
    public class PhotosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PhotosController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/photos/upload?albumId=5
        [Authorize]
        [HttpPost("upload")]
        
        public async Task<IActionResult> UploadPhotos([FromQuery] int albumId, [FromForm] IFormFileCollection files)
        {
            // 1️⃣ проверяем, что альбом существует
            var album = await _context.Albums.FindAsync(albumId);
            if (album == null)
                return NotFound("Альбом не найден");

            // 2️⃣ проверяем, что файлы выбраны
            if (files == null || files.Count == 0)
                return BadRequest("Файлы не выбраны");

            // 3️⃣ разрешённые расширения
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            // 4️⃣ путь к папке photo внутри альбома
            var photosFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Storage",
                "categories",
                album.CategoryId.ToString(),
                "albums",
                albumId.ToString(),
                "photo");
            if (!Directory.Exists(photosFolder))
                Directory.CreateDirectory(photosFolder);

            var savedPhotos = new List<Photo>();

            // 5️⃣ сохраняем каждый файл
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    continue; // пропускаем недопустимые файлы

                // уникальное имя файла
                var fileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(photosFolder, fileName);

                // сохраняем на диск, но предварительно уменьшаем изображение до FullHD (по большей стороне)
                // это уменьшает размер и ускоряет загрузку
                int outW;
                int outH;
                try
                {
                    await using var input = file.OpenReadStream();
                    using var image = await Image.LoadAsync(input);

                    // Max 1920x1080 с сохранением пропорций
                    image.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(1920, 1080)
                    }));

                    outW = image.Width;
                    outH = image.Height;

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
                            // fallback: если внезапно расширение неизвестно
                            await image.SaveAsync(output, new JpegEncoder { Quality = 85 });
                            break;
                    }
                }
                catch
                {
                    return BadRequest($"Файл '{file.FileName}' не является корректным изображением");
                }

                // сохраняем запись в БД
                var photo = new Photo
                {
                    AlbumId = albumId,
                    FileName = fileName,
                    OriginalFileName = file.FileName,
                    Width = outW,
                    Height = outH
                };

                _context.Photos.Add(photo);
                savedPhotos.Add(photo);
            }

            await _context.SaveChangesAsync();

            // 6️⃣ возвращаем результат
            return Ok(new
            {
                message = "Файлы загружены",
                photos = savedPhotos.Select(p => new { p.Id, p.FileName, p.OriginalFileName })
            });
        }

        // GET: api/photos/file/15
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPhotoFile(int id)
        {
            var photo = await _context.Photos.FindAsync(id);
            if (photo == null)
                return NotFound();

            var album = await _context.Albums
                .AsNoTracking()
                .Select(a => new { a.Id, a.CategoryId })
                .FirstOrDefaultAsync(a => a.Id == photo.AlbumId);
            if (album == null)
                return NotFound("Альбом не найден");

            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Storage",
                "categories",
                album.CategoryId.ToString(),
                "albums",
                photo.AlbumId.ToString(),
                "photo",
                photo.FileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Файл не найден");

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // 🔐 ETag (хеш файла)
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(fileBytes);
            var etag = Convert.ToBase64String(hash);

            // если у клиента уже есть актуальная версия
            if (Request.Headers["If-None-Match"] == etag)
                return StatusCode(304);

            // заголовки кеша
            Response.Headers["ETag"] = etag;
            Response.Headers["Cache-Control"] = "public,max-age=0,must-revalidate";

            return File(fileBytes, contentType);
        }


        // GET: api/photos/album/5
        [HttpGet("album/{albumId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhotosByAlbum(int albumId)
        {
            // 1️⃣ проверяем, что альбом существует
            var album = await _context.Albums.FindAsync(albumId);
            if (album == null)
                return NotFound("Альбом не найден");

            // 2️⃣ получаем все фото альбома из БД
            var photos = await _context.Photos
                .Where(p => p.AlbumId == albumId)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.OriginalFileName,
                    // путь, который можно использовать на фронте
                    Url = $"/api/Photos/{p.Id}",
                    p.Width,
                    p.Height
                })
                .ToListAsync();

            return Ok(photos);
        }

        // DELETE: api/photos/15
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePhoto(int id)
        {
            // 1️⃣ находим фото в БД
            var photo = await _context.Photos.FindAsync(id);
            if (photo == null)
                return NotFound("Фото не найдено");

            var album = await _context.Albums
                .AsNoTracking()
                .Select(a => new { a.Id, a.CategoryId })
                .FirstOrDefaultAsync(a => a.Id == photo.AlbumId);
            if (album == null)
                return NotFound("Альбом не найден");

            // 2️⃣ путь к файлу на диске
            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Storage",
                "categories",
                album.CategoryId.ToString(),
                "albums",
                photo.AlbumId.ToString(),
                "photo",
                photo.FileName);

            // 3️⃣ удаляем файл, если он существует
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // 4️⃣ удаляем запись из БД
            _context.Photos.Remove(photo);
            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
