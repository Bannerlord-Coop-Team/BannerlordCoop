using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Patches for the mutations of <see cref="Location.SpecialItems"/>. Unlike the character list,
/// special items are save-persisted campaign state, so they are strictly server-authoritative.
/// </summary>
[HarmonyPatch]
internal class LocationSpecialItemsPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<Location>();

    [HarmonyPatch(typeof(Location), nameof(Location.AddSpecialItem))]
    [HarmonyPrefix]
    static bool AddSpecialItemPrefix(Location __instance, ItemObject itemObject)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Debug("Client add of special item {Item} to location {Location} blocked",
                itemObject?.StringId, __instance.StringId);
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new LocationSpecialItemAdded(__instance, itemObject));

        return true;
    }

    // Vanilla removes picked-up special items by mutating the collection directly from this
    // mission handler, bypassing every Location method, so the handler itself is the chokepoint.
    [HarmonyPatch(typeof(LocationItemSpawnHandler), nameof(LocationItemSpawnHandler.OnEntityRemoved))]
    [HarmonyPrefix]
    static bool OnEntityRemovedPrefix(LocationItemSpawnHandler __instance, GameEntity entity)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var spawnedEntities = __instance._spawnedEntities;
        var location = CampaignMission.Current?.Location;
        if (spawnedEntities == null || location == null) return true;

        var matchedItems = spawnedEntities
            .Where(pair => pair.Value == entity)
            .Select(pair => pair.Key)
            .ToList();

        if (matchedItems.Count == 0) return true;

        if (ModInformation.IsClient)
        {
            // Pickups stay visual-only on clients until a request flow exists; the synced
            // collection keeps the item.
            Logger.Debug("Client pickup of special item in location {Location} not applied", location.StringId);
            return false;
        }

        foreach (var item in matchedItems)
        {
            MessageBroker.Instance.Publish(__instance, new LocationSpecialItemRemoved(location, item));
        }

        return true;
    }

    public static void AddSpecialItem(Location location, ItemObject item)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                location.AddSpecialItem(item);
            }
        });
    }

    public static void RemoveSpecialItem(Location location, ItemObject item)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                location.SpecialItems?.Remove(item);
            }
        });
    }
}
