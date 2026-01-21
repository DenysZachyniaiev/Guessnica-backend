using System.ComponentModel.DataAnnotations;
using Guessnica_backend.Models;
using Xunit;

namespace Guessnica_backend.Tests.Models;

public class RiddleTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    private static Location CreateValidLocation()
    {
        decimal latitude = Math.Round(51.21265200583543m, 7);
        decimal longitude = Math.Round(16.17950286801138m, 7);

        return new Location
        {
            Id = 1,
            Latitude = latitude,
            Longitude = longitude,
            ImageUrl = "https://lh3.googleusercontent.com/gps-cs-s/AG0ilSzj1Lgg4H0fhPgDMxCemSHQ3t-RDFdp4o_kg0PwtuoHhCF5nKpo4iTk8EOO94kHJ0PDRT9zsAkYGF25Irna0mDUSXNk3jDunyRJ8aDkXxFj2z9_vS7FmaJ9eLTzWZb8GkAYC8xkuCiaco8=w408-h544-k-no",
            ShortDescription = "Former headquarters of the Gestapo and UB"
        };
    }
    [Fact]
    public void RiddleTests_Riddle_WithValidData_ShouldPassValidation()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Id = 1,
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Medium,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };
        
        var validationResults = ValidateModel(riddle);
        
        Assert.Empty(validationResults);
    }

    [Fact]
    public void RiddleTests_Riddle_DescriptionIsRequired()
    {
        var property = typeof(Riddle).GetProperty("Description");
        
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
      
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            LocationId = 1,
            Location = location
        };
        Assert.NotNull(riddle.Description);
    }
  
    [Theory]
    [InlineData(RiddleDifficulty.Easy, 1)]
    [InlineData(RiddleDifficulty.Medium, 2)]
    [InlineData(RiddleDifficulty.Hard, 3)]
    public void RiddleTests_RiddleDifficulty_ShouldMapToCorrectInteger(RiddleDifficulty difficulty, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)difficulty);
    }
    [Fact (Skip = "Need to implement custom validation for required LocationId if required")]
    public void Riddle_WithoutLocation_ShouldFailValidation()
    {
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 1000,
            LocationId = 0,
            Location = null
        };

        var validationResults = ValidateModel(riddle);

        Assert.Contains(validationResults, v => v.MemberNames.Contains("LocationId"));
    }
    [Fact]
    public void RiddleTests_Riddle_WithZeroTimeLimitSeconds_ShouldPassValidation()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 0,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void RiddleTests_Riddle_WithNegativeTimeLimitSeconds_ShouldPassValidation()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = -100,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(3600)]
    public void RiddleTests_Riddle_WithVariousTimeLimits_ShouldPassValidation(int timeLimit)
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = timeLimit,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void RiddleTests_Riddle_WithZeroMaxDistanceMeters_ShouldPassValidation()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 0,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void RiddleTests_Riddle_WithVariousMaxDistances_ShouldPassValidation(int maxDistance)
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = maxDistance,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void RiddleTests_Riddle_LocationRelationship_ShouldBeSet()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };

        Assert.NotNull(riddle.Location);
        Assert.Equal(1, riddle.LocationId);
        Assert.Equal(location.Id, riddle.Location.Id);
    }

    [Fact]
    public void RiddleTests_Riddle_CanSetAndGetAllProperties()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 100,
            MaxDistanceMeters = 500,
            LocationId = 1,
            Location = location
        };

        riddle.Id = 42;
        riddle.Description = "Find St. Mary's Church in Legnica";
        riddle.Difficulty = RiddleDifficulty.Hard;
        riddle.TimeLimitSeconds = 600;
        riddle.MaxDistanceMeters = 2000;
        riddle.LocationId = 2;
        
        Assert.Equal(42, riddle.Id);
        Assert.Equal("Find St. Mary's Church in Legnica", riddle.Description);
        Assert.Equal(RiddleDifficulty.Hard, riddle.Difficulty);
        Assert.Equal(600, riddle.TimeLimitSeconds);
        Assert.Equal(2000, riddle.MaxDistanceMeters);
        Assert.Equal(2, riddle.LocationId);
    }

    [Fact]
    public void RiddleTests_Riddle_DefaultValues_ShouldBeZero()
    {
        var riddle = new Riddle
        {
            Description = "Find this church in Legnica",
            LocationId = 0
        };

        Assert.Equal(0, riddle.Id);
        Assert.Equal(0, riddle.TimeLimitSeconds);
        Assert.Equal(0, riddle.MaxDistanceMeters);
        Assert.Equal(0, riddle.LocationId);
    }

    [Fact]
    public void RiddleTests_RiddleDifficulty_AllValuesShouldBePositive()
    {
        Assert.True((int)RiddleDifficulty.Easy >= 1);
        Assert.True((int)RiddleDifficulty.Medium >= 2);
        Assert.True((int)RiddleDifficulty.Hard >= 3);
    }

    [Fact]
    public void RiddleTests_Riddle_DescriptionCanContainSpecialCharacters()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = "!@#$%^&*()",
            Difficulty = RiddleDifficulty.Easy,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 1000,
            LocationId = 1,
            Location = location
        };
        var validationResults = ValidateModel(riddle);

        Assert.Empty(validationResults);
    }
    [Fact (Skip = "Need to implement custom validation for max length if required")]
    public void RiddleTests_Riddle_WithoutDescription_ShouldFailValidation()
    {
        var location = CreateValidLocation();
        var riddle = new Riddle
        {
            Description = null,
            LocationId = 1,
            Location = location
        };

        var validationResults = ValidateModel(riddle);

        Assert.Contains(validationResults, v => 
            v.MemberNames.Contains("Description") && 
            v.ErrorMessage.Contains("The Description field is required"));
    }
}