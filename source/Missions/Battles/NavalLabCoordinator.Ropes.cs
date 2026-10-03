#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;

namespace Missions.Battles;

public interface INavalRopeCoordinator
{
    object ExecuteRope(Guid operationId, string kind, int sourceShip, int sourceStation, int targetStation);
    object RopeStatus();
}

public sealed partial class NavalLabCoordinator : INavalRopeCoordinator
{
    private readonly HashSet<(Guid Incarnation, int Slot)> finalRopesForwarded = new();

    // Final rope state is relayed after a hold too: it is the only way the other owner converges on the frozen lifecycle.
    private void ReceiveFinalRopes(MessagePayload<NetworkNavalLabRopeFinal> payload)
    {
        GameThread.RunSafe(() =>
        {
            var value = payload.What;
            var manifest = store.Current;
            if (!HasFixture || manifest.Mode != NavalLabMode.TwoClientNative || !value.IsValid
                || value.IncarnationId != manifest.IncarnationId || value.ShipId != manifest.Ships[value.Slot]) return;
            if (ModInformation.IsClient)
            {
                (controller as INavalNativeController)?.ReceiveFinalRopes(value);
                return;
            }
            if (payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || player.ControllerId != manifest.Controllers[value.Slot]
                || !finalRopesForwarded.Add((value.IncarnationId, value.Slot))) return;
            foreach (var owner in manifest.Controllers.Where(owner => owner != player.ControllerId))
                if (players.TryGetPeer(owner, out var target)) network.Send(target, value);
        }, context: nameof(ReceiveFinalRopes));
    }

    public object ExecuteRope(Guid operationId, string kind, int sourceShip, int sourceStation, int targetStation)
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Rope actions require the game thread.");
        if (kind != "throw" && kind != "miss" && kind != "cut" && kind != "plank-force") throw new ArgumentException("Use throw, miss, cut or plank-force.");
        return ExecuteCore(operationId, "rope-" + kind, sourceShip, sourceStation, false, targetStation);
    }

    public object RopeStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Rope status requires the game thread.");
        return (adapter as INavalRopeAdapter)?.InspectRopes() ?? new { unavailable = "no_native_rope_adapter" };
    }
}
#endif
