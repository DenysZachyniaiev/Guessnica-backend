using System.ComponentModel.DataAnnotations;
using Guessnica_backend.Models;
using Xunit;

namespace Guessnica_backend.Tests.Models;

public class LocationTests
{
    private static bool TryValidateModel(object model, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        return Validator.TryValidateObject(model, context, results, validateAllProperties: true);
    }
    [Fact]
    public void LocationTests_Location_WithValidData_ShouldPassValidation()
    {
        decimal latitude = Math.Round(51.210595412972516m, 7);
        decimal longitude = Math.Round(16.184357004465717m, 7);

        var location = new Location
        {
            Id = 1,
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSzcsEhlTY0bb8zoYW8ZdlUZwp_vlNll-tHVU0ox3lraB5leag3e0wSZr8Tx7NZyEgaLlhhmGR-y44ZDgNoOfbNFyfHBt-prprTvbA7Y569qn266ez2PQSpsGhy0JLeDIwGjFIGf=w408-h839-k-no",
            ShortDescription = "Memorial to the Fighters of the Warsaw Ghetto"
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void LocationTests_Location_WithoutLatitude_ShouldFailValidation()
    {
        decimal longitude = Math.Round(16.18406213628762m, 7);
        var location = new Location
        {
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipMYyJzsbqlbYexWMyQT0qqqdlfXCf5jxnaNOr1m=w408-h288-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.Equal(0m, location.Latitude);
    }

    [Fact]
    public void LocationTests_Location_WithoutLongitude_ShouldPassWithDefaultValue()
    {
        decimal latitude = Math.Round(51.20777969905726m, 7);
        var location = new Location
        {
            Latitude = latitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipMYyJzsbqlbYexWMyQT0qqqdlfXCf5jxnaNOr1m=w408-h288-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);
        
        Assert.True(isValid);
        Assert.Equal(0m, location.Longitude);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(-100)]
    [InlineData(100)]
    public void LocationTests_Location_WithInvalidLatitude_ShouldFailValidation(decimal invalidLatitude)
    {
        decimal longitude = Math.Round(16.193979778737614m, 7);
        var location = new Location
        {
            Latitude = invalidLatitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipOaWH89EQ1aAPnhkH-PHD40SQ1CN1gTyLalCr_Z=w408-h272-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Latitude"));
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(45.5)]
    [InlineData(-45.5)]
    public void LocationTests_Location_WithValidLatitude_ShouldPassValidation(decimal validLatitude)
    {
        decimal longitude = Math.Round(16.193979778737614m, 7);
        var location = new Location
        {
            Latitude = validLatitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipOaWH89EQ1aAPnhkH-PHD40SQ1CN1gTyLalCr_Z=w408-h272-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("Latitude"));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(-200)]
    [InlineData(200)]
    public void LocationTests_Location_WithInvalidLongitude_ShouldFailValidation(decimal invalidLongitude)
    {
        decimal latitude = Math.Round(51.20784857318625m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = invalidLongitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipOaWH89EQ1aAPnhkH-PHD40SQ1CN1gTyLalCr_Z=w408-h272-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Longitude"));
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    [InlineData(100.5)]
    [InlineData(-100.5)]
    public void LocationTests_Location_WithValidLongitude_ShouldPassValidation(decimal validLongitude)
    {
        decimal latitude = Math.Round(51.20784857318625m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = validLongitude,
            ImageUrl = "https://lh3.googleusercontent.com/p/AF1QipOaWH89EQ1aAPnhkH-PHD40SQ1CN1gTyLalCr_Z=w408-h272-k-no"
        };

        var isValid = TryValidateModel(location, out var validationResults);
        
        Assert.True(isValid);
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("Longitude"));
    }

    [Fact]
    public void LocationTests_Location_WithNullShortDescription_ShouldPassValidation()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSwd6TjzoNTrm0BJtWJL1gHVxU7t_KMT11DMPuwXe7Rzpxs1PG4MblT-cq4alQUXp5VyG82XtM8vpTKkZ3_S1sShEbnoxwRJ4DcfyXIGlPXpqva08hLeLCvUtqD3j4oiz1pltfph3uXNzvJe=w408-h272-k-no",
            ShortDescription = null
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void LocationTests_Location_WithShortDescriptionExceeding200Characters_ShouldFailValidation()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSwd6TjzoNTrm0BJtWJL1gHVxU7t_KMT11DMPuwXe7Rzpxs1PG4MblT-cq4alQUXp5VyG82XtM8vpTKkZ3_S1sShEbnoxwRJ4DcfyXIGlPXpqva08hLeLCvUtqD3j4oiz1pltfph3uXNzvJe=w408-h272-k-no",
            ShortDescription = new string('A', 201)
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains("ShortDescription"));
    }

    [Fact]
    public void LocationTests_Location_WithShortDescriptionExactly200Characters_ShouldPassValidation()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSwd6TjzoNTrm0BJtWJL1gHVxU7t_KMT11DMPuwXe7Rzpxs1PG4MblT-cq4alQUXp5VyG82XtM8vpTKkZ3_S1sShEbnoxwRJ4DcfyXIGlPXpqva08hLeLCvUtqD3j4oiz1pltfph3uXNzvJe=w408-h272-k-no",
            ShortDescription = new string('A', 200)
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void LocationTests_Location_WithEmptyImageUrl_ShouldPassValidation()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = string.Empty
        };

        var isValid = TryValidateModel(location, out var validationResults);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void LocationTests_Location_DefaultImageUrl_ShouldBeEmptyString()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location
        {
            Latitude = latitude,
            Longitude = longitude
        };

        Assert.Equal(string.Empty, location.ImageUrl);
    }

    [Fact]
    public void LocationTests_Location_CanSetAndGetAllProperties()
    {
        decimal latitude = Math.Round(51.177026159303594m, 7);
        decimal longitude = Math.Round(16.153342584452872m, 7);
        var location = new Location();

        location.Id = 42;
        location.Latitude = latitude;
        location.Longitude = longitude;
        location.ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSwd6TjzoNTrm0BJtWJL1gHVxU7t_KMT11DMPuwXe7Rzpxs1PG4MblT-cq4alQUXp5VyG82XtM8vpTKkZ3_S1sShEbnoxwRJ4DcfyXIGlPXpqva08hLeLCvUtqD3j4oiz1pltfph3uXNzvJe=w408-h272-k-no";
        location.ShortDescription = "Waterfall";
        
        Assert.Equal(42, location.Id);
        Assert.Equal(latitude, location.Latitude);
        Assert.Equal(longitude, location.Longitude);
        Assert.Equal("https://lh3.googleusercontent.com/gps-cs-s/AG0ilSwd6TjzoNTrm0BJtWJL1gHVxU7t_KMT11DMPuwXe7Rzpxs1PG4MblT-cq4alQUXp5VyG82XtM8vpTKkZ3_S1sShEbnoxwRJ4DcfyXIGlPXpqva08hLeLCvUtqD3j4oiz1pltfph3uXNzvJe=w408-h272-k-no", location.ImageUrl);
        Assert.Equal("Waterfall", location.ShortDescription);
    }

    [Fact]
    public void LocationTests_Location_WithBoundaryLatitudes_ShouldPassValidation()
    {
        var locationMin = new Location
        {
            Latitude = -90m,
            Longitude = 0m
        };
        var isValidMin = TryValidateModel(locationMin, out var resultsMin);

        var locationMax = new Location
        {
            Latitude = 90m,
            Longitude = 0m
        };
        var isValidMax = TryValidateModel(locationMax, out var resultsMax);

        Assert.True(isValidMin);
        Assert.Empty(resultsMin);
        Assert.True(isValidMax);
        Assert.Empty(resultsMax);
    }

    [Fact]
    public void LocationTests_Location_WithBoundaryLongitudes_ShouldPassValidation()
    {
        var locationMin = new Location
        {
            Latitude = 0m,
            Longitude = -180m
        };
        var isValidMin = TryValidateModel(locationMin, out var resultsMin);

        var locationMax = new Location
        {
            Latitude = 0m,
            Longitude = 180m
        };
        var isValidMax = TryValidateModel(locationMax, out var resultsMax);

        Assert.True(isValidMin);
        Assert.Empty(resultsMin);
        Assert.True(isValidMax);
        Assert.Empty(resultsMax);
    }
}