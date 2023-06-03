using GameInterface.Services.Entity;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces;

internal interface IMobilePartyInterface : IGameAbstraction
{
    void ManageNewParty(MobileParty party);

    void RegisterAllPartiesAsControlled(Guid ownerId);
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly MethodInfo PartyBase_OnFinishLoadState = typeof(PartyBase).GetMethod("OnFinishLoadState", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo AddMobileParty = typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly IMobilePartyRegistry _partyRegistry;
    private readonly IControlledEntityRegistry _controlledEntityRegistry;

    public MobilePartyInterface(
        IMobilePartyRegistry partyRegistry,
        IControlledEntityRegistry controlledEntityRegistry)
    {
        _partyRegistry = partyRegistry;
        _controlledEntityRegistry = controlledEntityRegistry;
    }

    public void ManageNewParty(MobileParty party)
    {
        AddMobileParty.Invoke(Campaign.Current.CampaignObjectManager, new object[] { party });

        party.IsVisible = true;

        PartyBase_OnFinishLoadState.Invoke(party.Party, null);
    }

    public void RegisterAllPartiesAsControlled(Guid ownerId)
    {
        foreach(var party in _partyRegistry)
        {
            _controlledEntityRegistry.RegisterAsControlled(ownerId, party.Key);
        }
    }
}
