using System.Data.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoSite.Api.Data;
using PhotoSite.Api.Models;

namespace PhotoSite.Api.Tests;

public sealed class PhotoSiteWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection = new SqliteConnection("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-key-that-is-at-least-32-bytes-long",
                ["Jwt:Issuer"] = "PhotoSite.Tests",
                ["Jwt:Audience"] = "PhotoSite.Tests",
                ["Jwt:ExpireMinutes"] = "30"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            _connection.Open();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.AuthenticationScheme,
                _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SeedIdentity(scope.ServiceProvider);
        SeedCatalog(db);
    }

    private static async Task SeedIdentity(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            Id = "test-admin",
            UserName = "test-admin",
            NormalizedUserName = "TEST-ADMIN",
            Email = "admin@photosite.test",
            NormalizedEmail = "ADMIN@PHOTOSITE.TEST"
        };

        if (await users.FindByNameAsync(user.UserName) == null)
        {
            var result = await users.CreateAsync(user, "Test-Password123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Could not seed test admin: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static void SeedCatalog(AppDbContext db)
    {
        if (db.AlbumCategories.Any())
            return;

        db.AlbumCategories.Add(new AlbumCategory
        {
            Id = 1,
            Title = "Wedding",
            Order = 1
        });
        db.SaveChanges();

        db.Albums.Add(new Album
        {
            Id = 1,
            CategoryId = 1,
            Title = "Summer wedding",
            Description = "A public test album"
        });
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();

        base.Dispose(disposing);
    }
}
