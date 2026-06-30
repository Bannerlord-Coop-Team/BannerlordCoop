using Common;
using Common.Logging;
using Common.Util;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Interfaces;

/// <summary>
/// Access to the owning player's alley management state, which lives in the (internal, nested)
/// <c>AlleyCampaignBehavior.PlayerAlleyData</c> entries of its private <c>_playerOwnedCommonAreaData</c>
/// list. Reached by reflection because that type is internal. Populating this list is what lights up the
/// in-game manage-alley menus on the owning client; the host reads it to seed the authoritative session.
/// </summary>
public interface IAlleyCampaignBehaviorInterface : IGameAbstraction
{
    void AddOrUpdatePlayerAlleyData(Alley alley, Hero overseer, TroopRoster garrison);
    void RemovePlayerAlleyData(Alley alley);
    bool TryGetCurrentSettlementAlley(out Alley alley);

    /// <summary>
    /// Reads the loaded behavior's player-owned alleys (alley, overseer, garrison) so the host can seed
    /// the authoritative CoopSession management data from a save that was never played in co-op.
    /// </summary>
    IEnumerable<(Alley alley, Hero overseer, TroopRoster garrison)> GetPlayerOwnedAlleys();
}

/// <inheritdoc cref="IAlleyCampaignBehaviorInterface"/>
public class AlleyCampaignBehaviorInterface : IAlleyCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyCampaignBehaviorInterface>();

    private static readonly Type PlayerAlleyDataType =
        typeof(AlleyCampaignBehavior).GetNestedType("PlayerAlleyData", BindingFlags.NonPublic);
    private static readonly FieldInfo PlayerOwnedDataField =
        AccessTools.Field(typeof(AlleyCampaignBehavior), "_playerOwnedCommonAreaData");
    private static readonly ConstructorInfo PlayerAlleyDataCtor = PlayerAlleyDataType != null
        ? AccessTools.Constructor(PlayerAlleyDataType, new[] { typeof(Alley), typeof(TroopRoster) })
        : null;
    private static readonly FieldInfo AlleyField =
        PlayerAlleyDataType != null ? AccessTools.Field(PlayerAlleyDataType, "Alley") : null;
    private static readonly FieldInfo AssignedClanMemberField =
        PlayerAlleyDataType != null ? AccessTools.Field(PlayerAlleyDataType, "AssignedClanMember") : null;
    private static readonly FieldInfo TroopRosterField =
        PlayerAlleyDataType != null ? AccessTools.Field(PlayerAlleyDataType, "TroopRoster") : null;
    private static readonly MethodInfo StateSetter =
        AccessTools.PropertySetter(typeof(Alley), nameof(Alley.State));

    private static AlleyCampaignBehavior Behavior => Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();

    public void AddOrUpdatePlayerAlleyData(Alley alley, Hero overseer, TroopRoster garrison)
    {
        if (!ReflectionReady()) return;
        if (alley == null || garrison == null)
        {
            Logger.Error("Cannot add alley management data with a null alley/garrison");
            return;
        }

        GameThread.RunSafe(() =>
        {
            var behavior = Behavior;
            if (behavior == null) return;
            if (PlayerOwnedDataField.GetValue(behavior) is not IList list) return;

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

                RemoveByAlley(list, alley);
                var data = PlayerAlleyDataCtor.Invoke(new object[] { alley, garrison });
                if (overseer != null) AssignedClanMemberField.SetValue(data, overseer);
                list.Add(data);

                // Promote State in lockstep with the entry. Vanilla's AddPlayerAlleyCharacters does an
                // unguarded _playerOwnedCommonAreaData.First(x => x.Alley == alley), so an OccupiedByPlayer
                // alley with no entry throws; setting State only here means OccupiedByPlayer always has a
                // matching entry, and an alley with no entry stays at its loaded gang-occupied State.
                StateSetter.Invoke(alley, new object[] { Alley.AreaState.OccupiedByPlayer });
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
        if (!ReflectionReady() || alley == null) return;

        GameThread.RunSafe(() =>
        {
            var behavior = Behavior;
            if (behavior == null) return;
            if (PlayerOwnedDataField.GetValue(behavior) is not IList list) return;

            using (new AllowedThread())
            {
                RemoveByAlley(list, alley);
            }
        });
    }

    public bool TryGetCurrentSettlementAlley(out Alley alley)
    {
        alley = null;
        if (!ReflectionReady()) return false;

        var behavior = Behavior;
        if (behavior == null) return false;

        var settlement = Settlement.CurrentSettlement;
        if (settlement == null) return false;

        if (PlayerOwnedDataField.GetValue(behavior) is not IList list) return false;

        foreach (var data in list)
        {
            if (AlleyField.GetValue(data) is Alley a && a.Settlement == settlement)
            {
                alley = a;
                return true;
            }
        }
        return false;
    }

    public IEnumerable<(Alley alley, Hero overseer, TroopRoster garrison)> GetPlayerOwnedAlleys()
    {
        if (!ReflectionReady()) yield break;

        var behavior = Behavior;
        if (behavior == null) yield break;
        if (PlayerOwnedDataField.GetValue(behavior) is not IList list) yield break;

        foreach (var data in list)
        {
            if (data == null) continue;
            if (AlleyField.GetValue(data) is not Alley alley) continue;

            var overseer = AssignedClanMemberField.GetValue(data) as Hero;
            var garrison = TroopRosterField.GetValue(data) as TroopRoster;
            yield return (alley, overseer, garrison);
        }
    }

    private static void RemoveByAlley(IList list, Alley alley)
    {
        // Walk forward, only advancing when we keep an entry, so removing one doesn't skip the next.
        int i = 0;
        while (i < list.Count)
        {
            if (AlleyField.GetValue(list[i]) as Alley == alley)
            {
                list.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }

    private static bool ReflectionReady()
    {
        if (PlayerAlleyDataType == null || PlayerOwnedDataField == null ||
            PlayerAlleyDataCtor == null || AlleyField == null || AssignedClanMemberField == null ||
            TroopRosterField == null || StateSetter == null)
        {
            Logger.Error("AlleyCampaignBehavior.PlayerAlleyData reflection members could not be resolved; alley management cannot be applied");
            return false;
        }
        return true;
    }
}
