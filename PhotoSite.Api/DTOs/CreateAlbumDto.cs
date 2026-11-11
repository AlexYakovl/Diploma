using System.ComponentModel.DataAnnotations;

namespace PhotoSite.Api.DTOs
{
    public class CreateAlbumDto
    {
        [Required]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public IFormFile? CoverFile { get; set; }
    }
}
