using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party;

/// <summary>
/// A party screen commit the server is replaying: both sides of the screen and what the commands moved so far.
/// </summary>
internal sealed class PartyScreenCommit
{
    public readonly NetworkCommitPartyScreen Message;
    public readonly NetPeer Requester;
    public readonly Hero MainHero;
    public readonly MobileParty Party;
    public readonly Settlement Settlement;
    // The donate and prisoner screens lost their settlement, so their transfers are rejected.
    public readonly bool SettlementGone;
    public readonly bool IsForceTransfer;
    public readonly PartyScreenRosterSide RightMembers;
    public readonly PartyScreenRosterSide RightPrisoners;
    public readonly PartyScreenRosterSide LeftMembers;
    public readonly PartyScreenRosterSide LeftPrisoners;
    public readonly Dictionary<CharacterObject, int> NetMembersToLeft = new();
    public readonly Dictionary<CharacterObject, int> NetPrisonersToLeft = new();
    public readonly List<string> Rejections = new();
    public int GoldChange;
    public int RecruitedCount;

    public PartyScreenCommit(
        NetworkCommitPartyScreen message,
        NetPeer requester,
        Hero mainHero,
        Settlement settlement,
        bool settlementGone,
        bool isForceTransfer,
        PartyScreenRosterSide leftMembers,
        PartyScreenRosterSide leftPrisoners)
    {
        Message = message;
        Requester = requester;
        MainHero = mainHero;
        Party = mainHero.PartyBelongedTo;
        Settlement = settlement;
        SettlementGone = settlementGone;
        IsForceTransfer = isForceTransfer;
        RightMembers = PartyScreenRosterSide.Real(Party.MemberRoster);
        RightPrisoners = PartyScreenRosterSide.Real(Party.PrisonRoster);
        LeftMembers = leftMembers;
        LeftPrisoners = leftPrisoners;
    }

    /// <summary>
    /// A prisoner hero moved to or from a client-only left side changes hands in the done effects, through
    /// vanilla's release and capture, which move the hero themselves. Until then they stay in the right roster.
    /// </summary>
    public bool IsDeferred(CharacterObject character)
    {
        var hero = character.HeroObject;
        return hero != null && LeftPrisoners.IsVirtual &&
            (Message.DoneKind == PartyScreenDoneKind.Default || hero.IsPlayerHero());
    }

    /// <summary>The hero on the left side of a create clan party screen, who leads the new party.</summary>
    public Hero ClanPartyLeader
        => LeftMembers.Entries.Select(entry => entry.Key).LastOrDefault(character => character.IsHero)?.HeroObject;
}

/// <summary>
/// One side of the screen: a real roster the server edits, or a roster only the client has, kept as counts.
/// </summary>
internal sealed class PartyScreenRosterSide
{
    private readonly Dictionary<CharacterObject, (int number, int wounded, int xp)> pool;

    private PartyScreenRosterSide(TroopRoster roster, Dictionary<CharacterObject, (int number, int wounded, int xp)> pool)
    {
        Roster = roster;
        this.pool = pool;
    }

    public TroopRoster Roster { get; }
    public bool IsVirtual => Roster == null;
    public IEnumerable<KeyValuePair<CharacterObject, (int number, int wounded, int xp)>> Entries
        => pool ?? Enumerable.Empty<KeyValuePair<CharacterObject, (int number, int wounded, int xp)>>();

    public static PartyScreenRosterSide Real(TroopRoster roster) => new(roster, null);

    public static PartyScreenRosterSide Virtual(IEnumerable<TroopRosterElement> seed)
    {
        var side = new PartyScreenRosterSide(null, new Dictionary<CharacterObject, (int number, int wounded, int xp)>());
        foreach (var element in seed)
            side.Add(element.Character, element.Number, element.WoundedNumber, element.Xp);
        return side;
    }

    public (int number, int wounded, int xp) Read(CharacterObject character)
    {
        if (IsVirtual)
        {
            pool.TryGetValue(character, out var pooled);
            return pooled;
        }

        int index = Roster.FindIndexOfTroop(character);
        if (index < 0) return default;
        var element = Roster.GetElementCopyAtIndex(index);
        return (element.Number, element.WoundedNumber, element.Xp);
    }

    public void Add(CharacterObject character, int number, int wounded, int xp)
    {
        if (IsVirtual)
        {
            pool.TryGetValue(character, out var pooled);
            pool[character] = (pooled.number + number, pooled.wounded + wounded, pooled.xp + xp);
            return;
        }

        Roster.AddToCounts(character, number, woundedCount: wounded, xpChange: xp);
    }
}
