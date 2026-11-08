#if DEBUG
using Common.Commands;
using Missions.Agents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles;

/// <summary>
/// Lists this client's stuck-missile world items by network id with their global frame, without changing mission
/// state. Every client names a stuck missile with the same id, so two clients' pages can be compared row by row.
/// </summary>
public sealed class StuckMissilesCoopCommand : ICoopCommand
{
    private const int MaximumOffset = 100000;
    private const int MaximumLimit = 64;
    private readonly INetworkWorldItemRegistry worldItemRegistry;

    public StuckMissilesCoopCommand(INetworkWorldItemRegistry worldItemRegistry)
    {
        this.worldItemRegistry = worldItemRegistry;
    }

    public string Prefix => "coop.debug.missile";
    public string Name => "stuck";
    public string Description => "Lists stuck-missile world items ordered by network id with their global frame, without changing mission state.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("offset", "Row offset 0..100000 in network id order, default 0.", false),
        new ExpectedArgs("limit", "Rows 1..64, default 16.", false)
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        int offset = 0, limit = 16;
        if (args.Count > 2
            || (args.Count > 0 && !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out offset))
            || (args.Count > 1 && !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out limit))
            || offset > MaximumOffset || limit < 1 || limit > MaximumLimit)
            return new CoopCommandResult(false, "Expected offset 0..100000 and limit 1..64.", "invalid_arguments");

        if (Mission.Current == null)
            return new CoopCommandResult(false, "No active mission.", "no_mission");

        KeyValuePair<Guid, SpawnedItemEntity>[] stuck = worldItemRegistry.GetAll()
            .Where(entry => entry.Value != null && !entry.Value.IsRemoved
                && entry.Value.GameEntity.IsValid && entry.Value.IsStuckMissile())
            .OrderBy(entry => entry.Key)
            .ToArray();

        var output = new StringBuilder();
        output.Append("STUCK_MISSILES total=").Append(stuck.Length.ToString(CultureInfo.InvariantCulture))
            .Append(" offset=").Append(offset.ToString(CultureInfo.InvariantCulture));
        foreach (KeyValuePair<Guid, SpawnedItemEntity> entry in stuck.Skip(offset).Take(limit))
        {
            MatrixFrame frame = entry.Value.GameEntity.GetGlobalFrame();
            Mat3 rotation = frame.rotation;
            output.Append('\n').Append(string.Format(CultureInfo.InvariantCulture,
                "STUCK_MISSILE worldItem={0} item={1} amount={2} o={3} s={4} f={5} u={6}",
                entry.Key, entry.Value.WeaponCopy.Item?.StringId, entry.Value.WeaponCopy.Amount,
                Format(frame.origin), Format(rotation.s), Format(rotation.f), Format(rotation.u)));
        }

        return new CoopCommandResult(true, output.ToString());
    }

    private static string Format(Vec3 value) =>
        string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", value.x, value.y, value.z);
}
#endif
