using Common.Messaging;
using Common.Network.Session;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>
/// Host a campaign-free session for a mod. The payload is opaque to Coop: every member receives it on joining and the
/// mod decides what it means.
/// </summary>
public record AttemptHostSession : ICommand
{
    /// <summary>A cap that keeps the command line short; the engine limit was not measured.</summary>
    public const int MaxPayloadBytes = 8192;

    public AttemptHostSession(string modId, byte[] payload, string password, ServerVisibility visibility)
    {
        ModId = modId;
        Payload = payload;
        Password = password;
        Visibility = visibility;
    }

    public string ModId { get; }
    public byte[] Payload { get; }
    public string Password { get; }
    public ServerVisibility Visibility { get; }
}
