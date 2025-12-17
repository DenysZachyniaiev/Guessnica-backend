using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Guessnica_backend.Services;

public class MailKitEmailSender : IAppEmailSender
{
    private readonly EmailOptions _opt;
    public MailKitEmailSender(Microsoft.Extensions.Options.IOptions<EmailOptions> opt) => _opt = opt.Value;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _opt.Host,
            _opt.Port,
            _opt.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto,
            ct
        );

        if (!string.IsNullOrWhiteSpace(_opt.User))
            await client.AuthenticateAsync(_opt.User, _opt.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}