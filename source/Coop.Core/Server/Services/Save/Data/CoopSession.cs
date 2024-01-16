using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Save.Data;

/// <summary>
/// Represents the current state of the game that the game transfer
/// couldn't handle
/// </summary>
public interface ICoopSession
{
    string UniqueGameId { get; set; }
    GameObjectGuids GameObjectGuids { get; set; }
}

/// <inheritdoc cref="ICoopSession"/>
[ProtoContract]
public class CoopSession : ICoopSession
{
    [ProtoMember(1)]
    public string UniqueGameId { get; set; }
    [ProtoMember(2)]
    public GameObjectGuids GameObjectGuids { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is CoopSession session == false) return false;

        if (UniqueGameId != session.UniqueGameId) return false;

        if (GameObjectGuids.Equals(session.GameObjectGuids) == false) return false;

        return true;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
