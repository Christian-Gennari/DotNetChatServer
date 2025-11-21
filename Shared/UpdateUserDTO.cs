using System;

namespace Shared;

/// <summary>
/// Responsible for: transferring user account update requests between client and server.
/// Data transfer object used for changing username or password with old credentials for verification.
/// </summary>
public class UpdateUserDTO
{
    public string? OldUsername { get; set; }
    public string? NewUsername { get; set; }
    public string? Password { get; set; }
}