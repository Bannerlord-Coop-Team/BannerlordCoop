using Common.Util;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Utils;

/// <summary>
/// <see cref="MapEvent.EventType"/> and <see cref="MapEvent.MapEventSettlement"/> come from the map event's component,
/// so tests give the map event a component of the matching type.
/// </summary>
public static class MapEventTestExtensions
{
    private static readonly AccessTools.FieldRef<MapEvent, MapEventComponent> ComponentField =
        AccessTools.FieldRefAccess<MapEvent, MapEventComponent>("<Component>k__BackingField");
    private static readonly AccessTools.FieldRef<MapEventComponent, MapEvent> ComponentMapEventField =
        AccessTools.FieldRefAccess<MapEventComponent, MapEvent>("<MapEvent>k__BackingField");
    private static readonly AccessTools.FieldRef<MapEventComponent, Settlement> ComponentSettlementField =
        AccessTools.FieldRefAccess<MapEventComponent, Settlement>("<MapEventSettlement>k__BackingField");

    /// <summary>Gives the map event a component that reports the given battle type.</summary>
    public static void SetBattleType(this MapEvent mapEvent, MapEvent.BattleTypes battleType)
    {
        MapEventComponent previous = ComponentField(mapEvent);
        // Like rewriting the old field with its current value, a matching component is left in place.
        if (previous?.GetBattleType() == battleType)
            return;

        // A new event gets a real component through the synced property, so it registers and replicates as the factories' do
        if (previous == null)
        {
            mapEvent.Component = Create(mapEvent, battleType, construct: true);
            return;
        }

        // An event that has one is re-stamped through the backing field, so the change stays on this instance
        MapEventComponent component = Create(mapEvent, battleType, construct: false);
        ComponentMapEventField(component) = mapEvent;
        ComponentSettlementField(component) = previous.MapEventSettlement;
        ComponentField(mapEvent) = component;
    }

    /// <summary>Puts back a component saved from this map event, on this instance only.</summary>
    public static void RestoreComponent(this MapEvent mapEvent, MapEventComponent component)
        => ComponentField(mapEvent) = component;

    /// <summary>Sets the battle settlement through the component.</summary>
    public static void SetMapEventSettlement(this MapEvent mapEvent, Settlement settlement)
    {
        if (mapEvent.Component == null)
            mapEvent.SetBattleType(MapEvent.BattleTypes.FieldBattle);

        mapEvent.Component.MapEventSettlement = settlement;
    }

    private static MapEventComponent Create(MapEvent mapEvent, MapEvent.BattleTypes battleType, bool construct)
    {
        switch (battleType)
        {
            case MapEvent.BattleTypes.FieldBattle:
            {
                var component = Make<FieldBattleEventComponent>(mapEvent, construct);
                // Without its constructor the component lacks its party position caches; OnAfterLoad
                // creates them, as for a component loaded from a save or replicated to a client.
                if (!construct) component.OnAfterLoad();
                return component;
            }
            case MapEvent.BattleTypes.Raid: return Make<RaidEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.IsForcingVolunteers: return Make<ForceVolunteersEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.IsForcingSupplies: return Make<ForceSuppliesEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.Hideout: return Make<HideoutEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.SallyOut: return Make<SiegeSallyOutEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.SiegeAmbush: return Make<SiegeAmbushEventComponent>(mapEvent, construct);
            case MapEvent.BattleTypes.Siege:
            {
                var component = Make<SiegeAssaultEventComponent>(mapEvent, construct);
                component._eventType = MapEvent.BattleTypes.Siege;
                return component;
            }
            case MapEvent.BattleTypes.SiegeOutside:
            {
                var component = Make<SiegeOutsideEventComponent>(mapEvent, construct);
                component._eventType = MapEvent.BattleTypes.SiegeOutside;
                return component;
            }
            case MapEvent.BattleTypes.BlockadeBattle:
            case MapEvent.BattleTypes.BlockadeSallyOutBattle:
            {
                var component = Make<BlockadeBattleEventComponent>(mapEvent, construct);
                component._isSallyOut = battleType == MapEvent.BattleTypes.BlockadeSallyOutBattle;
                return component;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(battleType), battleType, "No v1.5 component reports this battle type.");
        }
    }

    private static T Make<T>(MapEvent mapEvent, bool construct) where T : MapEventComponent
    {
        if (!construct) return ObjectHelper.SkipConstructor<T>();

        var withMapEvent = AccessTools.DeclaredConstructor(typeof(T), new[] { typeof(MapEvent) });
        if (withMapEvent != null) return (T)withMapEvent.Invoke(new object[] { mapEvent });

        var hideout = AccessTools.DeclaredConstructor(typeof(T), new[] { typeof(MapEvent), typeof(bool) });
        return (T)hideout.Invoke(new object[] { mapEvent, false });
    }
}
