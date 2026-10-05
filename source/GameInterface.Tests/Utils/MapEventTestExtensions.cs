using Common.Util;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Utils;

/// <summary>
/// v1.5 derives <see cref="MapEvent.EventType"/> from <see cref="MapEvent.Component"/> and keeps
/// <see cref="MapEvent.MapEventSettlement"/> on the component, so a test map event needs a component
/// of the matching type instead of the removed <c>_mapEventType</c> field.
/// </summary>
internal static class MapEventTestExtensions
{
    public static void SetBattleType(this MapEvent mapEvent, MapEvent.BattleTypes battleType)
    {
        MapEventComponent component = battleType switch
        {
            MapEvent.BattleTypes.FieldBattle => Create<FieldBattleEventComponent>(mapEvent),
            MapEvent.BattleTypes.Raid => Create<RaidEventComponent>(mapEvent),
            MapEvent.BattleTypes.IsForcingVolunteers => Create<ForceVolunteersEventComponent>(mapEvent),
            MapEvent.BattleTypes.IsForcingSupplies => Create<ForceSuppliesEventComponent>(mapEvent),
            MapEvent.BattleTypes.Siege => SiegeAssault(mapEvent, MapEvent.BattleTypes.Siege),
            MapEvent.BattleTypes.Hideout => Create<HideoutEventComponent>(mapEvent),
            MapEvent.BattleTypes.SallyOut => Create<SiegeSallyOutEventComponent>(mapEvent),
            MapEvent.BattleTypes.SiegeOutside => SiegeOutside(mapEvent),
            MapEvent.BattleTypes.BlockadeBattle => Blockade(mapEvent, isSallyOut: false),
            MapEvent.BattleTypes.BlockadeSallyOutBattle => Blockade(mapEvent, isSallyOut: true),
            MapEvent.BattleTypes.SiegeAmbush => Create<SiegeAmbushEventComponent>(mapEvent),
            _ => throw new ArgumentOutOfRangeException(nameof(battleType), battleType, "No v1.5 component reports this battle type."),
        };

        Settlement settlement = mapEvent.Component?.MapEventSettlement;
        component.MapEvent = mapEvent;
        component.MapEventSettlement = settlement;
        mapEvent.Component = component;
    }

    /// <summary>Sets the battle settlement, giving the map event a field battle component when it has none.</summary>
    public static void SetMapEventSettlement(this MapEvent mapEvent, Settlement settlement)
    {
        if (mapEvent.Component == null)
            mapEvent.SetBattleType(MapEvent.BattleTypes.FieldBattle);

        mapEvent.Component.MapEventSettlement = settlement;
    }

    private static SiegeAssaultEventComponent SiegeAssault(MapEvent mapEvent, MapEvent.BattleTypes eventType)
    {
        var component = Create<SiegeAssaultEventComponent>(mapEvent);
        component._eventType = eventType;
        return component;
    }

    private static SiegeOutsideEventComponent SiegeOutside(MapEvent mapEvent)
    {
        var component = Create<SiegeOutsideEventComponent>(mapEvent);
        component._eventType = MapEvent.BattleTypes.SiegeOutside;
        return component;
    }

    private static BlockadeBattleEventComponent Blockade(MapEvent mapEvent, bool isSallyOut)
    {
        var component = Create<BlockadeBattleEventComponent>(mapEvent);
        component._isSallyOut = isSallyOut;
        return component;
    }

    /// <summary>Runs the component's own (private) constructor so its field initializers run.</summary>
    private static T Create<T>(MapEvent mapEvent) where T : MapEventComponent
    {
        var withMapEvent = AccessTools.DeclaredConstructor(typeof(T), new[] { typeof(MapEvent) });
        if (withMapEvent != null) return (T)withMapEvent.Invoke(new object[] { mapEvent });

        var hideout = AccessTools.DeclaredConstructor(typeof(T), new[] { typeof(MapEvent), typeof(bool) });
        if (hideout != null) return (T)hideout.Invoke(new object[] { mapEvent, false });

        return ObjectHelper.SkipConstructor<T>();
    }
}
