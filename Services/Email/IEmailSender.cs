namespace DammaniAPI.Services.Email;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string email, string language, string resetUrl, CancellationToken ct);
    Task SendStaffInviteAsync(string email, string language, string shopName, string inviteUrl, CancellationToken ct);
    Task SendContactMessageAsync(
        string inboxEmail,
        string fromEmail,
        string? fromName,
        string? topic,
        string message,
        CancellationToken ct);
}
