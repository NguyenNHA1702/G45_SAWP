namespace Core.Configuration;

// First Admin account, created on startup only while no Admin account exists (e.g. a new
// production database). Supply the password through an environment variable or a secret store,
// never a committed file, and remove it once the account exists.
public class FirstAdminOptions
{
    public const string SectionName = "Seed:Admin";
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string FullName { get; set; } = "Quản trị viên Hệ thống";
    public string? Password { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) ||
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(Password);
}
