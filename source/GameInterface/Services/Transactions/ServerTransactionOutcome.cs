using LiteNetLib;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Transactions;

/// <summary>
/// Server-only completion signal for authoritative campaign transactions.
/// Action handlers publish only after their game-thread work either commits
/// or is rejected. The additive type does not alter Coop's wire protocol or
/// any client assembly contract.
/// </summary>
public static class ServerTransactionOutcome
{
    private static readonly object CraftGate = new();
    private static readonly ConditionalWeakTable<NetPeer, CraftXpPermit>
        CraftXpPermits = new();
    private static readonly ConditionalWeakTable<NetPeer, CraftRenamePermit>
        CraftRenamePermits = new();
    [ThreadStatic]
    private static ExecutionFrame CurrentExecution;
    public const int Trade = 1;
    public const int Party = 2;
    public const int Smelt = 3;
    public const int Refine = 4;
    public const int Craft = 5;
    public const int CraftXp = 6;
    public const int Recruit = 7;
    public const int ClanParty = 8;

    public static event Action<NetPeer, int, bool, string> Completed;

    public static void Accept(NetPeer peer, int kind)
    {
        MarkExecutionCompleted(peer, kind);
        InvokeCompleted(peer, kind, true, string.Empty);
    }

    public static void Reject(NetPeer peer, int kind, string reason)
    {
        MarkExecutionCompleted(peer, kind);
        InvokeCompleted(
            peer,
            kind,
            false,
            reason ?? "The server rejected this action.");
    }

    private static void InvokeCompleted(
        NetPeer peer, int kind, bool success, string reason)
    {
        Delegate[] handlers = Completed?.GetInvocationList();
        if (handlers == null)
            return;
        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action<NetPeer, int, bool, string>)handler)(
                    peer, kind, success, reason);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "[Coop] Transaction completion listener failed: " +
                    exception.GetBaseException().Message);
            }
        }
    }

    public static void Execute(NetPeer peer, int kind, Action action)
    {
        ExecutionFrame previous = CurrentExecution;
        var frame = new ExecutionFrame(peer, kind);
        CurrentExecution = frame;
        try
        {
            action?.Invoke();
            if (!frame.Completed)
                Reject(
                    peer,
                    kind,
                    "The authoritative handler did not report a result.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "[Coop] Authoritative transaction failed: " +
                exception.GetBaseException().Message);
            Reject(
                peer,
                kind,
                "The server could not safely complete this action.");
        }
        finally
        {
            CurrentExecution = previous;
        }
    }

    private static void MarkExecutionCompleted(NetPeer peer, int kind)
    {
        ExecutionFrame frame = CurrentExecution;
        if (frame != null && ReferenceEquals(frame.Peer, peer) &&
            frame.Kind == kind)
            frame.Completed = true;
    }

    public static bool TryResolvePlayer(
        NetPeer peer,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        string requestedHeroId,
        string requestedPartyId,
        out Player player,
        out Hero hero,
        out MobileParty party,
        out string reason)
    {
        player = null;
        hero = null;
        party = null;
        reason = "The server could not authenticate this player.";
        if (peer == null || playerManager == null || objectManager == null ||
            !playerManager.TryGetPlayer(peer, out player) || player == null ||
            !string.Equals(player.HeroId, requestedHeroId,
                StringComparison.Ordinal) ||
            !string.Equals(player.MobilePartyId, requestedPartyId,
                StringComparison.Ordinal) ||
            !objectManager.TryGetObject(player.HeroId, out hero) ||
            !objectManager.TryGetObject(player.MobilePartyId, out party) ||
            hero == null || party == null || hero.PartyBelongedTo != party)
            return false;

        return true;
    }

    public static bool TryResolveOwnedCraftingHero(
        NetPeer peer,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        string craftingHeroId,
        out Player player,
        out Hero craftingHero,
        out MobileParty playerParty,
        out string reason)
    {
        craftingHero = null;
        player = null;
        playerParty = null;
        reason = "The server could not authenticate this player.";
        if (peer == null || playerManager == null || objectManager == null ||
            !playerManager.TryGetPlayer(peer, out player) || player == null ||
            !TryResolvePlayer(
                peer,
                playerManager,
                objectManager,
                player.HeroId,
                player.MobilePartyId,
                out player,
                out Hero playerHero,
                out playerParty,
                out reason) ||
            !objectManager.TryGetObject(craftingHeroId, out craftingHero) ||
            craftingHero == null ||
            craftingHero != playerHero &&
            (craftingHero.Clan != playerHero.Clan ||
             craftingHero.PartyBelongedTo != playerParty))
        {
            reason = "Smithing hero did not belong to the connected player.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static void AllowCraftXp(NetPeer peer, string heroId)
    {
        if (peer == null || string.IsNullOrEmpty(heroId)) return;
        lock (CraftGate)
        {
            CraftXpPermits.Remove(peer);
            CraftXpPermits.Add(peer, new CraftXpPermit(
                heroId, DateTime.UtcNow.AddSeconds(30)));
        }
    }

    public static bool TryConsumeCraftXp(NetPeer peer, string heroId)
    {
        if (peer == null || string.IsNullOrEmpty(heroId)) return false;
        lock (CraftGate)
        {
            if (!CraftXpPermits.TryGetValue(peer, out CraftXpPermit permit))
                return false;
            CraftXpPermits.Remove(peer);
            return permit.ExpiresUtc >= DateTime.UtcNow &&
                string.Equals(permit.HeroId, heroId, StringComparison.Ordinal);
        }
    }

    public static void AllowCraftRename(NetPeer peer, string craftedItemId)
    {
        if (peer == null || string.IsNullOrEmpty(craftedItemId)) return;
        lock (CraftGate)
        {
            CraftRenamePermits.Remove(peer);
            CraftRenamePermits.Add(peer, new CraftRenamePermit(
                craftedItemId, DateTime.UtcNow.AddMinutes(2)));
        }
    }

    public static bool TryConsumeCraftRename(
        NetPeer peer, string craftedItemId)
    {
        if (peer == null || string.IsNullOrEmpty(craftedItemId)) return false;
        lock (CraftGate)
        {
            if (!CraftRenamePermits.TryGetValue(
                    peer, out CraftRenamePermit permit))
                return false;
            CraftRenamePermits.Remove(peer);
            return permit.ExpiresUtc >= DateTime.UtcNow &&
                string.Equals(permit.CraftedItemId, craftedItemId,
                    StringComparison.Ordinal);
        }
    }

    private sealed class CraftXpPermit
    {
        internal readonly string HeroId;
        internal readonly DateTime ExpiresUtc;

        internal CraftXpPermit(string heroId, DateTime expiresUtc)
        {
            HeroId = heroId;
            ExpiresUtc = expiresUtc;
        }
    }

    private sealed class ExecutionFrame
    {
        internal readonly NetPeer Peer;
        internal readonly int Kind;
        internal bool Completed;

        internal ExecutionFrame(NetPeer peer, int kind)
        {
            Peer = peer;
            Kind = kind;
        }
    }

    private sealed class CraftRenamePermit
    {
        internal readonly string CraftedItemId;
        internal readonly DateTime ExpiresUtc;

        internal CraftRenamePermit(
            string craftedItemId, DateTime expiresUtc)
        {
            CraftedItemId = craftedItemId;
            ExpiresUtc = expiresUtc;
        }
    }
}
