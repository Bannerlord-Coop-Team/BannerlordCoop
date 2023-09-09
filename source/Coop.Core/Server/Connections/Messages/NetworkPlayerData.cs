using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A new player has been created event containing that player's data
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPlayerData : IEvent
{
    public NetworkPlayerData(NewPlayerHeroRegistered registrationData)
    {
        var playerData = registrationData.NewPlayerData;

        if (playerData == null) return;

        HeroData = playerData.HeroData;
        HeroStringId = playerData.HeroStringId;
        PartyStringId = playerData.PartyStringId;
        CharacterObjectStringId = playerData.CharacterObjectStringId;
        ClanStringId = playerData.ClanStringId;
    }
    [ProtoMember(1)]
    public string PlayerId { get; }
    
    [ProtoMember(2)]
    public string HeroStringId { get; }
    [ProtoMember(3)]
    public string PartyStringId { get; }
    [ProtoMember(4)]
    public string CharacterObjectStringId { get; }
    [ProtoMember(5)]
    public string ClanStringId { get; }
    [ProtoMember(6)]
    public byte[] HeroData { get; }
}
