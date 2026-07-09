namespace Core.Identity.Users.Errors;

public static class ValidationMessages
{
    public const string UsernameRequired = "Username is required";
    public const string EmailRequired = "Email is required";
    public const string InvalidEmailFormat = "Invalid email format";
    public const string PasswordRequired = "Password is required";
    public const string PasswordMinLength = "Password must be at least 8 characters";
    public const string PasswordsDoNotMatch = "Passwords do not match";
    public const string RefreshTokenRequired = "Refresh token is required";
}