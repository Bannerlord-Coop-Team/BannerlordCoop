using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Abstracts interacting with the MobileParty class in game
/// </summary>
public interface IMobilePartyInterface : IGameAbstraction
{
    /// <summary>
    /// Registers all parties in the game as controlled by <paramref name="ownerId"/>.
    /// </summary>
    /// <param name="ownerId">Owner to assign all parties</param>
    void RegisterAllPartiesAsControlled(string ownerId);
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyInterface>();

    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    public MobilePartyInterface(
        IObjectManager objectManager,
        IControlledEntityRegistry controlledEntityRegistry)
    {
        this.objectManager = objectManager;
        this.controlledEntityRegistry = controlledEntityRegistry;
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
