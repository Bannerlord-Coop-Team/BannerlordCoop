using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyVisuals;

internal class PartyVisualRegistry : AutoRegistryBase<MobilePartyVisual>
{
    public PartyVisualRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
        Logger.Information("PartyVisualRegistry instantiated, constructors: {count}", Constructors.Count());
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MobilePartyVisual), new Type[] { typeof(PartyBase)}),
    };


    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var visualManager = MobilePartyVisualManager.Current;

        if (visualManager == null)
        {
            Logger.Error("Unable to register party visuals when PartyVisualManager is null");
            return;
        }

        //foreach (var party in MobileParty.All)
        //{
        //    var mobilePartyVisual = party.Party.GetPartyVisual();

        //    if (mobilePartyVisual == null) continue;

        //    objectManager.AddExisting(party.StringId, mobilePartyVisual);
        //}

        foreach (MobilePartyVisual visual in visualManager._visualsFlattened)
        {
            var party = visual.MapEntity.MobileParty;
            RegisterExistingObject(party.StringId, visual);
        }
    }

    public override void OnClientCreated(MobilePartyVisual obj, string id)
    {
        using (new AllowedThread())
        {
            if (obj.MapEntity?.MapFaction == null) return;
            obj.ValidateIsDirty();
        }
    }

    public override void OnClientDestroyed(MobilePartyVisual obj, string id)
    {
    }

    public override void OnServerCreated(MobilePartyVisual obj, string id)
    {
    }

    public override void OnServerDestroyed(MobilePartyVisual obj, string id)
    {
    }
}