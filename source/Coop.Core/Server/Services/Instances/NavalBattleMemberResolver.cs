using Common.Logging;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace Coop.Core.Server.Services.Instances;

/// <summary>[Server, game thread] Resolves the sender of a naval battle report as a member of that battle while it is still open.</summary>
public interface INavalBattleMemberResolver
{
    /// <summary>
    /// The sender as a registered player in an open naval battle it is a member of, with the battle; logs why the report named
    /// <paramref name="what"/> is dropped otherwise.
    /// </summary>
    bool TryResolve(string controllerId, string instanceId, string what, out Player player, out MapEvent mapEvent);
}

/// <inheritdoc cref="INavalBattleMemberResolver"/>
public class NavalBattleMemberResolver : INavalBattleMemberResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalBattleMemberResolver>();

    private readonly IMissionManager missionManager;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    // Takes the player registry, the mission memberships and the object manager the battle resolves through.
    public NavalBattleMemberResolver(IMissionManager missionManager, IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.missionManager = missionManager;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool TryResolve(string controllerId, string instanceId, string what, out Player player, out MapEvent mapEvent)
    {
        mapEvent = null;
        if (!playerManager.TryGetPlayer(controllerId, out player))
        {
            Logger.Warning("[NavalSync] Dropped {What} from {Controller}: it is no registered player", what, controllerId);
            return false;
        }

        if (!missionManager.TryGetControllers(instanceId, out var members) || !members.Contains(controllerId))
        {
            Logger.Information("[NavalSync] Dropped {What} from {Controller}: it is not in battle {Instance}", what, controllerId,
                instanceId);
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(instanceId, out mapEvent)) return false;
        if (mapEvent.IsNavalMapEvent && !mapEvent.IsFinalized && !mapEvent.HasCalculatedResults()) return true;

        Logger.Information("[NavalSync] Dropped {What} from {Controller}: battle {Instance} is not at sea or already has its results",
            what, controllerId, instanceId);
        return false;
    }
}
