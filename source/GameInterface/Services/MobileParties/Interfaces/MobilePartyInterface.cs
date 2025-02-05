using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Abstracts interacting with the MobileParty class in game
/// </summary>
internal interface IMobilePartyInterface : IGameAbstraction
{
    /// <summary>
    /// Handles the initialization of a newly transfered party
    /// </summary>
    /// <param name="party"></param>
    void ManageNewPlayerParty(MobileParty party);
    /// <summary>
    /// Registers all parties in the game as controlled by <paramref name="ownerId"/>.
    /// </summary>
    /// <param name="ownerId">Owner to assign all parties</param>
    void RegisterAllPartiesAsControlled(string ownerId);
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyInterface>();
    private static readonly MethodInfo PartyBase_OnFinishLoadState = typeof(PartyBase).GetMethod("OnFinishLoadState", BindingFlags.NonPublic | BindingFlags.Instance);

    private readonly MobilePartyRegistry partyRegistry;
    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public MobilePartyInterface(
        MobilePartyRegistry partyRegistry,
        IObjectManager objectManager,
        IControlledEntityRegistry controlledEntityRegistry)
    {
        this.partyRegistry = partyRegistry;
        this.objectManager = objectManager;
        this.controlledEntityRegistry = controlledEntityRegistry;
    }

    public void ManageNewPlayerParty(MobileParty party)
    {
        party.IsVisible = true;

        PartyBase_OnFinishLoadState.Invoke(party.Party, null);
    }

    public void RegisterAllPartiesAsControlled(string ownerId)
    {
        foreach(var party in partyRegistry)
        {
            controlledEntityRegistry.RegisterAsControlled(ownerId, party.Key);
        }
    }
}
