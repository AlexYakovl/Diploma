using System.ComponentModel.DataAnnotations;

namespace PhotoSite.Api.DTOs
{
    public class UpdateAlbumDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
