using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Converts between the wire <see cref="SiegeEngineState"/> and the mission's
/// <see cref="MissionSiegeWeapon"/>, in both directions. Null-tolerant: protobuf-net deserializes an
/// empty array as null.
/// </summary>
public static class SiegeEngineStateConverter
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineState>();

    public static SiegeEngineState[] ToEngineStates(IEnumerable<IMissionSiegeWeapon> weapons)
    {
        if (weapons == null) return Array.Empty<SiegeEngineState>();

        var states = new List<SiegeEngineState>();
        foreach (var weapon in weapons)
        {
            states.Add(new SiegeEngineState(weapon.Type.StringId, weapon.Index, weapon.Health, weapon.MaxHealth));
        }

        return states.ToArray();
    }

    public static List<MissionSiegeWeapon> ToMissionWeapons(SiegeEngineState[] states)
    {
        var weapons = new List<MissionSiegeWeapon>(states?.Length ?? 0);
        if (states == null) return weapons;

        foreach (var state in states)
        {
            var engineType = MBObjectManager.Instance.GetObject<SiegeEngineType>(state.EngineTypeId);
            if (engineType == null)
            {
                Logger.Error("Unknown siege engine type {EngineTypeId} in a siege engine state", state.EngineTypeId);
                continue;
            }

            weapons.Add(MissionSiegeWeapon.CreateCampaignWeapon(engineType, state.Index, state.Health, state.MaxHealth));
        }

        return weapons;
    }
}
