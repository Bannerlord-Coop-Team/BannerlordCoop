using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CreateNewClanParty : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string NewLeaderId;

    [ProtoMember(3)]
    public readonly string TargetClanId;

    [ProtoMember(4)]
    public readonly int PartyGoldLowerThreshold;

    public CreateNewClanParty(
        string mainHeroId,
        string newLeaderId,
        string targetClanId,
        int partyGoldLowerThreshold)
    {
        MainHeroId = mainHeroId;
        NewLeaderId = newLeaderId;
        TargetClanId = targetClanId;
        PartyGoldLowerThreshold = partyGoldLowerThreshold;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeClanPartyLeader : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string NewLeaderId;

    [ProtoMember(3)]
    public readonly string OldLeaderId;

    [ProtoMember(4)]
    public readonly string SelectedPartyId;

    [ProtoMember(5)]
    public readonly string MainPartyId;

    public ChangeClanPartyLeader(
        string mainHeroId,
        string newLeaderId,
        string oldLeaderId,
        string selectedPartyId,
        string mainPartyId)
    {
        MainHeroId = mainHeroId;
        NewLeaderId = newLeaderId;
        OldLeaderId = oldLeaderId;
        SelectedPartyId = selectedPartyId;
        MainPartyId = mainPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct DisbandClanParty : ICommand
{
    [ProtoMember(1)]
    public readonly string SelectedPartyId;

    public DisbandClanParty(string selectedPartyId)
    {
        SelectedPartyId = selectedPartyId;
    }
}