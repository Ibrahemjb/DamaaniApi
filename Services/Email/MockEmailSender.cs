namespace DammaniAPI.Services.Email;

public class MockEmailSender : IEmailSender
{
    private readonly ILogger<MockEmailSender> _logger;

    public MockEmailSender(ILogger<MockEmailSender> logger) => _logger = logger;

    public Task SendPasswordResetAsync(string email, string language, string resetUrl, CancellationToken ct)
    {
        var subject = language == "ar" ? "إعادة تعيين كلمة مرور ضماني" : "Reset your Damaani password";
        _logger.LogInformation(
            "Mock email send: To={Email}; Subject={Subject}; ResetUrl={ResetUrl}",
            email,
            subject,
            resetUrl);
        return Task.CompletedTask;
    }

    public Task SendStaffInviteAsync(string email, string language, string shopName, string inviteUrl, CancellationToken ct)
    {
        var subject = language == "ar" ? $"دعوة للانضمام إلى {shopName} على ضماني" : $"Join {shopName} on Damaani";
        _logger.LogInformation(
            "Mock email send: To={Email}; Subject={Subject}; InviteUrl={InviteUrl}",
            email,
            subject,
            inviteUrl);
        return Task.CompletedTask;
    }

    public Task SendContactMessageAsync(
        string inboxEmail,
        string fromEmail,
        string? fromName,
        string? topic,
        string message,
        CancellationToken ct)
    {
        var subject = string.IsNullOrWhiteSpace(topic)
            ? "Damaani contact form"
            : $"Damaani contact: {topic}";
        _logger.LogInformation(
            "Mock email send: To={Inbox}; From={FromEmail}; Name={Name}; Subject={Subject}; Body={Body}",
            inboxEmail,
            fromEmail,
            fromName,
            subject,
            message);
        return Task.CompletedTask;
    }
}
