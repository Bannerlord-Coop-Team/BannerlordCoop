using Common.Messaging;

namespace Coop.Core.Server.Services.Save.Messages;

/// <summary>Reports whether the co-op session JSON for a save was written.</summary>
public readonly struct CoopSessionWritten : IEvent
{
    public string SaveName { get; }
    public bool Success { get; }

    public CoopSessionWritten(string saveName, bool success)
    {
        SaveName = saveName;
        Success = success;
    }
}
