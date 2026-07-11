using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Extensions;

/// <summary>
/// Extensions for the <see cref="Army"/> class
/// </summary>
internal static class ArmyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<Army>();

    public static string GetStringId(this Army army)
    {
        if (army == null) return null;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to get {objectManager}", nameof(IObjectManager));
            return null;
        }

        if (objectManager.TryGetId(army, out var id) == false)
        {
            Logger.Error("{army} was not properly registered", army.Name);
        }

        return id;
    }

    /// <summary>
    /// Checks to see if an army includes at least one player party.
    /// </summary>
    public static bool IsPlayerArmy(this Army army)
    {
        if (army is null)
        {
            Logger.Error("{parameterName} was null", nameof(army));
            return false;
        }

        if (army.LeaderParty.IsPlayerParty()) return true;

        foreach (var party in army.LeaderParty.AttachedParties)
        {
            if (party.IsPlayerParty()) return true;
        }

        return false;
    }
}