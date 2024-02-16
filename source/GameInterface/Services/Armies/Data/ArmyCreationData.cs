using GameInterface.Services.Armies.Extensions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Data;

/// <summary>
/// Data required for creating an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyCreationData
{
    [ProtoMember(1)]
    public string StringId { get; }
    public string KingdomId { get; internal set; }
    public string LeaderPartyId { get; internal set; }
    public short ArmyType { get; internal set; }

    internal ArmyCreationData(Army instance, Kingdom kingdom, MobileParty party, Army.ArmyTypes armyType)
    {
        StringId = instance.GetStringId();
        KingdomId = kingdom.StringId;
        LeaderPartyId = party.StringId;
        ArmyType = (short)armyType;
    }
}
