using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoSite.Api.Data;
using PhotoSite.Api.DTOs;
using PhotoSite.Api.Models;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    // ✅ ПУБЛИЧНО: получить опубликованные отзывы
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var reviews = await (
                from r in _context.Reviews.AsNoTracking()
                join a in _context.Albums.AsNoTracking() on r.AlbumId equals a.Id into ra
                from a in ra.DefaultIfEmpty()
                where !r.IsHidden
                orderby r.CreatedAt descending
                select new
                {
                    r.Id,
                    r.Author,
                    r.Text,
                    r.AvatarPath,
                    r.CreatedAt,
                    r.IsHidden,
                    AlbumId = a != null ? r.AlbumId : null,
                    AlbumTitle = a != null ? a.Title : null
                })
            .ToListAsync();

        return Ok(reviews);
    }

    // 🔐 АДМИН: получить все отзывы (включая скрытые)
    [Authorize]
    [HttpGet("admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var reviews = await (
                from r in _context.Reviews.AsNoTracking()
                join a in _context.Albums.AsNoTracking() on r.AlbumId equals a.Id into ra
                from a in ra.DefaultIfEmpty()
                orderby r.CreatedAt descending
                select new
                {
                    r.Id,
                    r.Author,
                    r.Text,
                    r.AvatarPath,
                    r.CreatedAt,
                    r.IsHidden,
                    AlbumId = a != null ? r.AlbumId : null,
                    AlbumTitle = a != null ? a.Title : null
                })
            .ToListAsync();

        return Ok(reviews);
    }

    // 🔐 АДМИН: получить только скрытые (на модерацию)
    [Authorize]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var reviews = await (
                from r in _context.Reviews.AsNoTracking()
                join a in _context.Albums.AsNoTracking() on r.AlbumId equals a.Id into ra
                from a in ra.DefaultIfEmpty()
                where r.IsHidden
                orderby r.CreatedAt descending
                select new
                {
                    r.Id,
                    r.Author,
                    r.Text,
                    r.AvatarPath,
                    r.CreatedAt,
                    r.IsHidden,
                    AlbumId = a != null ? r.AlbumId : null,
                    AlbumTitle = a != null ? a.Title : null
                })
            .ToListAsync();

        return Ok(reviews);
    }

    // ✅ ПУБЛИЧНО: добавить отзыв
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateReviewDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Author) || string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest("Автор и текст обязательны");

        if (dto.AlbumId.HasValue)
        {
            var albumExists = await _context.Albums.AnyAsync(a => a.Id == dto.AlbumId.Value);
            if (!albumExists)
                return BadRequest("Указанный альбом не найден");
        }

        // аватарка: сначала быстрые проверки, чтобы не создавать "висящие" отзывы
        string? avatarExt = null;
        var hasAvatar = dto.Avatar != null && dto.Avatar.Length > 0;
        if (hasAvatar)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            avatarExt = Path.GetExtension(dto.Avatar!.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(avatarExt))
                return BadRequest("Неверный формат аватарки. Поддерживаются: JPG, PNG, WEBP.");
        }

        var review = new Review
        {
            Author = dto.Author,
            Text = dto.Text,
            AlbumId = dto.AlbumId,
            IsHidden = true
        };

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync(); // получаем Id

            if (hasAvatar)
            {
                var folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Storage",
                    "reviews",
                    review.Id.ToString()
                );

                var fileName = "avatar" + avatarExt;
                var filePath = Path.Combine(folderPath, fileName);

                // сохраняем аватар, но уменьшаем (квадратный) — быстрее грузится и меньше занимает места
                try
                {
                    await using var input = dto.Avatar!.OpenReadStream();
                    using var image = await Image.LoadAsync(input);

                    // 256x256, Crop чтобы всегда был квадрат
                    image.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Crop,
                        Size = new Size(256, 256)
                    }));

                    Directory.CreateDirectory(folderPath);
                    await using var output = new FileStream(filePath, FileMode.Create);

                    switch (avatarExt)
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
                    // откатим insert, чтобы не создавать скрытые дубликаты
                    if (Directory.Exists(folderPath))
                        Directory.Delete(folderPath, true);

                    await tx.RollbackAsync();
                    return BadRequest("Аватарка не читается как изображение. Загрузите корректный файл JPG/PNG/WEBP.");
                }

                review.AvatarPath = Path
                    .Combine("reviews", review.Id.ToString(), fileName)
                    .Replace("\\", "/");

                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return Ok(review);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ✅ ПУБЛИЧНО: получить аватарку отзыва
    // GET: api/reviews/{id}/avatar
    [AllowAnonymous]
    [HttpGet("{id:int}/avatar")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(review.AvatarPath))
            return NotFound("Аватар отсутствует");

        var storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, review.AvatarPath));
        var fullStorageRoot = Path.GetFullPath(storageRoot);

        // защита от path traversal
        if (!fullPath.StartsWith(fullStorageRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Некорректный путь к файлу");

        if (!System.IO.File.Exists(fullPath))
            return NotFound("Файл не найден");

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

        // ETag (хеш файла)
        var hash = MD5.HashData(fileBytes);
        var etag = Convert.ToBase64String(hash);

        if (Request.Headers["If-None-Match"] == etag)
            return StatusCode(304);

        Response.Headers["ETag"] = etag;
        Response.Headers["Cache-Control"] = "public,max-age=0,must-revalidate";

        return File(fileBytes, contentType);
    }

    // 🔐 АДМИН: опубликовать/скрыть отзыв
    [Authorize]
    [HttpPut("{id:int}/visibility")]
    public async Task<IActionResult> SetVisibility(int id, [FromQuery] bool hidden)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.IsHidden = hidden;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // 🔐 ТОЛЬКО АДМИН: удалить отзыв
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return NotFound();

        var folderPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Storage",
            "reviews",
            review.Id.ToString()
        );

        if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, true);

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
