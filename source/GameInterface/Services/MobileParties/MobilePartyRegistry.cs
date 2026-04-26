using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Registry for <see cref="MobileParty"/> objects
/// </summary>
internal class MobilePartyRegistry : IAutoRegistry<MobileParty>
{
    private readonly IControlledEntityRegistry controlledEntityRegistry;

    ILogger Logger { get; }
    public MobilePartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IControlledEntityRegistry controlledEntityRegistry)
    {
        Logger = logger;
        this.controlledEntityRegistry = controlledEntityRegistry;
        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MobileParty))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<MobileParty> registry)
    {
        foreach (var party in MobileParty.All)
        {
            registry.RegisterExistingObject(party.StringId, party);
        }
    }

    public void OnClientCreated(MobileParty obj, string id)
    {
        using (new AllowedThread())
        {
            obj._isVisible = false;
            obj.IsActive = true;
            obj._isCurrentlyUsedByAQuest = false;
            obj.Party = new PartyBase(obj);
            obj.Anchor = new AnchorPoint(obj);
            obj.InitMembers();
            obj.InitCached();
            obj.Initialize();
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        Campaign.Current?.CampaignObjectManager?.AddMobileParty(obj);
    }

    public void OnClientDestroyed(MobileParty obj, string id)
    {
    }

    public void OnServerCreated(MobileParty obj, string id)
    {
    }

    public void OnServerDestroyed(MobileParty obj, string id)
    {
    }
}
