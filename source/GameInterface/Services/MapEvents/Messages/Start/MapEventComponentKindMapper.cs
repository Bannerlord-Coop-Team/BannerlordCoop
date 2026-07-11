using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal enum MapEventComponentKind
{
    None = 0,
    FieldBattle = 1,
    Raid = 2,
    ForceVolunteers = 3,
    ForceSupplies = 4,
    Hideout = 5,
    SiegeAmbush = 6,
    BlockadeBattle = 7
}

/// <summary>
/// Stable wire-kind mapping for the concrete vanilla map-event component types.
/// </summary>
internal static class MapEventComponentKindMapper
{
    public static bool TryGetKind(MapEventComponent component, out MapEventComponentKind kind)
    {
        if (component == null)
        {
            kind = MapEventComponentKind.None;
            return true;
        }

        return TryGetKind(component.GetType(), out kind);
    }

    public static bool TryGetKind(Type componentType, out MapEventComponentKind kind)
    {
        if (componentType == null)
        {
            kind = MapEventComponentKind.None;
            return true;
        }

        if (componentType == typeof(FieldBattleEventComponent))
            kind = MapEventComponentKind.FieldBattle;
        else if (componentType == typeof(RaidEventComponent))
            kind = MapEventComponentKind.Raid;
        else if (componentType == typeof(ForceVolunteersEventComponent))
            kind = MapEventComponentKind.ForceVolunteers;
        else if (componentType == typeof(ForceSuppliesEventComponent))
            kind = MapEventComponentKind.ForceSupplies;
        else if (componentType == typeof(HideoutEventComponent))
            kind = MapEventComponentKind.Hideout;
        else if (componentType == typeof(SiegeAmbushEventComponent))
            kind = MapEventComponentKind.SiegeAmbush;
        else if (componentType == typeof(BlockadeBattleMapEvent))
            kind = MapEventComponentKind.BlockadeBattle;
        else
        {
            kind = MapEventComponentKind.None;
            return false;
        }

        return true;
    }

    public static bool TryGetComponentType(MapEventComponentKind kind, out Type componentType)
    {
        switch (kind)
        {
            case MapEventComponentKind.None:
                componentType = null;
                return true;
            case MapEventComponentKind.FieldBattle:
                componentType = typeof(FieldBattleEventComponent);
                return true;
            case MapEventComponentKind.Raid:
                componentType = typeof(RaidEventComponent);
                return true;
            case MapEventComponentKind.ForceVolunteers:
                componentType = typeof(ForceVolunteersEventComponent);
                return true;
            case MapEventComponentKind.ForceSupplies:
                componentType = typeof(ForceSuppliesEventComponent);
                return true;
            case MapEventComponentKind.Hideout:
                componentType = typeof(HideoutEventComponent);
                return true;
            case MapEventComponentKind.SiegeAmbush:
                componentType = typeof(SiegeAmbushEventComponent);
                return true;
            case MapEventComponentKind.BlockadeBattle:
                componentType = typeof(BlockadeBattleMapEvent);
                return true;
            default:
                componentType = null;
                return false;
        }
    }
}
