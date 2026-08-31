namespace Coop.Core.Common.Commands;

/// <summary>
/// Identifies commands that must remain available outside the session command registrar lifecycle.
/// </summary>
public static class LegacyConnectionCommandExceptions
{
    public const string Prefix = "coop.debug.connection";

    public const string StartName = "start";

    public const string ReconnectName = "reconnect";
}
