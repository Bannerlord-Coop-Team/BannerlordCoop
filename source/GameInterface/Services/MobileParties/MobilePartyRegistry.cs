using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
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

    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMessageBroker messageBroker;

    public MobilePartyRegistry(
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider,
        IMessageBroker messageBroker,
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.messageBroker = messageBroker;
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MobileParty));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    //    new MethodBase[]
    //{
    //    AccessTools.Method(typeof(MobileParty), nameof(MobileParty.RemoveParty))
    //};


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
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        GameLoopRunner.RunOnMainThread(() =>
        {
            Campaign.Current?.CampaignObjectManager?.AddMobileParty(obj);
        });
    }

    public override void OnClientDestroyed(MobileParty obj, string id)
    {
        GameLoopRunner.RunOnMainThread(() =>
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
        controlledEntityRegistry.RegisterAsControlled(controllerIdProvider.ControllerId, id);
    }

    public override void OnServerDestroyed(MobileParty obj, string id)
    {
        obj.MemberRoster.Clear();
        obj.PrisonRoster.Clear();
        obj.Party.SetVisualAsDirty();

        var message = new InstanceDestroyed<PartyBase>(obj.Party);
        messageBroker.Publish(this, message);

        
    }
}
