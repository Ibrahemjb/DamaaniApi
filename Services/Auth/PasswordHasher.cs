namespace DammaniAPI.Services.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
        => !string.IsNullOrWhiteSpace(hash) && BCrypt.Net.BCrypt.Verify(password, hash);
}
