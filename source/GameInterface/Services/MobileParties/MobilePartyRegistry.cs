using Common;
using Common.Messaging;
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

    public MobilePartyRegistry(
        IControllerIdProvider controllerIdProvider,
        IMessageBroker messageBroker,
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.controllerIdProvider = controllerIdProvider;
        this.messageBroker = messageBroker;
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

        GameThread.RunSafe(() =>
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;
            if (objectManager == null) return;

            objectManager.AddMobileParty(obj);
            MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, presumed: false, out _);
        }, context: "MobilePartyRegistry.OnClientCreated");
    }

    public override void OnClientDestroyed(MobileParty obj, string id)
    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    // A destroy that reaches the client only as this registry message never ran the
                    // vanilla DestroyPartyAction locally: a server-side destroy inside an AllowedThread
                    // scope skips publishing DestroyPartyApplied, while its RemoveParty (deferred out of
                    // that scope by DestroyPartyActionPatches.ApplyInternalPrefix) still publishes
                    // InstanceDestroyed. Vanilla map UI tears down on MobilePartyDestroyed — the party
                    // nameplate only removes plates on that event or a visibility change — so without it
                    // the dead party's plate leaks and forever renders its post-teardown name, which for
                    // bandits (ActualClan nulled in OnRemoveParty) is "NameFailed - BanditPartyPatch".
                    // Fire the events vanilla ApplyInternal would have. Membership in the world list
                    // discriminates: vanilla OnRemoveParty removes the party from CampaignObjectManager,
                    // so a party still listed here never had its local vanilla destroy, and a party that
                    // did is not double-notified.
                    if (Campaign.Current.MobileParties.Contains(obj))
                    {
                        CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(obj, null);
                        CampaignEventDispatcher.Instance.OnMapInteractableDestroyed(obj.Party);
                    }

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
        }, context: "MobilePartyRegistry.OnClientDestroyed");
    }

    public override void OnServerCreated(MobileParty obj, string id)
    {
    }

    public override void OnServerDestroyed(MobileParty obj, string id)
    {
        obj.MemberRoster.Clear();
        obj.PrisonRoster.Clear();
        obj.Party.SetVisualAsDirty();

        messageBroker.Publish(this, new MobilePartyDestroyed(obj));

        // Sole publisher of the PartyBase teardown: PartyBaseRegistry.DestroyMethods is empty, so
        // a PartyBase's lifetime ends with its party's, while everything is still registered.
        messageBroker.Publish(this, new InstanceDestroyed<PartyBase>(obj.Party));
    }
}
