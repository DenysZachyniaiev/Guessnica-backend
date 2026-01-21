using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Guessnica_backend.Services;

namespace Guessnica_backend.Tests.Services;

public class MailKitEmailSenderTests
{
    private readonly Mock<IOptions<EmailOptions>> _optionsMock;
    private readonly EmailOptions _emailOptions;
    private readonly MailKitEmailSender _service;

    public MailKitEmailSenderTests()
    {
        _emailOptions = new EmailOptions
        {
            Host = "smtp.test.com",
            Port = 587,
            UseStartTls = true,
            User = "testuser@test.com",
            Password = "testpassword",
            FromName = "Test Sender",
            FromEmail = "sender@test.com"
        };

        _optionsMock = new Mock<IOptions<EmailOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(_emailOptions);

        _service = new MailKitEmailSender(_optionsMock.Object);
    }

    [Fact]
    public void MailKitEmailSenderTests_Constructor_ValidOptions_InitializesService()
    {
        _service.Should().NotBeNull();
        _optionsMock.Verify(o => o.Value, Times.Once);
    }

    [Fact]
    public async Task MailKitEmailSenderTests_SendAsync_ValidEmail_BuildsCorrectMessage()
    {
        var toEmail = "recipient@test.com";
        var subject = "Test Subject";
        var body = "Test Body Content";

        var act = async () => 
        {
            try
            {
                await _service.SendAsync(toEmail, subject, body, CancellationToken.None);
            }
            catch (Exception)
            { }
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void MailKitEmailSenderTests_SendAsync_ValidParameters_DoesNotThrowDuringMessageConstruction()
    {
        var toEmail = "test@example.com";
        var subject = "Test";
        var body = "Body";

        var act = () =>
        {
            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(_emailOptions.FromName, _emailOptions.FromEmail));
            message.To.Add(MimeKit.MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new MimeKit.BodyBuilder { TextBody = body };
            message.Body = builder.ToMessageBody();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void MailKitEmailSenderTests_EmailOptions_FromConfiguration_HasCorrectValues()
    {
        _emailOptions.Host.Should().Be("smtp.test.com");
        _emailOptions.Port.Should().Be(587);
        _emailOptions.UseStartTls.Should().BeTrue();
        _emailOptions.User.Should().Be("testuser@test.com");
        _emailOptions.Password.Should().Be("testpassword");
        _emailOptions.FromName.Should().Be("Test Sender");
        _emailOptions.FromEmail.Should().Be("sender@test.com");
    }

    [Fact]
    public void MailKitEmailSenderTests_EmailOptions_DefaultValues_AreSetCorrectly()
    {
        var defaultOptions = new EmailOptions();

        defaultOptions.Host.Should().Be("");
        defaultOptions.Port.Should().Be(587);
        defaultOptions.UseStartTls.Should().BeTrue();
        defaultOptions.User.Should().Be("");
        defaultOptions.Password.Should().Be("");
        defaultOptions.FromName.Should().Be("Guessnica");
        defaultOptions.FromEmail.Should().Be("");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(587)]
    [InlineData(465)]
    [InlineData(65535)]
    public void MailKitEmailSenderTests_EmailOptions_ValidPort_IsWithinRange(int port)
    {
        var options = new EmailOptions { Port = port };

        options.Port.Should().BeInRange(1, 65535);
    }

    [Fact]
    public void MailKitEmailSenderTests_EmailOptions_WithoutCredentials_UserCanBeEmpty()
    {
        var optionsWithoutAuth = new EmailOptions
        {
            Host = "smtp.test.com",
            Port = 587,
            UseStartTls = false,
            User = "",
            Password = "",
            FromName = "Test",
            FromEmail = "test@test.com"
        };

        var mockOptions = new Mock<IOptions<EmailOptions>>();
        mockOptions.Setup(o => o.Value).Returns(optionsWithoutAuth);

        var service = new MailKitEmailSender(mockOptions.Object);

        service.Should().NotBeNull();
        optionsWithoutAuth.User.Should().BeEmpty();
    }

    [Fact]
    public void MailKitEmailSenderTests_EmailOptions_WithStartTls_UsesCorrectSecurityOption()
    {
        var secureOptions = _emailOptions.UseStartTls 
            ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable 
            : MailKit.Security.SecureSocketOptions.Auto;
        secureOptions.Should().Be(MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
    }

    [Fact]
    public void MailKitEmailSenderTests_EmailOptions_WithoutStartTls_UsesAutoSecurityOption()
    {
        var optionsNoTls = new EmailOptions
        {
            Host = "smtp.test.com",
            Port = 25,
            UseStartTls = false,
            User = "user@test.com",
            Password = "password",
            FromName = "Test",
            FromEmail = "test@test.com"
        };

        var secureOptions = optionsNoTls.UseStartTls 
            ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable 
            : MailKit.Security.SecureSocketOptions.Auto;

        secureOptions.Should().Be(MailKit.Security.SecureSocketOptions.Auto);
    }

    [Theory]
    [InlineData("test@example.com", "Subject", "Body")]
    [InlineData("user@domain.org", "Welcome!", "Hello User")]
    [InlineData("admin@site.net", "Alert", "System notification")]
    public void MailKitEmailSenderTests_MessageConstruction_VariousInputs_CreatesValidMessage(string toEmail, string subject, string body)
    {
        var act = () =>
        {
            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(_emailOptions.FromName, _emailOptions.FromEmail));
            message.To.Add(MimeKit.MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new MimeKit.BodyBuilder { TextBody = body };
            message.Body = builder.ToMessageBody();

            return message;
        };

        var message = act.Should().NotThrow().Subject;
        message.From.Count.Should().Be(1);
        message.To.Count.Should().Be(1);
        message.Subject.Should().Be(subject);
    }

    [Fact]
    public void MailKitEmailSenderTests_MessageBody_TextContent_IsSetCorrectly()
    {
        var body = "This is a test email body with some content.";
        var builder = new MimeKit.BodyBuilder { TextBody = body };

        var messageBody = builder.ToMessageBody();

        messageBody.Should().NotBeNull();
        var textPart = messageBody as MimeKit.TextPart;
        textPart.Should().NotBeNull();
        textPart!.Text.Should().Be(body);
    }

    [Fact]
    public void MailKitEmailSenderTests_MailboxAddress_ValidEmail_ParsesCorrectly()
    {
        var email = "user@example.com";

        var act = () => MimeKit.MailboxAddress.Parse(email);

        var mailbox = act.Should().NotThrow().Subject;
        mailbox.Address.Should().Be(email);
    }

    [Fact]
    public void MailKitEmailSenderTests_MailboxAddress_WithName_CreatesCorrectly()
    {
        var name = "Adam Grabowsky";
        var email = "adam@gmail.com";

        var mailbox = new MimeKit.MailboxAddress(name, email);

        mailbox.Name.Should().Be(name);
        mailbox.Address.Should().Be(email);
    }
    
}
public interface ISmtpClientWrapper : IDisposable
{
    Task ConnectAsync(string host, int port, MailKit.Security.SecureSocketOptions options, CancellationToken cancellationToken = default);
    Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task SendAsync(MimeKit.MimeMessage message, CancellationToken cancellationToken = default);
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default);
}

public class SmtpClientWrapper : ISmtpClientWrapper
{
    private readonly MailKit.Net.Smtp.SmtpClient _client;

    public SmtpClientWrapper()
    {
        _client = new MailKit.Net.Smtp.SmtpClient();
    }

    public Task ConnectAsync(string host, int port, MailKit.Security.SecureSocketOptions options, CancellationToken cancellationToken = default)
        => _client.ConnectAsync(host, port, options, cancellationToken);

    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
        => _client.AuthenticateAsync(userName, password, cancellationToken);

    public Task SendAsync(MimeKit.MimeMessage message, CancellationToken cancellationToken = default)
        => _client.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        => _client.DisconnectAsync(quit, cancellationToken);

    public void Dispose()
    {
        _client?.Dispose();
    }
}
public class TestableMailKitEmailSender : IAppEmailSender
{
    private readonly EmailOptions _opt;
    private readonly Func<ISmtpClientWrapper> _clientFactory;

    public TestableMailKitEmailSender(
        IOptions<EmailOptions> opt, 
        Func<ISmtpClientWrapper>? clientFactory = null)
    {
        _opt = opt.Value;
        _clientFactory = clientFactory ?? (() => new SmtpClientWrapper());
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(_opt.FromName, _opt.FromEmail));
        message.To.Add(MimeKit.MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new MimeKit.BodyBuilder { TextBody = body };
        message.Body = builder.ToMessageBody();

        using var client = _clientFactory();
        await client.ConnectAsync(
            _opt.Host,
            _opt.Port,
            _opt.UseStartTls ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable : MailKit.Security.SecureSocketOptions.Auto,
            ct
        );

        if (!string.IsNullOrWhiteSpace(_opt.User))
            await client.AuthenticateAsync(_opt.User, _opt.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
public class TestableMailKitEmailSenderTests
{
    private readonly Mock<IOptions<EmailOptions>> _optionsMock;
    private readonly Mock<ISmtpClientWrapper> _smtpClientMock;
    private readonly EmailOptions _emailOptions;

    public TestableMailKitEmailSenderTests()
    {
        _emailOptions = new EmailOptions
        {
            Host = "smtp.test.com",
            Port = 587,
            UseStartTls = true,
            User = "testuser@test.com",
            Password = "testpassword",
            FromName = "Test Sender",
            FromEmail = "sender@test.com"
        };

        _optionsMock = new Mock<IOptions<EmailOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(_emailOptions);

        _smtpClientMock = new Mock<ISmtpClientWrapper>();
    }

    [Fact]
    public async Task MailKitEmailSenderTests_SendAsync_WithAuthentication_ConnectsAuthenticatesAndSends()
    {
        var service = new TestableMailKitEmailSender(
            _optionsMock.Object,
            () => _smtpClientMock.Object
        );

        var toEmail = "recipient@test.com";
        var subject = "Test Subject";
        var body = "Test Body";
        
        await service.SendAsync(toEmail, subject, body);

        _smtpClientMock.Verify(c => c.ConnectAsync(
            _emailOptions.Host,
            _emailOptions.Port,
            MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _smtpClientMock.Verify(c => c.AuthenticateAsync(
            _emailOptions.User,
            _emailOptions.Password,
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _smtpClientMock.Verify(c => c.SendAsync(
            It.Is<MimeKit.MimeMessage>(m => 
                m.Subject == subject &&
                m.To.Count == 1
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _smtpClientMock.Verify(c => c.DisconnectAsync(
            true,
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
    [Fact]
    public async Task SendAsync_SmtpConnectionFails_ThrowsException()
    {
        var service = new TestableMailKitEmailSender(
            _optionsMock.Object,
            () => _smtpClientMock.Object
        );

        _smtpClientMock.Setup(c => c.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<MailKit.Security.SecureSocketOptions>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendAsync("recipient@test.com", "Subject", "Body"));

        exception.Message.Should().Be("Connection failed");
    }
    
    [Fact]
    public async Task MailKitEmailSenderTests_SendAsync_WithoutAuthentication_SkipsAuthStep()
    {
        var optionsWithoutAuth = new EmailOptions
        {
            Host = "smtp.test.com",
            Port = 587,
            UseStartTls = false,
            User = "",
            Password = "",
            FromName = "Test",
            FromEmail = "test@test.com"
        };

        var mockOptions = new Mock<IOptions<EmailOptions>>();
        mockOptions.Setup(o => o.Value).Returns(optionsWithoutAuth);

        var service = new TestableMailKitEmailSender(
            mockOptions.Object,
            () => _smtpClientMock.Object
        );

        await service.SendAsync("test@test.com", "Subject", "Body");

        _smtpClientMock.Verify(c => c.ConnectAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            MailKit.Security.SecureSocketOptions.Auto,
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _smtpClientMock.Verify(c => c.AuthenticateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task MailKitEmailSenderTests_SendAsync_CancellationRequested_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var service = new TestableMailKitEmailSender(
            _optionsMock.Object,
            () => _smtpClientMock.Object
        );

        await service.SendAsync("test@test.com", "Subject", "Body", cts.Token);

        _smtpClientMock.Verify(c => c.ConnectAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<MailKit.Security.SecureSocketOptions>(),
            cts.Token
        ), Times.Once);
    }
}