using System.ComponentModel.DataAnnotations;
using Guessnica_backend.Models;
using Xunit;

namespace Guessnica_backend.Tests.Models;

public class AppUserTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
    [Fact]
    public void AppUserTests_AppUser_WithValidData_ShouldPassValidation()
    {
        var appUser = new AppUser
        {
            Id = "user123",
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var validationResults = ValidateModel(appUser);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void AppUserTests_AppUser_DefaultDisplayName_ShouldBeEmptyString()
    {
        var appUser = new AppUser();

        Assert.Equal(string.Empty, appUser.DisplayName);
    }

    [Fact]
    public void AppUserTests_AppUser_WithEmptyDisplayName_ShouldPassValidation()
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = string.Empty
        };

        var validationResults = ValidateModel(appUser);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void AppUserTests_AppUser_WithDisplayNameExceeding50Characters_ShouldFailValidation()
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = new string('A', 51)
        };
        var validationResults = ValidateModel(appUser);

        Assert.Contains(validationResults, v =>
        {
            var errorMessage = "The field DisplayName must be a string or array type with a maximum length of '50'.";
            return v.MemberNames.Contains("DisplayName") && v.ErrorMessage == errorMessage;
        });
    }

    [Fact]
    public void AppUserTests_AppUser_WithDisplayNameExactly50Characters_ShouldPassValidation()
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = new string('A', 50)
        };

        var validationResults = ValidateModel(appUser);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("Adam")]
    [InlineData("Adam Grabowsky")]
    [InlineData("María García")]
    [InlineData("亚当")]
    [InlineData("User123")]
    [InlineData("Test-User_01")]
    public void AppUserTests_AppUser_WithVariousValidDisplayNames_ShouldPassValidation(string displayName)
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = displayName
        };

        var validationResults = ValidateModel(appUser);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void AppUserTests_AppUser_WithValidEmail_ShouldPassValidation()
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "valid@example.com",
            DisplayName = "User with valid email"
        };

        var validationResults = ValidateModel(appUser);

        Assert.Empty(validationResults);
    }

    [Fact (Skip = "Nedd to add email in AppUser model first")]
    public void AppUserTests_AppUser_WithInvalidEmailFormat_ShouldFailValidation()
    {
        var appUser = new AppUser
        {
            UserName = "testuser",
            Email = "invalid-email",
            DisplayName = "User"
        };

        var validationResults = ValidateModel(appUser);

        Assert.Contains(validationResults, v =>
        {
            return v.MemberNames.Contains("Email") && 
                   v.ErrorMessage == "Invalid email format.";
        });
    }

    [Fact]
    public void AppUserTests_AppUser_InheritsFromIdentityUser()
    {
        var appUser = new AppUser();

        Assert.IsAssignableFrom<Microsoft.AspNetCore.Identity.IdentityUser>(appUser);
    }

    [Fact (Skip = "Nedd to add email in AppUser model first")]
    public void AppUserTests_AppUser_HasAppropriateAttributesForEmailAndDisplayName()
    {
        var emailProperty = typeof(AppUser).GetProperty("Email");
        var displayNameProperty = typeof(AppUser).GetProperty("DisplayName");

        var emailAttr = emailProperty?.GetCustomAttributes(typeof(EmailAddressAttribute), false).FirstOrDefault();
        var displayNameAttr = displayNameProperty?.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>().FirstOrDefault();

        Assert.NotNull(emailAttr);
        Assert.NotNull(displayNameAttr);
        Assert.Equal(50, displayNameAttr?.Length);
    }
}