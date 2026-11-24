using System.ComponentModel.DataAnnotations;
using PhotoSite.Api.DTOs;
using PhotoSite.Api.Models;

namespace PhotoSite.Api.Tests;

[TestFixture]
public sealed class ModelValidationTests
{
    [Test]
    public void Album_WithValidRequiredFields_PassesValidation()
    {
        var album = new Album
        {
            CategoryId = 1,
            Title = "Wedding",
            Description = "A test album"
        };

        Assert.That(Validate(album), Is.Empty);
    }

    [Test]
    public void Album_WithOverlongTitle_FailsValidation()
    {
        var album = new Album
        {
            CategoryId = 1,
            Title = new string('A', 201)
        };

        Assert.That(Validate(album), Is.Not.Empty);
    }

    [Test]
    public void CreateAlbumDto_WithMissingTitle_FailsValidation()
    {
        var dto = new CreateAlbumDto
        {
            CategoryId = 1,
            Title = ""
        };

        Assert.That(Validate(dto), Is.Not.Empty);
    }

    [Test]
    public void Review_NewInstance_IsHiddenByDefault()
    {
        var review = new Review();

        Assert.That(review.IsHidden, Is.True);
    }

    [Test]
    public void Category_NewInstance_HasDefaultOrder()
    {
        var category = new AlbumCategory();

        Assert.That(category.Order, Is.Zero);
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            true);
        return results;
    }
}
