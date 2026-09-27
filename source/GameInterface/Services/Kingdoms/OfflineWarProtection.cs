using Common;
using GameInterface.Configuration;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms;

/// <summary>
/// Decides whether the server refuses an AI war proposal against a player faction while none of
/// its players are online.
/// </summary>
public interface IOfflineWarProtection
{
    /// <summary>
    /// True when <see cref="ModOptions.BlockAiWarDeclarationsOnOfflinePlayers"/> is on and a
    /// non-player clan proposes a war or a call to war against a player-ruled kingdom or an
    /// independent player clan with no connected player.
    /// </summary>
    /// <param name="decision">The decision about to be added.</param>
    /// <param name="target">The protected faction when refused, otherwise null.</param>
    bool ShouldRefuse(KingdomDecision decision, out IFaction target);
}

/// <inheritdoc cref="IOfflineWarProtection"/>
public class OfflineWarProtection : IOfflineWarProtection
{
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public OfflineWarProtection(IPlayerManager playerManager, IObjectManager objectManager)
    {
        if (playerManager == null) throw new ArgumentNullException(nameof(playerManager));
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));

        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public bool ShouldRefuse(KingdomDecision decision, out IFaction target)
    {
        target = null;

        if (!ModInformation.IsServer || !ModConfigProvider.ModOptions.BlockAiWarDeclarationsOnOfflinePlayers) return false;

        Clan proposerClan = decision?.ProposerClan;
        if (proposerClan == null || playerManager.Contains(proposerClan)) return false;

        IFaction warTarget = GetWarTarget(decision);
        if (!IsPlayerRuled(warTarget) || HasConnectedPlayer(warTarget)) return false;

        target = warTarget;
        return true;
    }

    private static IFaction GetWarTarget(KingdomDecision decision) => decision switch
    {
        DeclareWarDecision declareWar => declareWar.FactionToDeclareWarOn,
        ProposeCallToWarAgreementDecision proposeCallToWar => proposeCallToWar.KingdomToCallToWarAgainst,
        AcceptCallToWarAgreementDecision acceptCallToWar => acceptCallToWar.KingdomToCallToWarAgainst,
        _ => null,
    };

    // Only the ruling clan counts, so an offline player serving in an AI kingdom is not protected.
    private bool IsPlayerRuled(IFaction faction)
    {
        if (faction == null || faction.IsEliminated) return false;

        Clan rulingClan = faction switch
        {
            Kingdom kingdom => kingdom.RulingClan,
            Clan clan when clan.Kingdom == null => clan,
            _ => null,
        };

        return rulingClan != null && playerManager.Contains(rulingClan);
    }

    // Any connected player of the faction can respond, including a vassal or a coop clan member.
    private bool HasConnectedPlayer(IFaction faction)
    {
        foreach (Player player in playerManager.Players)
        {
            if (!playerManager.IsConnected(player)) continue;

            Clan clan = GetCurrentClan(player);
            if (clan != null && (clan == faction || clan.Kingdom == faction)) return true;
        }

        return false;
    }

    // Player.ClanId can lag a coop clan join until the next reconnect or save, so the hero's clan comes first.
    private Clan GetCurrentClan(Player player)
    {
        if (objectManager.TryGetObject(player.HeroId, out Hero hero) && hero.Clan != null) return hero.Clan;

        return objectManager.TryGetObject(player.ClanId, out Clan clan) ? clan : null;
    }
}
