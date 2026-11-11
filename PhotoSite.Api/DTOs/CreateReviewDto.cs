namespace PhotoSite.Api.DTOs;
using Microsoft.AspNetCore.Http;

public class CreateReviewDto
{
    public string Author { get; set; } = null!;
    public int? AlbumId { get; set; }
    public string Text { get; set; } = null!;
    public IFormFile? Avatar { get; set; }
}

