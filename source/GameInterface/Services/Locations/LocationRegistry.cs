using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Locations;

/// <summary>
/// Registry for <see cref="Location"/> type
/// </summary>
internal class LocationRegistry : AutoRegistryBase<Location>
{
    public LocationRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // Only the primary constructor is patched. The copy constructor Location(Location, LocationComplex)
    // chains into the primary one, so patching both would register every copied instance twice.
    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(
            typeof(Location),
            new Type[]
            {
                typeof(string), typeof(TextObject), typeof(TextObject), typeof(int),
                typeof(bool), typeof(bool), typeof(string), typeof(string),
                typeof(string), typeof(string), typeof(string[]), typeof(LocationComplex)
            })
    };

    // Locations are never destroyed; settlements keep their template-defined set for the whole campaign.
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (Settlement settlement in Campaign.Current.Settlements)
        {
            if (settlement.LocationComplex == null)
                continue;

            foreach (Location location in settlement.LocationComplex.GetListOfLocations())
            {
                // Location.StringId (e.g. "tavern") is only unique within its complex,
                // so the settlement id is prepended to disambiguate.
                RegisterExistingObject($"{settlement.StringId}_{location.StringId}", location);
            }
        }
    }

    public override void OnClientCreated(Location obj, string id)
    {
        // SkipConstructor leaves every collection null on network-created instances.
        obj._characterList = new List<LocationCharacter>();
        obj.SpecialItems = new List<ItemObject>();
        obj.LocationsOfPassages = new List<Location>();
    }

    public override void OnClientDestroyed(Location obj, string id)
    {
    }

    public override void OnServerCreated(Location obj, string id)
    {
    }

    public override void OnServerDestroyed(Location obj, string id)
    {
    }
}
