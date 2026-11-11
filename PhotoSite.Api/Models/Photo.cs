using System.ComponentModel.DataAnnotations;

namespace PhotoSite.Api.Models
{
    public class Photo
    {
        public int Id { get; set; }
        [Required]
        public string FileName { get; set; } = string.Empty;

        public string? OriginalFileName { get; set; }
        
        public int AlbumId { get; set; }

        /// <summary>Width in pixels (stored file), after server resize. Null for older uploads.</summary>
        public int? Width { get; set; }

        /// <summary>Height in pixels, after server resize. Null for older uploads.</summary>
        public int? Height { get; set; }
    }
}
