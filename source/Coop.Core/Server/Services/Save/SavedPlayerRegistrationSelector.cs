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
    /// Returns one row per controller, and none for a hero or party that two controllers name. Every hero the saved
    /// rows name goes to <paramref name="playerHeroes"/>, the player heroes
    /// <see cref="IPlayerPartyRestorer.TryGetOwnedParty"/> takes.
    /// </summary>
    IReadOnlyList<Player> Select(IEnumerable<Player> savedPlayers, out IReadOnlyCollection<Hero> playerHeroes);
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

    public IReadOnlyList<Player> Select(IEnumerable<Player> savedPlayers, out IReadOnlyCollection<Hero> playerHeroes)
    {
        // A row can name a hero or party by its full or its short id, so claims compare the objects they resolve to
        var claims = ResolveClaims(savedPlayers.Where(player => player != null), out playerHeroes);
        var heroClaims = claims.Where(claim => claim.Hero != null).Select(claim => (claim.Player, Claimed: claim.Hero));
        var partyClaims = claims.Where(claim => claim.KeepsParty).Select(claim => (claim.Player, Claimed: claim.Party));

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

        return SelectOneRowPerController(claims.Where(claim => !shared.Contains(claim.Player))).ToArray();
    }

    private SavedRowClaim[] ResolveClaims(IEnumerable<Player> rows, out IReadOnlyCollection<Hero> playerHeroes)
    {
        var resolved = new List<(Player Player, Hero Hero, MobileParty Party)>();
        foreach (var player in rows)
        {
            var heroExists = objectManager.TryGetObject(player.HeroId, out Hero hero);
            var partyExists = objectManager.TryGetObject(player.MobilePartyId, out MobileParty party);
            resolved.Add((player, heroExists ? hero : null, partyExists ? party : null));
        }

        var heroes = resolved.Where(row => row.Hero != null).Select(row => row.Hero).ToArray();
        playerHeroes = heroes;

        return resolved
            .Select(row => new SavedRowClaim(
                row.Player,
                row.Hero,
                row.Party,
                row.Hero != null && row.Party != null && playerPartyRestorer.KeepsParty(row.Hero, row.Party, heroes)))
            .ToArray();
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
    private static IEnumerable<Player> SelectOneRowPerController(IEnumerable<SavedRowClaim> claims)
    {
        foreach (var group in claims.GroupBy(claim => claim.Player.ControllerId))
        {
            var registrations = group.ToArray();
            if (registrations.Length == 1)
            {
                yield return registrations[0].Player;
                continue;
            }

            // A claim is a struct, so a rank with no match gives a default claim whose Player is null
            var kept =
                registrations.FirstOrDefault(claim => claim.KeepsParty).Player ??
                registrations.FirstOrDefault(claim => claim.Hero != null && claim.Party != null).Player ??
                registrations.FirstOrDefault(claim => claim.Hero != null).Player ??
                registrations[0].Player;

            Logger.Warning(
                "Save carries {Count} registrations for controller {ControllerId} (heroes {HeroIds}); " +
                "restoring {KeptHeroId} and dropping the rest",
                registrations.Length,
                group.Key,
                string.Join(", ", registrations.Select(claim => claim.Player.HeroId)),
                kept.HeroId);

            yield return kept;
        }
    }

    // One saved row with its hero and party resolved once (null when missing), and whether the campaign still gives
    // the row that party
    private readonly struct SavedRowClaim
    {
        public SavedRowClaim(Player player, Hero hero, MobileParty party, bool keepsParty)
        {
            Player = player;
            Hero = hero;
            Party = party;
            KeepsParty = keepsParty;
        }

        public Player Player { get; }
        public Hero Hero { get; }
        public MobileParty Party { get; }
        public bool KeepsParty { get; }
    }
}
