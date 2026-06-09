using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Handles defeat of a connected player's party the way vanilla handles the host's <c>MainParty</c>.
/// </summary>
/// <remarks>
/// <c>MapEvent.CaptureDefeatedPartyMembers</c> only spares <c>Hero.MainHero</c> / <c>PartyBase.MainParty</c>.
/// A remote player's defeated party therefore takes the generic-lord path (<c>RemovePartyLeader</c>, the hero
/// made prisoner or fugitive, the roster emptied) and the now-empty, non-main party is destroyed — vanishing
/// on server and client. Since the deciding round runs the whole results pass on the server mid-simulation,
/// this fires there.
///
/// We pre-empt that: for each defeated player party we take its hero prisoner through the normal
/// <see cref="TakePrisonerAction"/> (which the mod already syncs via <c>PrisonerTaken</c> and uses to
/// deactivate — not destroy — the party), then remove those parties from the list vanilla processes so it
/// never runs its destroy path on them.
/// </remarks>
[HarmonyPatch(typeof(MapEvent), "CaptureDefeatedPartyMembers")]
internal class PlayerDefeatCapturePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerDefeatCapturePatch>();

    [HarmonyPrefix]
    private static void Prefix(MBReadOnlyList<MapEventParty> winnerParties, ref MBReadOnlyList<MapEventParty> defeatedParties)
    {
        // Defeat resolution is authoritative on the server.
        if (!ModInformation.IsServer)
            return;

        var playerParties = defeatedParties
            .Where(p => p.Party.MobileParty?.IsPlayer() == true)
            .ToList();

        if (playerParties.Count == 0)
            return;

        var captor = SelectCaptor(winnerParties);

        foreach (var playerParty in playerParties)
        {
            var hero = playerParty.Party.LeaderHero;
            if (hero == null)
                continue;

            if (captor != null && hero.CanBecomePrisoner())
            {
                // Routes through TakePrisonerActionPatches -> PrisonerTaken -> the party is deactivated
                // (kept, recoverable through the captivity-end flow), not destroyed.
                TakePrisonerAction.Apply(captor, hero);
            }
            else
            {
                Logger.Debug("Defeated player hero {Hero} could not be taken prisoner; leaving party intact", hero.Name);
            }
        }

        // Keep player parties out of vanilla's RemovePartyLeader / fugitive / destroy path.
        defeatedParties = new MBList<MapEventParty>(
            defeatedParties.Where(p => p.Party.MobileParty?.IsPlayer() != true));
    }

    /// <summary>Mirror of the winner selection vanilla uses when imprisoning the main hero.</summary>
    private static PartyBase SelectCaptor(MBReadOnlyList<MapEventParty> winnerParties)
    {
        var candidate = winnerParties
            .Where(x => x.Party.MemberRoster.TotalManCount > 0)
            .OrderByDescending(x => x.ContributionToBattle)
            .FirstOrDefault();

        if (candidate == null)
            return null;

        var party = candidate.Party;
        if (party.IsMobile && (party.MobileParty.IsMilitia || party.MobileParty.IsGarrison))
            party = party.MobileParty.HomeSettlement.Party;

        return party;
    }
}
