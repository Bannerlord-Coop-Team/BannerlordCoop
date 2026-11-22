using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships;

internal class ShipRegistry : AutoRegistryBase<Ship>
{
    public ShipRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Ship));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in Campaign.Current.CampaignObjectManager.MobileParties)
        {
            if (party.Ships == null) continue;

            var index = 0;
            foreach (var ship in party.Ships)
            {
                RegisterExistingObject($"MobileParty_{party.StringId}_{index++}", ship);
            }
        }

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            if (settlement.Party == null || settlement.Party.Ships == null) continue;

            var index = 0;
            foreach (var ship in settlement.Party.Ships)
            {
                RegisterExistingObject($"Settlement_{settlement.StringId}_{index++}", ship);
            }
        }

        // Moored ships have no owner, so they're only reachable through their port's stash
        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            if (settlement.ShipStash == null) continue;

            var index = 0;
            foreach (var ship in settlement.ShipStash)
            {
                RegisterExistingObject($"ShipStash_{settlement.StringId}_{index++}", ship);
            }
        }
        
        // An empty clan party keeps its ships without an owner until its leader returns
        var emptyClanParties = Campaign.Current.GetCampaignBehavior<EmptyClanPartiesCampaignBehavior>()?._emptyClanParties;
        if (emptyClanParties == null) return;

        foreach (var emptyClanParty in emptyClanParties)
        {
            var index = 0;
            foreach (var ship in emptyClanParty.Ships)
            {
                RegisterExistingObject($"EmptyClanParty_{emptyClanParty.Leader.StringId}_{index++}", ship);
            }
        }
    }

    public override void OnClientCreated(Ship obj, string id)
    {
        // Initialise the dictionary by creating it and dirtying the version number
        ReflectionUtils.SetPrivateField(typeof(Ship), nameof(Ship._shipPieces), obj, new Dictionary<string, ShipUpgradePiece>());
        obj.UpdateVersionNo();

        using (new AllowedThread())
        {
            obj.IsTradeable = true;
        }
    }

    public override void OnClientDestroyed(Ship obj, string id)
    {
        // DestroyShipAction clears the owner, but its patched copy is compiled before the Owner sync patch is
        // applied, and v1.5's smaller Owner setter can be inlined into it, so that owner change never reaches
        // the clients. Clearing it here also takes the ship off the owner's ship list.
        using (new AllowedThread())
        {
            obj.Owner = null;
        }
    }

    public override void OnServerCreated(Ship obj, string id)
    {
    }

    public override void OnServerDestroyed(Ship obj, string id)
    {
    }
}
