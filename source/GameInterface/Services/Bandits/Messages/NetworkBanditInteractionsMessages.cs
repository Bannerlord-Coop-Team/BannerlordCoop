using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPlayerBanditInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string ConversationPartyId;

    [ProtoMember(3)]
    public readonly BanditInteractionsCampaignBehavior.PlayerInteraction Interaction;

    public NetworkSetPlayerBanditInteraction(
        string mainHeroId,
        string conversationPartyId,
        BanditInteractionsCampaignBehavior.PlayerInteraction interaction)
    {
        MainHeroId = mainHeroId;
        ConversationPartyId = conversationPartyId;
        Interaction = interaction;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBanditPartyDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkBanditPartyDestroyed(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBanditPartyScreenDoneCondition : ICommand
{
    [ProtoMember(1)]
    public readonly List<string> CharactersIds;

    public NetworkBanditPartyScreenDoneCondition(List<string> charactersIds)
    {
        CharactersIds = charactersIds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGetBanditMemberAndPrisonerRosters : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerClanId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly List<string> PartiesIds;

    [ProtoMember(4)]
    public readonly bool DoBanditsJoinPlayerSide;

    public NetworkGetBanditMemberAndPrisonerRosters(
        string playerClanId,
        string mainPartyId,
        List<string> partiesIds,
        bool doBanditsJoinPlayerSide)
    {
        PlayerClanId = playerClanId;
        MainPartyId = mainPartyId;
        PartiesIds = partiesIds;
        DoBanditsJoinPlayerSide = doBanditsJoinPlayerSide;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRosterScreenAfterBanditEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly List<string> PartiesIds;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    public NetworkRosterScreenAfterBanditEncounter(
        List<string> partiesIds,
        string mainPartyId)
    {
        PartiesIds = partiesIds;
        MainPartyId = mainPartyId;
    }
}
