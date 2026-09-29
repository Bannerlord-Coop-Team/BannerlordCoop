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
        // Preserve concrete-type lookup precedence for compact IDs without trying a Clan as a Kingdom.
        var lookupId = id;
        if (!string.IsNullOrEmpty(id))
        {
            if (objectManager.Contains($"Kingdom_{id}"))
                lookupId = $"Kingdom_{id}";
            else if (objectManager.Contains($"Clan_{id}") &&
                !(objectManager.TryGetObject(id, out object direct) && direct is Kingdom))
                lookupId = $"Clan_{id}";
        }
        if (objectManager.TryGetObject(lookupId, out faction)) return true;
        Logger.Debug("Faction not found in IFactionInterface with id: {id}", id);
        faction = null;
        return false;
    }
}
