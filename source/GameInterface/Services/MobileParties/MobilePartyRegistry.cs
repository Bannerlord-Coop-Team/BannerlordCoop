using Common;
using Common.Messaging;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Registry for <see cref="MobileParty"/> objects
/// </summary>
internal class MobilePartyRegistry : AutoRegistryBase<MobileParty>
{
    public override bool Debug => true;

    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMessageBroker messageBroker;
    private readonly ISendCoalescer coalescer;

    public MobilePartyRegistry(
        IControllerIdProvider controllerIdProvider,
        IMessageBroker messageBroker,
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        ISendCoalescer coalescer = null)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.controllerIdProvider = controllerIdProvider;
        this.messageBroker = messageBroker;
        this.coalescer = coalescer;
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MobileParty));

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.RemoveParty))
    };


    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            RegisterExistingObject(party.StringId, party);
        }
    }

    public override void OnClientCreated(MobileParty obj, string id)
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
            obj.HasLandNavigationCapability = true;
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        GameThread.Run(() =>
        {
            Campaign.Current?.CampaignObjectManager?.AddMobileParty(obj);
        });
    }

    public override void OnClientDestroyed(MobileParty obj, string id)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    Campaign.Current.MobilePartyLocator.RemoveLocatable(obj);
                    Campaign.Current.VisualTrackerManager.RemoveTrackedObject(obj, true);
                    CampaignEventDispatcher.Instance.OnPartyRemoved(obj.Party);
                    Campaign.Current.CampaignObjectManager.RemoveMobileParty(obj);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to remove party");
                }
            }
        });
    }

    public override void OnServerCreated(MobileParty obj, string id)
    {
    }

    public override void OnServerDestroyed(MobileParty obj, string id)
    {
        try
        {
            obj.MemberRoster.Clear();
            obj.PrisonRoster.Clear();
            obj.Party.SetVisualAsDirty();

            messageBroker.Publish(this, new MobilePartyDestroyed(obj));

            // Sole publisher of the PartyBase teardown: PartyBaseRegistry.DestroyMethods is empty, so
            // a PartyBase's lifetime ends with its party's, while everything is still registered.
            messageBroker.Publish(this, new InstanceDestroyed<PartyBase>(obj.Party));
        }
        finally
        {
            // AutoRegistryHandler sends the MobileParty destroy after this callback. Drop any state
            // queued before or during teardown so no behavior update can follow that destroy.
            coalescer?.DropInstance(
                ObjectManager.Compact(id, typeof(MobileParty)));
        }
    }
}
