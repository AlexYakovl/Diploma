using System.ComponentModel.DataAnnotations;

namespace PhotoSite.Api.Models
{
    public class AlbumCategory
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        public string? CoverPath { get; set; }

        public int Order { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Album> Albums { get; set; } = new List<Album>();
    }
}

