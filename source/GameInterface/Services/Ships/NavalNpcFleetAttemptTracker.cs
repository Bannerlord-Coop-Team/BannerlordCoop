using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Game thread] Each open naval battle side's recorded AI fleet attempt: the server replays it to joining clients and a client's
/// launcher caps a late joiner against it. The spawner's latch stays the mission's own, set at once on the host.
/// </summary>
public interface INavalNpcFleetAttemptTracker
{
    /// <summary>Keeps the side's first attempt in its battle; returns whether this one was new.</summary>
    bool Record(NetworkNpcFleetAttemptRecorded attempt);

    /// <summary>The side's attempt in the battle, if one was recorded and the battle is still open.</summary>
    bool TryGet(string mapEventId, BattleSideEnum side, out NetworkNpcFleetAttemptRecorded attempt);

    /// <summary>Every recorded attempt of an open battle.</summary>
    IEnumerable<NetworkNpcFleetAttemptRecorded> All { get; }
}

/// <inheritdoc cref="INavalNpcFleetAttemptTracker"/>
public class NavalNpcFleetAttemptTracker : INavalNpcFleetAttemptTracker
{
    private readonly Dictionary<(string MapEventId, BattleSideEnum Side), NetworkNpcFleetAttemptRecorded> attempts =
        new Dictionary<(string MapEventId, BattleSideEnum Side), NetworkNpcFleetAttemptRecorded>();
    private readonly IObjectManager objectManager;

    // Takes the object manager that tells which recorded battles are still open.
    public NavalNpcFleetAttemptTracker(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool Record(NetworkNpcFleetAttemptRecorded attempt)
    {
        DropEndedBattles();
        var key = (attempt.InstanceId, attempt.Side);
        if (attempts.ContainsKey(key)) return false;

        attempts.Add(key, attempt);
        return true;
    }

    public bool TryGet(string mapEventId, BattleSideEnum side, out NetworkNpcFleetAttemptRecorded attempt)
    {
        DropEndedBattles();
        return attempts.TryGetValue((mapEventId, side), out attempt);
    }

    public IEnumerable<NetworkNpcFleetAttemptRecorded> All
    {
        get
        {
            DropEndedBattles();
            return attempts.Values.ToList();
        }
    }

    // An attempt lives only while its battle is open, on the server and on clients, which never see the finalize event.
    private void DropEndedBattles()
    {
        foreach (var key in attempts.Keys.ToList())
        {
            if (objectManager.TryGetObject<MapEvent>(key.MapEventId, out var mapEvent) && !mapEvent.IsFinalized) continue;

            attempts.Remove(key);
        }
    }
}
