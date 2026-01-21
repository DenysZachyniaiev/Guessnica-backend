using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;

namespace Guessnica_backend.Tests.Controllers
{
    public class RegisterTests
    {
        private readonly Mock<UserManager<AppUser>> _userManagerMock;
        private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly Mock<ILogger<AuthController>> _loggerMock;
        private readonly Mock<IAppEmailSender> _emailSenderMock;
        private readonly AuthController _controller;

        public RegisterTests()
        {
            var userStoreMock = new Mock<IUserStore<AppUser>>();
            
            _userManagerMock = new Mock<UserManager<AppUser>>(
                userStoreMock.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<IPasswordHasher<AppUser>>().Object,
                new[] { new Mock<IUserValidator<AppUser>>().Object },
                new[] { new Mock<IPasswordValidator<AppUser>>().Object },
                new Mock<ILookupNormalizer>().Object,
                new Mock<IdentityErrorDescriber>().Object,
                new Mock<IServiceProvider>().Object,
                new Mock<ILogger<UserManager<AppUser>>>().Object);

            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
            
            _signInManagerMock = new Mock<SignInManager<AppUser>>(
                _userManagerMock.Object,
                contextAccessorMock.Object,
                claimsFactoryMock.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<ILogger<SignInManager<AppUser>>>().Object,
                new Mock<IAuthenticationSchemeProvider>().Object,
                new Mock<IUserConfirmation<AppUser>>().Object);

            _jwtServiceMock = new Mock<IJwtService>();
            _loggerMock = new Mock<ILogger<AuthController>>();
            _emailSenderMock = new Mock<IAppEmailSender>();

            _controller = new AuthController(
                _userManagerMock.Object,
                _signInManagerMock.Object,
                _jwtServiceMock.Object,
                _loggerMock.Object);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.Action(It.IsAny<UrlActionContext>()))
                .Returns("http://localhost/auth/confirm-email?userId=test&token=test-token");

            _controller.Url = urlHelperMock.Object;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task Register_WithNewUser_CreatesUserAndSendsConfirmationEmail()
        {
            var dto = new RegisterDto
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                DisplayName = "New User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("newuser@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AppUser>()))
                .ReturnsAsync("token");

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            Assert.NotNull(response);

            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response)?.ToString();
            Assert.Equal("If the email is valid, instructions have been sent.", message);

            _userManagerMock.Verify(x => x.CreateAsync(
                It.Is<AppUser>(u => u.Email == "newuser@example.com" && u.DisplayName == "New User"),
                dto.Password), Times.Once);
            _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"), Times.Once);
            _emailSenderMock.Verify(x => x.SendAsync(
                "newuser@example.com",
                "Confirm your Guessnica account",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Register_WithExistingUser_SendsNotificationEmailAndReturnsGenericMessage()
        {
            var dto = new RegisterDto
            {
                Email = "existing@example.com",
                Password = "Password123!",
                DisplayName = "Existing User"
            };

            var existingUser = new AppUser
            {
                Id = "existing-id",
                Email = "existing@example.com",
                DisplayName = "Existing User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("existing@example.com"))
                .ReturnsAsync(existingUser);

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            Assert.NotNull(response);

            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response)?.ToString();
            Assert.Equal("If the email is valid, instructions have been sent.", message);

            _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
            _emailSenderMock.Verify(x => x.SendAsync(
                "existing@example.com",
                "Guessnica registration attempt",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Register_WithFailedCreation_ReturnsBadRequestWithErrors()
        {
            var dto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "weak",
                DisplayName = "Test User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Hasło jest za krótkie" }));

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = badRequestResult.Value;
            Assert.NotNull(response);

            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            Assert.Equal("Registration failed", messageProperty.GetValue(response)?.ToString());

            _emailSenderMock.Verify(x => x.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Register_TrimsAndLowercasesEmail()
        {
            var dto = new RegisterDto
            {
                Email = "  TEST@EXAMPLE.COM  ",
                Password = "Password123!",
                DisplayName = "Test User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AppUser>()))
                .ReturnsAsync("token");

            await _controller.Register(dto, _emailSenderMock.Object);

            _userManagerMock.Verify(x => x.FindByEmailAsync("test@example.com"), Times.Once);
            _userManagerMock.Verify(x => x.CreateAsync(
                It.Is<AppUser>(u => u.Email == "test@example.com" && u.UserName == "test@example.com"),
                dto.Password), Times.Once);
        }

        [Fact]
        public async Task Register_SetsEmailConfirmedToFalse()
        {
            var dto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "Password123!",
                DisplayName = "Test User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AppUser>()))
                .ReturnsAsync("token");

            await _controller.Register(dto, _emailSenderMock.Object);

            _userManagerMock.Verify(x => x.CreateAsync(
                It.Is<AppUser>(u => u.EmailConfirmed == false),
                dto.Password), Times.Once);
        }

        [Fact]
        public async Task Register_WithInvalidModel_ReturnsValidationProblem()
        {
            _controller.ModelState.AddModelError("Email", "The Email field is required.");
            var dto = new RegisterDto { Email = string.Empty, Password = "Password123!", DisplayName = "Test User" };

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);

            Assert.NotNull(objectResult);

            Assert.Equal(400, objectResult.StatusCode);

            var problemDetails = Assert.IsType<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(objectResult.Value);
            Assert.NotEmpty(problemDetails.Errors);

            _userManagerMock.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
            _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Register_WhenAddToRoleFails_ReturnsInternalServerError()
        {
            var dto = new RegisterDto
            {
                Email = "failrole@example.com",
                Password = "Password123!",
                DisplayName = "Fail Role User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("failrole@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Failed to assign role" }));

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);

            var response = objectResult.Value;
            Assert.NotNull(response);
            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response)?.ToString();
            Assert.DoesNotContain("Failed to assign role", message);
            Assert.Contains("Registration failed", message);
        }

        [Fact]
        public async Task Register_WhenEmailSendingFailsForNewUser_ReturnsInternalServerError()
        {
            var dto = new RegisterDto
            {
                Email = "failmail@example.com",
                Password = "Password123!",
                DisplayName = "Fail Email User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("failmail@example.com"))
                .ReturnsAsync((AppUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<AppUser>()))
                .ReturnsAsync("token");

            _emailSenderMock.Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("SMTP server connection failed."));

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);

            var response = objectResult.Value;
            Assert.NotNull(response);
            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response)?.ToString();
            Assert.DoesNotContain("SMTP", message);
            Assert.Contains("email could not be sent", message);
        }

        [Fact]
        public async Task Register_WhenEmailSendingFailsForExistingUser_ReturnsSuccess()
        {
            var dto = new RegisterDto
            {
                Email = "existing@example.com",
                Password = "Password123!",
                DisplayName = "Existing User"
            };

            var existingUser = new AppUser
            {
                Id = "existing-id",
                Email = "existing@example.com",
                DisplayName = "Existing User"
            };

            _userManagerMock.Setup(x => x.FindByEmailAsync("existing@example.com"))
                .ReturnsAsync(existingUser);

            _emailSenderMock.Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("SMTP server connection failed."));

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            Assert.NotNull(response);

            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response)?.ToString();
            Assert.Equal("If the email is valid, instructions have been sent.", message);
        }

        [Fact]
        public async Task Register_WithNullEmail_ReturnsValidationProblem()
        {
            var dto = new RegisterDto
            {
                Email = string.Empty,
                Password = "Password123!",
                DisplayName = "Test User"
            };

            var result = await _controller.Register(dto, _emailSenderMock.Object);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);

            Assert.Equal(400, badRequestResult.StatusCode);

            var validationProblem = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
            Assert.NotNull(validationProblem);

            _userManagerMock.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        }
    }
}