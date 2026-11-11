namespace PhotoSite.Api.Models
{
    public class Review
    {
        public int Id { get; set; }

        public string Author { get; set; } = null!;

        public string? SocialLink { get; set; }

        public int? AlbumId { get; set; }

        public string Text { get; set; } = null!;

        public string? AvatarPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  

        public bool IsHidden { get; set; } = true;
    }

}
