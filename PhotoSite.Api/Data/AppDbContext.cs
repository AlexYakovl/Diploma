using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhotoSite.Api.Models;

namespace PhotoSite.Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options) 
        { 
        }

        public DbSet<AlbumCategory> AlbumCategories => Set<AlbumCategory>();
        public DbSet<Album> Albums => Set<Album>();
        public DbSet<Photo> Photos => Set<Photo>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<HeroPhoto> HeroPhotos => Set<HeroPhoto>();
        public DbSet<AboutPhoto> AboutPhotos => Set<AboutPhoto>();


    }
}
