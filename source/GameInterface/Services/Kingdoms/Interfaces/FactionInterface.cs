using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Interfaces;

public interface IFactionInterface : IGameAbstraction
{
    bool TryGetFaction(string id, out IFaction faction);
}

public class FactionInterface : IFactionInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<FactionInterface>();

    private readonly IObjectManager objectManager;

    public FactionInterface(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryGetFaction(string id, out IFaction faction)
    {
        if (objectManager.TryGetObject(id, out Kingdom kingdom))
        {
            faction = kingdom;
            return true;
        }
        if (objectManager.TryGetObject(id, out Clan clan))
        {
            faction = clan;
            return true;
        }
        Logger.Debug("Faction not found in IFactionInterface with id: {id}", id);
        faction = null;
        return false;
    }
}