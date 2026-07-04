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
}
