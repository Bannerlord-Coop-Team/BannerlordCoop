using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.PlayerList;

public enum PlayerActivity
{
    None, Idle, Town, Village, Castle, Battle, Siege, Hideout, Travelling
}

/// <summary>Presentation data derived from an existing backend player registration.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record PlayerListEntry
{
    [ProtoMember(1)] public string ControllerId { get; set; }
    [ProtoMember(2)] public string PlatformName { get; set; }
    [ProtoMember(3)] public string HeroName { get; set; }
    [ProtoMember(4)] public bool Online { get; set; }
    [ProtoMember(5)] public PlayerActivity Activity { get; set; }
}

/// <summary>Replaces the client's presentation snapshot, not its player registry.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record NetworkPlayerList : IMessage
{
    [ProtoMember(1)] public PlayerListEntry[] Entries { get; }

    // Captures the server's current presentation rows.
    public NetworkPlayerList(PlayerListEntry[] entries) => Entries = entries;
}

/// <summary>Reports the local platform name after the player's campaign has loaded.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed record NetworkPlayerPlatformName : IMessage
{
    [ProtoMember(1)] public string Name { get; }

    // Carries a display name; player identity comes from the sending peer.
    public NetworkPlayerPlatformName(string name) => Name = name;
}
