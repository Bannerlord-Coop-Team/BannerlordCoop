using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct StanceLinkConstructed : ICommand
{
    [ProtoMember(1)]
    public readonly string Faction1Id;
    [ProtoMember(2)]
    public readonly string Faction2Id;
    [ProtoMember(3)]
    public readonly StanceType StanceType;

    public StanceLinkConstructed(string faction1Id, string faction2Id, StanceType stanceType)
    {
        Faction1Id = faction1Id;
        Faction2Id = faction2Id;
        StanceType = stanceType;
    }
}
