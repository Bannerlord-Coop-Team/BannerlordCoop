using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages.Behavior;
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
public interface IMobilePartyInterface : IGameAbstraction
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

    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public MobilePartyInterface(
        IObjectManager objectManager,
        IControlledEntityRegistry controlledEntityRegistry)
    {
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
        foreach(var party in MobileParty.All)
        {
            if (objectManager.TryGetId(party, out var id) == false)
            {
                Logger.Error($"Failed to retrieve object id for MobileParty with identifier {party.Id}. Registration skipped.");
                continue;
            }

            controlledEntityRegistry.RegisterAsControlled(ownerId, id);
        }
    }
}
