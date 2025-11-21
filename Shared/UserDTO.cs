namespace Shared;

/// <summary>
/// Responsible for: transferring user login credentials between client and server.
/// Data transfer object used for authentication requests with nullable properties for validation.
/// </summary>
public class UserDTO
{
    // Properties are nullable to allow validation before sending to server
    // if any fields are null the login attempt is invalid
    public string? Username { get; set; } = null;
    public string? Password { get; set; } = null;
}