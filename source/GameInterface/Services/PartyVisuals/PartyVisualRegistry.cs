using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals;

internal class PartyVisualRegistry : IAutoRegistry<MobilePartyVisual>
{
    ILogger Logger { get; }
    public PartyVisualRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
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
            objectManager.AddExisting(party.StringId, visual);
        }
    }

    public void OnClientCreated(MobilePartyVisual obj, string id)
    {
    }

    public void OnClientDestroyed(MobilePartyVisual obj, string id)
    {
    }

    public void OnServerCreated(MobilePartyVisual obj, string id)
    {
    }

    public void OnServerDestroyed(MobilePartyVisual obj, string id)
    {
    }
}