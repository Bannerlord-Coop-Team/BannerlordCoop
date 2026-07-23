using Common;
using Common.Logging;
using Common.Util;
using SandBox.CampaignBehaviors;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Alleys.Interfaces;

/// <summary>
/// Access to the owning player's alley management state, which lives in the
/// <c>AlleyCampaignBehavior.PlayerAlleyData</c> entries of its <c>_playerOwnedCommonAreaData</c> list
/// (both reached directly through the publicizer). Populating this list is what lights up the in-game
/// manage-alley menus on the owning client; the host reads it to seed the authoritative session.
/// </summary>
public interface IAlleyCampaignBehaviorInterface : IGameAbstraction
{
    void AddOrUpdatePlayerAlleyData(Alley alley, Hero overseer, TroopRoster garrison);
    void RemovePlayerAlleyData(Alley alley);
    bool TryGetCurrentSettlementAlley(out Alley alley);

    /// <summary>
    /// Marks the owning client's mirror entry as under attack by <paramref name="attacker"/> with the
    /// given answer deadline, so the vanilla confront-alley menu/conversation/fight light up when the
    /// player visits. <paramref name="showNotification"/> adds the vanilla under-attack map notice (only
    /// for a fresh attack, not a late-join restore where the notice would be stale).
    /// </summary>
    void SetPlayerAlleyUnderAttackByAi(Alley alley, Alley attacker, CampaignTime dueDate, bool showNotification);

    /// <summary>Clears the owning client's mirror under-attack state once the attack is resolved.</summary>
    void ClearPlayerAlleyUnderAttackByAi(Alley alley);

    /// <summary>
    /// Enumerates the currently player-owned alleys so the host can add management data for them to the
    /// authoritative CoopSession when it has no entry yet (an owned alley the session never recorded).
    /// Ownership is read from the replicated/saved <c>Alley.Owner</c> (the host's own
    /// <c>_playerOwnedCommonAreaData</c> is empty in co-op, it has no main hero); gang-occupied alleys
    /// are excluded.
    /// </summary>
    IReadOnlyList<Alley> GetPlayerOwnedAlleys();
}

/// <inheritdoc cref="IAlleyCampaignBehaviorInterface"/>
public class AlleyCampaignBehaviorInterface : IAlleyCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyCampaignBehaviorInterface>();

    private static AlleyCampaignBehavior Behavior => Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();

    public void AddOrUpdatePlayerAlleyData(Alley alley, Hero overseer, TroopRoster garrison)
    {
        if (alley == null || garrison == null)
        {
            Logger.Error("Cannot add alley management data with a null alley/garrison");
            return;
        }

        GameThread.RunSafe(() =>
        {
            var behavior = Behavior;
            if (behavior == null) return;
            var list = behavior._playerOwnedCommonAreaData;
            if (list == null) return;

            using (new AllowedThread())
            {
                // The vanilla ctor derives the overseer from the first hero in the roster. Ensure an
                // explicit overseer is present; otherwise the roster must already contain one.
                if (overseer != null && !garrison.Contains(overseer.CharacterObject))
                {
                    garrison.AddToCounts(overseer.CharacterObject, 1, false, 0, 0, true, -1);
                }

                if (!RosterHasHero(garrison))
                {
                    Logger.Error("Cannot add alley management data: no overseer hero in the garrison roster");
                    return;
                }

                // Preserve an in-progress attack across the rebuild: the PlayerAlleyData ctor resets
                // UnderAttackBy, so a garrison/overseer update would otherwise drop the pending defense.
                Alley underAttackBy = null;
                CampaignTime dueDate = default;
                if (TryGetByAlley(list, alley, out var existing))
                {
                    underAttackBy = existing.UnderAttackBy;
                    dueDate = existing.AttackResponseDueDate;
                }

                RemoveByAlley(list, alley);
                var data = new AlleyCampaignBehavior.PlayerAlleyData(alley, garrison);
                if (overseer != null) data.AssignedClanMember = overseer;
                if (underAttackBy != null)
                {
                    data.UnderAttackBy = underAttackBy;
                    data.AttackResponseDueDate = dueDate;
                }
                list.Add(data);

                // Promote State in lockstep with the entry. Vanilla's AddPlayerAlleyCharacters does an
                // unguarded _playerOwnedCommonAreaData.First(x => x.Alley == alley), so an OccupiedByPlayer
                // alley with no entry throws; setting State only here means OccupiedByPlayer always has a
                // matching entry, and an alley with no entry stays at its loaded gang-occupied State.
                alley.State = Alley.AreaState.OccupiedByPlayer;
            }
        });
    }

    private static bool RosterHasHero(TroopRoster roster)
    {
        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character != null && element.Character.IsHero) return true;
        }
        return false;
    }

    public void RemovePlayerAlleyData(Alley alley)
    {
        if (alley == null) return;

        GameThread.RunSafe(() =>
        {
            var behavior = Behavior;
            if (behavior == null) return;
            var list = behavior._playerOwnedCommonAreaData;
            if (list == null) return;

            using (new AllowedThread())
            {
                RemoveByAlley(list, alley);
            }
        });
    }

    public void SetPlayerAlleyUnderAttackByAi(Alley alley, Alley attacker, CampaignTime dueDate, bool showNotification)
    {
        if (alley == null || attacker == null) return;

        GameThread.RunSafe(() =>
        {
            var list = Behavior?._playerOwnedCommonAreaData;
            if (list == null) return;

            using (new AllowedThread())
            {
                if (!TryGetByAlley(list, alley, out var data)) return;
                data.UnderAttackBy = attacker;
                data.AttackResponseDueDate = dueDate;

                if (showNotification) AddUnderAttackNotice(alley, dueDate);
            }
        });
    }

    public void ClearPlayerAlleyUnderAttackByAi(Alley alley)
    {
        if (alley == null) return;

        GameThread.RunSafe(() =>
        {
            var list = Behavior?._playerOwnedCommonAreaData;
            if (list == null) return;

            using (new AllowedThread())
            {
                if (TryGetByAlley(list, alley, out var data)) data.UnderAttackBy = null;
            }
        });
    }

    private static void AddUnderAttackNotice(Alley alley, CampaignTime dueDate)
    {
        var text = new TextObject("{=5bIpeW9X}Your alley in {SETTLEMENT} is under attack from neighboring gangs. Unless you go to their help, the alley will be lost in {RESPONSE_TIME} days.");
        text.SetTextVariable("SETTLEMENT", alley.Settlement.Name);
        text.SetTextVariable("RESPONSE_TIME", (float)dueDate.RemainingDaysFromNow, 2);
        Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new AlleyUnderAttackMapNotification(alley, text));
    }

    private static bool TryGetByAlley(List<AlleyCampaignBehavior.PlayerAlleyData> list, Alley alley, out AlleyCampaignBehavior.PlayerAlleyData data)
    {
        foreach (var d in list)
        {
            if (d.Alley == alley) { data = d; return true; }
        }
        data = null;
        return false;
    }

    public bool TryGetCurrentSettlementAlley(out Alley alley)
    {
        alley = null;

        var behavior = Behavior;
        if (behavior == null) return false;

        var settlement = Settlement.CurrentSettlement;
        if (settlement == null) return false;

        var list = behavior._playerOwnedCommonAreaData;
        if (list == null) return false;

        foreach (var data in list)
        {
            if (data.Alley != null && data.Alley.Settlement == settlement)
            {
                alley = data.Alley;
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<Alley> GetPlayerOwnedAlleys()
    {
        var owned = new List<Alley>();
        foreach (var settlement in Settlement.All)
        {
            var alleys = settlement.Alleys;
            if (alleys == null) continue;

            foreach (var alley in alleys)
            {
                // Alley.Owner is the replicated/saved source of truth. Skip the unowned (e.g. abandoned)
                // and the gang-occupied (owner is a gang leader, not a player); keep only player-owned.
                if (alley?.Owner == null || alley.Owner.IsGangLeader) continue;

                owned.Add(alley);
            }
        }
        return owned;
    }

    private static void RemoveByAlley(List<AlleyCampaignBehavior.PlayerAlleyData> list, Alley alley)
    {
        // Walk forward, only advancing when we keep an entry, so removing one doesn't skip the next.
        int i = 0;
        while (i < list.Count)
        {
            if (list[i].Alley == alley)
            {
                list.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }
}
