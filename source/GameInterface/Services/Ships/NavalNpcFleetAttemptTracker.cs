using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Game thread] Each open naval battle side's latest recorded AI fleet attempt: the server replays it to joining clients and a
/// client's launcher caps a late joiner against it. The spawner's latch stays the mission's own, set at once on the host.
/// </summary>
public interface INavalNpcFleetAttemptTracker
{
    /// <summary>Keeps the attempt as its side's latest in its battle; a re-engaged battle's new mission attempts again.</summary>
    void Record(NetworkNpcFleetAttemptRecorded attempt);

    /// <summary>The side's attempt in the battle, if one was recorded and the battle is still open.</summary>
    bool TryGet(string mapEventId, BattleSideEnum side, out NetworkNpcFleetAttemptRecorded attempt);

    /// <summary>Every recorded attempt of an open battle.</summary>
    IEnumerable<NetworkNpcFleetAttemptRecorded> All { get; }

    /// <summary>Drops the battle's attempts, once its mission instance is gone while the battle stays open (BR-017).</summary>
    void Forget(string mapEventId);
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

    public void Record(NetworkNpcFleetAttemptRecorded attempt)
    {
        DropEndedBattles();
        attempts[(attempt.InstanceId, attempt.Side)] = attempt;
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

    public void Forget(string mapEventId)
    {
        foreach (var key in attempts.Keys.Where(key => key.MapEventId == mapEventId).ToList())
            attempts.Remove(key);
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
