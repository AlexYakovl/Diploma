using System.ComponentModel.DataAnnotations;

namespace PhotoSite.Api.Models
{
    public class Album
    {
        public int Id { get; set; }

        public int CategoryId { get; set; }
        public AlbumCategory? Category { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? CoverPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    }
}
