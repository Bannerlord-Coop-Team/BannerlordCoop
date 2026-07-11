using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.TroopRosters;
internal class TroopRosterRegistry : AutoRegistryBase<TroopRoster>
{
    public override bool Debug => true;
    public TroopRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // No constructor hooks: the TroopRoster(PartyBase) ctor is 14 bytes of IL and gets JIT-inlined
    // into its callers, so a Harmony lifetime prefix on it never runs. Party rosters are registered
    // via the (non-inlined) set_OwnerParty patch (TroopRosterOwnerPartyRegistrationPatch); dummy
    // rosters via TroopRosterCreateDummyPatch; load-time rosters via RegisterAllObjects below.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var parties = Campaign.Current?.MobileParties;
        if (parties == null)
        {
            Logger.Error("Unable to register {Type} when Campaign.MobileParties is null", nameof(TroopRoster));
            return;
        }

        foreach (MobileParty party in parties)
        {

            if (party == null) continue;

            if (party.MemberRoster is null)
            {
                Logger.Error("Unable to register {Roster} for party {PartyId}: roster is null", nameof(MobileParty.MemberRoster), party.StringId);
                continue;
            }

            RegisterExistingObject($"{nameof(MobileParty.MemberRoster)}_{party.StringId}", party.MemberRoster);

            if (party.PrisonRoster is null)
            {
                Logger.Error("Unable to register {Roster} for party {PartyId}: roster is null", nameof(MobileParty.PrisonRoster), party.StringId);
                continue;
            }

            RegisterExistingObject($"{nameof(MobileParty.PrisonRoster)}_{party.StringId}", party.PrisonRoster);
        }

        foreach (var settlement in Settlement.All)
        {
            if (settlement?.Party?.PrisonRoster == null) continue;

            RegisterExistingObject($"{nameof(Settlement.Party.PrisonRoster)}_{settlement.StringId}", settlement.Party.PrisonRoster);
        }

        RegisterMapEventCasualtyRosters(Campaign.Current?.MapEventManager?.MapEvents);
    }

    /// <summary>
    /// Registers the three casualty rosters owned by every active map-event party. These rosters are created
    /// during map-event initialization rather than through a reliable constructor hook, so late-joining clients
    /// must rediscover them from the saved map-event graph just like party member and prisoner rosters above.
    /// </summary>
    internal void RegisterMapEventCasualtyRosters(IEnumerable<MapEvent> mapEvents)
    {
        if (mapEvents == null)
        {
            Logger.Error("Unable to register map-event casualty rosters when Campaign.MapEventManager.MapEvents is null");
            return;
        }

        foreach (MapEvent mapEvent in mapEvents)
        {
            if (mapEvent?.StringId == null || mapEvent._sides == null) continue;

            // Match MapEventPartyRegistry's side/party traversal and one-based counter. Prefixing the owner key
            // with MapEventParty makes the roster id unambiguous while keeping it deterministic on both peers.
            int partyCounter = 1;
            foreach (MapEventSide side in mapEvent._sides)
            {
                if (side?.Parties == null) continue;

                foreach (MapEventParty party in side.Parties)
                {
                    if (party == null) continue;

                    string ownerId = $"{nameof(MapEventParty)}_{mapEvent.StringId}_{partyCounter++}";
                    RegisterMapEventCasualtyRoster(ownerId, nameof(MapEventParty.WoundedInBattle), party._woundedInBattle);
                    RegisterMapEventCasualtyRoster(ownerId, nameof(MapEventParty.DiedInBattle), party._diedInBattle);
                    RegisterMapEventCasualtyRoster(ownerId, nameof(MapEventParty.RoutedInBattle), party._routedInBattle);
                }
            }
        }
    }

    private void RegisterMapEventCasualtyRoster(string ownerId, string rosterName, TroopRoster roster)
    {
        if (roster == null)
        {
            Logger.Error("Unable to register {RosterName} for {OwnerId}: roster is null", rosterName, ownerId);
            return;
        }

        RegisterExistingObject($"{ownerId}_{rosterName}", roster);
    }

    public override void OnClientCreated(TroopRoster obj, string id)
    {
        obj.data = new TroopRosterElement[4];
        obj._count = 0;
        obj._troopRosterElements = new MBList<TroopRosterElement>();
        obj.InitializeCachedData();
    }

    public override void OnClientDestroyed(TroopRoster obj, string id)
    {
    }

    public override void OnServerCreated(TroopRoster obj, string id)
    {
    }

    public override void OnServerDestroyed(TroopRoster obj, string id)
    {
    }
}
