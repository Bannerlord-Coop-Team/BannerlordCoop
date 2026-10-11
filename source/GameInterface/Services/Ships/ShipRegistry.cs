using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships;

/// <summary>
/// Registry for campaign <see cref="Ship"/> objects. Save-loaded ships derive their id from the owning party and
/// their index in its ship list; ships created mid-session are reconciled on join through the id remap.
/// </summary>
internal class ShipRegistry : AutoRegistryBase<Ship>
{
    private static readonly FieldInfo ShipPiecesField = AccessTools.Field(typeof(Ship), nameof(Ship._shipPieces));

    private readonly ISendCoalescer coalescer;

    public ShipRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        ISendCoalescer coalescer = null)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.coalescer = coalescer;
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Ship));

    // DestroyShipAction is static, so ShipLifetimePatches publishes the destroy.
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var party in MobileParty.All)
        {
            RegisterPartyShips(party.StringId, party.Party);
        }

        foreach (var settlement in Settlement.All)
        {
            RegisterPartyShips(settlement.StringId, settlement.Party);
        }
    }

    private void RegisterPartyShips(string ownerId, PartyBase owner)
    {
        var ships = owner?.Ships;
        if (ships == null) return;

        for (int i = 0; i < ships.Count; i++)
        {
            RegisterExistingObject($"{ownerId}_{i}", ships[i]);
        }
    }

    public override void OnClientCreated(Ship obj, string id)
    {
        // The skipped constructor leaves the field initializers unset; _shipPieces is readonly, hence reflection.
        ShipPiecesField.SetValue(obj, new Dictionary<string, ShipUpgradePiece>());
        obj._isVersionDirty = true;

        using (new AllowedThread())
        {
            obj.IsTradeable = true;
            obj.CustomSailPatternId = "";
        }
    }

    public override void OnClientDestroyed(Ship obj, string id)
    {
    }

    public override void OnServerCreated(Ship obj, string id)
    {
    }

    public override void OnServerDestroyed(Ship obj, string id)
    {
        // Pending hit point updates must not follow the destroy.
        coalescer?.DropInstance(id);
        if (objectManager.TryGetHandle(obj, out var handle))
            coalescer?.DropInstance(handle);
    }
}
