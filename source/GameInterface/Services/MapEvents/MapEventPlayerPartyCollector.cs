using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

internal static class MapEventPlayerPartyCollector
{
    public static string[] CollectPartyIds(MapEvent mapEvent, IObjectManager objectManager)
    {
        var ids = new HashSet<string>();
        if (mapEvent == null)
            return new string[0];

        AddParties(mapEvent.InvolvedParties, objectManager, ids);
        AddSideParties(mapEvent.AttackerSide, objectManager, ids);
        AddSideParties(mapEvent.DefenderSide, objectManager, ids);
        return ids.ToArray();
    }

    public static string[] Combine(params string[][] partyIdGroups)
    {
        var ids = new HashSet<string>();

        foreach (var partyIds in partyIdGroups ?? System.Array.Empty<string[]>())
        {
            if (partyIds == null)
                continue;

            foreach (var partyId in partyIds)
            {
                if (string.IsNullOrEmpty(partyId))
                    continue;

                ids.Add(partyId);
            }
        }

        return ids.ToArray();
    }

    private static void AddSideParties(MapEventSide side, IObjectManager objectManager, HashSet<string> ids)
    {
        if (side?.Parties == null)
            return;

        foreach (var mapEventParty in side.Parties)
            AddParty(mapEventParty?.Party, objectManager, ids);
    }

    private static void AddParties(IEnumerable<PartyBase> parties, IObjectManager objectManager, HashSet<string> ids)
    {
        if (parties == null)
            return;

        foreach (var party in parties)
            AddParty(party, objectManager, ids);
    }

    private static void AddParty(PartyBase party, IObjectManager objectManager, HashSet<string> ids)
    {
        if (!IsPlayerParty(party))
            return;

        if (objectManager.TryGetId(party, out var partyId))
            ids.Add(partyId);
    }

    private static bool IsPlayerParty(PartyBase party)
    {
        if (party?.MobileParty == null)
            return false;

        return party.MobileParty.IsPlayerParty() ||
               party == PartyBase.MainParty ||
               party.MobileParty == MobileParty.MainParty;
    }
}
