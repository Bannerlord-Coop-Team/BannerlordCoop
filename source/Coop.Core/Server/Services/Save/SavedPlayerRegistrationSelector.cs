using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Save;

/// <summary>
/// Picks the rows of a loaded co-op session to restore as player registrations.
/// </summary>
internal interface ISavedPlayerRegistrationSelector
{
    /// <summary>
    /// Returns one row per controller, and none for a hero or party that two controllers name. Also returns every hero
    /// the saved rows name, which <see cref="IPlayerPartyRestorer.TryGetOwnedParty"/> needs after the restore.
    /// </summary>
    (IReadOnlyList<Player> Rows, IReadOnlyCollection<Hero> PlayerHeroes) Select(IEnumerable<Player> savedPlayers);
}

/// <inheritdoc cref="ISavedPlayerRegistrationSelector"/>
internal class SavedPlayerRegistrationSelector : ISavedPlayerRegistrationSelector
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedPlayerRegistrationSelector>();

    private readonly IObjectManager objectManager;
    private readonly IPlayerPartyRestorer playerPartyRestorer;

    public SavedPlayerRegistrationSelector(IObjectManager objectManager, IPlayerPartyRestorer playerPartyRestorer)
    {
        this.objectManager = objectManager;
        this.playerPartyRestorer = playerPartyRestorer;
    }

    public (IReadOnlyList<Player> Rows, IReadOnlyCollection<Hero> PlayerHeroes) Select(IEnumerable<Player> savedPlayers)
    {
        var rows = savedPlayers.Where(player => player != null).ToArray();

        // A row can name a hero or party by its full or its short id, so claims compare the objects they resolve to
        var heroClaims = new List<(Player Player, Hero Claimed)>();
        foreach (var player in rows)
        {
            if (objectManager.TryGetObject(player.HeroId, out Hero hero))
                heroClaims.Add((player, hero));
        }

        var playerHeroes = heroClaims.Select(claim => claim.Claimed).ToArray();

        var partyClaims = new List<(Player Player, MobileParty Claimed)>();
        foreach (var player in rows)
        {
            if (playerPartyRestorer.TryGetOwnedParty(player, playerHeroes, out var party))
                partyClaims.Add((player, party));
        }

        // A pre-#2400 save can give one hero or party to two controllers, so restore neither; remove once those saves
        // are unsupported. Checked over every row first, so the row order can't decide which controller keeps it.
        var shared = new HashSet<Player>();
        foreach (var player in SharedClaims(heroClaims).Concat(SharedClaims(partyClaims)))
        {
            if (shared.Add(player))
                Logger.Error(
                    "Skipped saved registration for hero {HeroId}: another saved controller names the same hero or party. " +
                    "The next save writes the session file without it, so copy the session .json now and fix the copy",
                    player.HeroId);
        }

        var selected = SelectOneRowPerController(
            rows.Where(player => !shared.Contains(player)),
            new HashSet<Player>(heroClaims.Select(claim => claim.Player)),
            new HashSet<Player>(partyClaims.Select(claim => claim.Player)));

        return (selected.ToArray(), playerHeroes);
    }

    private static IEnumerable<Player> SharedClaims<T>(IEnumerable<(Player Player, T Claimed)> claims) =>
        claims.GroupBy(claim => claim.Claimed)
            .Where(group => group.Select(claim => claim.Player.ControllerId).Distinct().Count() > 1)
            .SelectMany(group => group.Select(claim => claim.Player));

    /// <summary>
    /// A save written before controller ids were unique can carry several rows for one controller, in either order.
    /// The campaign decides which is real: a row whose party it still gives to that row, then one whose hero and party
    /// both exist, then one whose hero exists.
    /// </summary>
    private IEnumerable<Player> SelectOneRowPerController(
        IEnumerable<Player> rows,
        ISet<Player> withHero,
        ISet<Player> withOwnedParty)
    {
        foreach (var group in rows.GroupBy(player => player.ControllerId))
        {
            var registrations = group.ToArray();
            if (registrations.Length == 1)
            {
                yield return registrations[0];
                continue;
            }

            var kept =
                registrations.FirstOrDefault(withOwnedParty.Contains) ??
                registrations.FirstOrDefault(registration =>
                    withHero.Contains(registration) && objectManager.TryGetObject(registration.MobilePartyId, out MobileParty _)) ??
                registrations.FirstOrDefault(withHero.Contains) ??
                registrations[0];

            Logger.Warning(
                "Save carries {Count} registrations for controller {ControllerId} (heroes {HeroIds}); " +
                "restoring {KeptHeroId} and dropping the rest",
                registrations.Length,
                group.Key,
                string.Join(", ", registrations.Select(registration => registration.HeroId)),
                kept.HeroId);

            yield return kept;
        }
    }
}
