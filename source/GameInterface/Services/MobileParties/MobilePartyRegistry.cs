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

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        // Assign the party's MBGUID now, before anything can observe the party. MBObjectBase.GetHashCode
        // is Id-based, and the deferred AddMobileParty below otherwise assigns the Id on the game thread
        // only after the map UI may already have keyed the party into an Id-hashed dictionary — the
        // movement sync can make a fresh party visible (locator + spotting) within the same frame, and
        // PartyNameplatesVM keys plates by party. A dictionary key whose hash mutates after insert is
        // unfindable, so such a party's nameplate could never be removed again and outlived its party,
        // rendering the dead party's name ("NameFailed - BanditPartyPatch" for bandits).
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager != null)
        {
            obj.Id = new MBGUID(14u, campaignObjectManager.GetNextUniqueObjectIdOfType<MobileParty>());
        }

        GameThread.Run(() =>
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;
            if (objectManager == null) return;

            // AddMobileParty unconditionally re-assigns Id (nothing inside it reads the Id); restore
            // the pre-assigned one so the identity every Id-hashed dictionary keyed on stays stable.
            var preassignedId = obj.Id;
            objectManager.AddMobileParty(obj);
            if (preassignedId.InternalValue != 0)
            {
                obj.Id = preassignedId;
            }
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
        });
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
