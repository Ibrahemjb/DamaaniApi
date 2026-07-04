namespace DammaniAPI.Services.Email;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string email, string language, string resetUrl, CancellationToken ct);
}
