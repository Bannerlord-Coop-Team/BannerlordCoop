using GameInterface.Services.Ships.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships;

/// <summary>
/// [Game thread] Each open naval battle side's AI fleet attempt, kept by the server for joining clients and by every client for
/// its launcher, which caps a late joiner's fleet against it.
/// </summary>
public interface INavalFleetRecords
{
    /// <summary>Keeps the side's first attempt in its battle; returns whether this one was new.</summary>
    bool Record(NetworkNpcFleetAttempted attempt);

    /// <summary>The side's attempt in the battle, if one was recorded.</summary>
    bool TryGet(string mapEventId, BattleSideEnum side, out NetworkNpcFleetAttempted attempt);

    /// <summary>Every recorded attempt, of every battle.</summary>
    IEnumerable<NetworkNpcFleetAttempted> All { get; }

    /// <summary>Drops the battle's attempts once it ended.</summary>
    void Forget(string mapEventId);
}

/// <inheritdoc cref="INavalFleetRecords"/>
public class NavalFleetRecords : INavalFleetRecords
{
    private readonly Dictionary<(string MapEventId, BattleSideEnum Side), NetworkNpcFleetAttempted> attempts =
        new Dictionary<(string MapEventId, BattleSideEnum Side), NetworkNpcFleetAttempted>();

    public bool Record(NetworkNpcFleetAttempted attempt)
    {
        var key = (attempt.InstanceId, attempt.Side);
        if (attempt.InstanceId == null || attempts.ContainsKey(key)) return false;

        attempts.Add(key, attempt);
        return true;
    }

    public bool TryGet(string mapEventId, BattleSideEnum side, out NetworkNpcFleetAttempted attempt) =>
        attempts.TryGetValue((mapEventId, side), out attempt);

    public IEnumerable<NetworkNpcFleetAttempted> All => attempts.Values.ToList();

    public void Forget(string mapEventId)
    {
        foreach (var key in attempts.Keys.Where(key => key.MapEventId == mapEventId).ToList())
            attempts.Remove(key);
    }
}
