using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyVisuals;

internal class MobilePartyVisualRegistry : IAutoRegistry<MobilePartyVisual>
{
    ILogger Logger { get; }
    public MobilePartyVisualRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MobilePartyVisual), new Type[] { typeof(PartyBase) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<MobilePartyVisual> registry)
    {
        var visualManager = MobilePartyVisualManager.Current;

        if (visualManager == null)
        {
            Logger.Error("Unable to register party visuals when PartyVisualManager is null");
            return;
        }

        foreach (var party in MobileParty.All)
        {
            var mobilePartyVisual = party.Party.GetPartyVisual();

            if (mobilePartyVisual == null) continue;

            var networkId = $"{nameof(mobilePartyVisual)}_{party.StringId}";
            registry.RegisterExistingObject(networkId, mobilePartyVisual);
        }

        foreach (MobilePartyVisual visual in visualManager._visualsFlattened)
        {
            registry.RegisterNewObject(visual, out var _);
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

