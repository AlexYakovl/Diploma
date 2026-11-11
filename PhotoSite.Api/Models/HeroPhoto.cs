namespace PhotoSite.Api.Models
{
    public class HeroPhoto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
