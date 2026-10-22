using Common.Logging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.SiegeEvents;

/// <summary>
/// The troop sacrifice that breaking in and breaking out both pay on the server, kept in one place so a fix reaches both.
/// </summary>
internal static class SiegeSacrifice
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SiegeSacrifice));

    internal sealed class Paid
    {
        public uint[] CharacterIds;
        public int[] Counts;
        public int ArmyCasualties;
    }

    /// <summary>
    /// Takes <paramref name="losses"/> regulars from the party (and the army it leads) with patches live so the roster sync
    /// carries them. <paramref name="markPaid"/> runs right before the first troop is taken, so a failure part way through
    /// is never retried as a second sacrifice. Returns null, taking nothing, when the troops cannot be paid.
    /// </summary>
    public static Paid TryApply(IObjectManager objectManager, ISiegeBreakOut breakOut, INetwork network,
        ISendCoalescer coalescer, MobileParty party, int losses, Action markPaid)
    {
        var parties = party.Army?.LeaderParty == party ? party.Army.Parties.ToArray() : new[] { party };

        // Same as vanilla break_in_menu_accept_on_condition: refuse instead of charging fewer troops than required.
        int available = parties.Sum(member => member.MemberRoster.TotalRegulars);
        if (losses > available)
        {
            Logger.Warning("Party {PartyId} cannot pay {Losses} troops with {Available} regulars", party.StringId, losses, available);
            return null;
        }

        foreach (var member in parties)
        {
            if (!objectManager.TryGetHandleWithLogging(member.MemberRoster, out _)) return null;
            foreach (var element in member.MemberRoster.GetTroopRoster())
                if (element.Character.IsRegular && !objectManager.TryGetHandleWithLogging(element.Character, out _))
                    return null;
        }

        markPaid();
        var casualties = breakOut.ApplySacrifice(party, losses, out var armyCasualties);
        var elements = casualties.GetTroopRoster();
        var paid = new Paid { CharacterIds = new uint[elements.Count], Counts = new int[elements.Count], ArmyCasualties = armyCasualties };
        for (int index = 0; index < elements.Count; index++)
        {
            objectManager.TryGetHandleWithLogging(elements[index].Character, out paid.CharacterIds[index]);
            paid.Counts[index] = elements[index].Number;
        }
        foreach (var member in parties)
            if (objectManager.TryGetHandle(member.MemberRoster, out var rosterId))
                coalescer?.FlushInstance(rosterId, network);
        return paid;
    }
}
