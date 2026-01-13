namespace Guessnica_backend.Services;

public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}