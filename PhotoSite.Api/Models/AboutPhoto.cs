namespace PhotoSite.Api.Models
{
    public class AboutPhoto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
