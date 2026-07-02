using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkVillagerPartyDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkVillagerPartyDestroyed(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDeleteExpiredLootedVillagers : ICommand
{
    [ProtoMember(1)]
    public readonly List<string> DeletedLootedVillagersIdsList;

    public NetworkDeleteExpiredLootedVillagers(
        List<string> deletedLootedVillagersIdsList)
    {
        DeletedLootedVillagersIdsList = deletedLootedVillagersIdsList;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddToLootedVillagers : ICommand
{
    [ProtoMember(1)]
    public readonly string VillagersPartyId;

    [ProtoMember(2)]
    public readonly CampaignTime CampaignTime;

    public NetworkAddToLootedVillagers(
        string villagersPartyId,
        CampaignTime campaignTime)
    {
        VillagersPartyId = villagersPartyId;
        CampaignTime = campaignTime;
    }
}