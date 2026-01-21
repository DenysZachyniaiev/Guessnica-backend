using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Guessnica_backend.Services;
using Guessnica_backend.Models;
using Guessnica_backend.Data;

namespace Guessnica_backend.Tests.Services;

public class LocationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly LocationService _service;
    private readonly string _testWebRootPath;

    public LocationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        _testWebRootPath = Path.Combine(Path.GetTempPath(), "LocationServiceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testWebRootPath);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.WebRootPath).Returns(_testWebRootPath);

        _service = new LocationService(_context, _envMock.Object);
    }
    private IFormFile CreateMockFormFile(string fileName, string contentType, long length = 1024)
    {
        var content = new byte[length];
        var stream = new MemoryStream(content);
        
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(length);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken token) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(target, token);
            });

        return fileMock.Object;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWebRootPath))
        {
            Directory.Delete(_testWebRootPath, true);
        }
        _context.Dispose();
    }
    [Fact]
    public async Task LocationServiceTests_GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LocationServiceTests_GetAllAsync_WithLocations_ReturnsAllLocations()
    {
        var locations = new List<Location>
        {
            new Location { Id = 1, Latitude = 51.20735m, Longitude = 16.16215m, ShortDescription = "Pomnik Jana Pawła II", ImageUrl = "/test1.jpg" },
            new Location { Id = 2, Latitude = 51.20765m, Longitude = 16.16756m, ShortDescription = "Śpiący Lew pomnik wojen o zjednoczenie Niemiec", ImageUrl = "/test2.jpg" }
        };
        await _context.Locations.AddRangeAsync(locations);
        await _context.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(l => l.ShortDescription == "Pomnik Jana Pawła II");
        result.Should().Contain(l => l.ShortDescription == "Śpiący Lew pomnik wojen o zjednoczenie Niemiec");
    }

    [Fact]
    public async Task LocationServiceTests_GetByIdAsync_ExistingLocation_ReturnsLocation()
    {
        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II",
            ImageUrl = "/test.jpg"
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var result = await _service.GetByIdAsync(1);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.ShortDescription.Should().Be("Pomnik Jana Pawła II");
    }

    [Fact]
    public async Task LocationServiceTests_GetByIdAsync_NonExistingLocation_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task LocationServiceTests_CreateAsync_ValidLocationWithImage_CreatesLocation()
    {
        var location = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II"
        };
        var image = CreateMockFormFile("test.jpg", "image/jpeg");

        var result = await _service.CreateAsync(location, image);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ImageUrl.Should().NotBeNullOrEmpty();
        result.ImageUrl.Should().StartWith("/images/locations/");
        result.ImageUrl.Should().EndWith(".jpg");

        var dbLocation = await _context.Locations.FindAsync(result.Id);
        dbLocation.Should().NotBeNull();
        dbLocation!.ShortDescription.Should().Be("Pomnik Jana Pawła II");
    }

    [Fact]
    public async Task LocationServiceTests_CreateAsync_NullImage_ThrowsArgumentException()
    {
        var location = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(location, null!));
    }

    [Fact]
    public async Task LocationServiceTests_CreateAsync_EmptyImage_ThrowsArgumentException()
    {
        var location = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II"
        };
        var emptyImage = CreateMockFormFile("test.jpg", "image/jpeg", 0);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(location, emptyImage));
    }

    [Fact]
    public async Task LocationServiceTests_CreateAsync_InvalidImageType_ThrowsException()
    {
        var location = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła IIw"
        };
        var invalidImage = CreateMockFormFile("test.txt", "text/plain");

        await Assert.ThrowsAsync<Exception>(() => _service.CreateAsync(location, invalidImage));
    }

    [Fact]
    public async Task LocationServiceTests_CreateAsync_PngImage_CreatesLocationSuccessfully()
    {
        var location = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II"
        };
        var image = CreateMockFormFile("test.png", "image/png");

        var result = await _service.CreateAsync(location, image);

        result.ImageUrl.Should().EndWith(".png");
    }

    [Fact]
    public async Task LocationServiceTests_UpdateAsync_ExistingLocation_UpdatesProperties()
    {
        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II",
            ImageUrl = "/test.jpg"
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var updated = new Location
        {
            Latitude = 51.20765m,
            Longitude = 16.16754m,
            ShortDescription = "Śpiący Lew pomnik wojen o zjednoczenie Niemiec"
        };

        var result = await _service.UpdateAsync(1, updated);

        result.Should().NotBeNull();
        result.Latitude.Should().Be(51.20765m);
        result.Longitude.Should().Be(16.16754m);
        result.ShortDescription.Should().Be("Śpiący Lew pomnik wojen o zjednoczenie Niemiec");
        result.ImageUrl.Should().Be("/test.jpg");
    }

    [Fact]
    public async Task LocationServiceTests_UpdateAsync_WithNewImage_UpdatesImageUrl()
    {
        var oldImagePath = Path.Combine(_testWebRootPath, "images/locations/old.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(oldImagePath)!);
        await File.WriteAllTextAsync(oldImagePath, "old image");

        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II",
            ImageUrl = "/images/locations/old.jpg"
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var updated = new Location
        {
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II zmienione zdjęcie"
        };
        var newImage = CreateMockFormFile("new.jpg", "image/jpeg");

        var result = await _service.UpdateAsync(1, updated, newImage);

        result.ImageUrl.Should().NotBe("/images/locations/old.jpg");
        result.ImageUrl.Should().StartWith("/images/locations/");
        File.Exists(oldImagePath).Should().BeFalse();
    }

    [Fact]
    public async Task LocationServiceTests_UpdateAsync_NonExistingLocation_ThrowsKeyNotFoundException()
    {
        var updated = new Location
        {
            Latitude = 51.20333m,
            Longitude = 16.15672m,
            ShortDescription = "Pomnik Jubileuszu 2000-lecia"
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(999, updated));
    }

    [Fact]
    public async Task LocationServiceTests_DeleteAsync_ExistingLocation_DeletesLocationAndImage()
    {
        var imagePath = Path.Combine(_testWebRootPath, "images/locations/test.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        await File.WriteAllTextAsync(imagePath, "test image");

        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła IIw",
            ImageUrl = "/images/locations/test.jpg"
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteAsync(1);

        result.Should().BeTrue();
        var dbLocation = await _context.Locations.FindAsync(1);
        dbLocation.Should().BeNull();
        File.Exists(imagePath).Should().BeFalse();
    }

    [Fact]
    public async Task LocationServiceTests_DeleteAsync_NonExistingLocation_ReturnsFalse()
    {
        var result = await _service.DeleteAsync(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LocationServiceTests_DeleteAsync_LocationWithoutImage_DeletesLocationOnly()
    {
        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II",
            ImageUrl = ""
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteAsync(1);

        result.Should().BeTrue();
        var dbLocation = await _context.Locations.FindAsync(1);
        dbLocation.Should().BeNull();
    }

    [Fact]
    public async Task LocationServiceTests_DeleteAsync_LocationWithWhitespaceImage_DeletesLocationOnly()
    {
        var location = new Location
        {
            Id = 1,
            Latitude = 51.20735m,
            Longitude = 16.16215m,
            ShortDescription = "Pomnik Jana Pawła II",
            ImageUrl = "   "
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteAsync(1);

        result.Should().BeTrue();
        var dbLocation = await _context.Locations.FindAsync(1);
        dbLocation.Should().BeNull();
    }

    [Fact]
    public async Task LocationServiceTests_CleanupUnusedImagesAsync_NoFolder_ReturnsZero()
    {
        var result = await _service.CleanupUnusedImagesAsync();

        result.Should().Be(0);
    }

    [Fact]
    public async Task LocationServiceTests_CleanupUnusedImagesAsync_WithUnusedImages_DeletesUnusedFiles()
    {
        var folder = Path.Combine(_testWebRootPath, "images/locations");
        Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(Path.Combine(folder, "used.jpg"), "used");
        var location = new Location
        {
            Id = 1,
            Latitude = 51.20333m,
            Longitude = 16.15672m,
            ShortDescription = "Pomnik Jubileuszu 2000-lecia",
            ImageUrl = "/images/locations/used.jpg"
        };
        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        await File.WriteAllTextAsync(Path.Combine(folder, "unused1.jpg"), "unused1");
        await File.WriteAllTextAsync(Path.Combine(folder, "unused2.jpg"), "unused2");

        var result = await _service.CleanupUnusedImagesAsync();

        result.Should().Be(2);
        File.Exists(Path.Combine(folder, "used.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "unused1.jpg")).Should().BeFalse();
        File.Exists(Path.Combine(folder, "unused2.jpg")).Should().BeFalse();
    }

    [Fact]
    public async Task LocationServiceTests_CleanupUnusedImagesAsync_AllImagesUsed_DeletesNothing()
    {
        var folder = Path.Combine(_testWebRootPath, "images/locations");
        Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(Path.Combine(folder, "image1.jpg"), "image1");
        await File.WriteAllTextAsync(Path.Combine(folder, "image2.jpg"), "image2");

        var locations = new List<Location>
        {
            new Location { Id = 1, Latitude = 52.2297m, Longitude = 21.0122m, ShortDescription = "Test1", ImageUrl = "/images/locations/image1.jpg" },
            new Location { Id = 2, Latitude = 50.0647m, Longitude = 19.9450m, ShortDescription = "Test2", ImageUrl = "/images/locations/image2.jpg" }
        };
        await _context.Locations.AddRangeAsync(locations);
        await _context.SaveChangesAsync();

        var result = await _service.CleanupUnusedImagesAsync();

        result.Should().Be(0);
        File.Exists(Path.Combine(folder, "image1.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "image2.jpg")).Should().BeTrue();
    }
}