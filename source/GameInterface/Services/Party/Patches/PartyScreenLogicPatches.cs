using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenLogic))]
internal class PartyScreenLogicPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenLogic>();
    [ThreadStatic]
    private static bool _inCommit;
    internal static bool InCommit
    {
        get => _inCommit;
        private set => _inCommit = value;
    }
    [HarmonyPatch(nameof(PartyScreenLogic.DoneLogic))]
    [HarmonyPrefix]
    public static bool DoneLogicPrefix(PartyScreenLogic __instance, ref bool __result, bool isForced)
    {
        if (Hero.MainHero.Gold < -__instance.CurrentData.PartyGoldChangeAmount && __instance.CurrentData.PartyGoldChangeAmount < 0)
        {
            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_inventory_popup_player_not_enough_gold", null), 0, null, null, "");
            __result = false;
            return false;
        }

        FlattenedTroopRoster takenPrisonersRoster = new FlattenedTroopRoster(4);
        FlattenedTroopRoster donatedPrisonersRoster = new FlattenedTroopRoster(4);
        foreach (Tuple<CharacterObject, int> tuple in __instance.CurrentData.TransferredPrisonersHistory)
        {
            int number = MathF.Abs(tuple.Item2);
            if (tuple.Item2 < 0)
            {
                takenPrisonersRoster.Add(tuple.Item1, number, 0);
            }
            else if (tuple.Item2 > 0)
            {
                donatedPrisonersRoster.Add(tuple.Item1, number, 0);
            }
        }

        bool flag = __instance.PartyPresentationDoneButtonDelegate(__instance.MemberRosters[0], __instance.PrisonerRosters[0], __instance.MemberRosters[1], __instance.PrisonerRosters[1], donatedPrisonersRoster, takenPrisonersRoster, isForced, __instance.LeftOwnerParty, __instance.RightOwnerParty);
        if (flag)
        {
            FlattenedTroopRoster recruitedPrisonersRoster = new FlattenedTroopRoster(4);
            foreach (Tuple<CharacterObject, int> tuple in __instance.CurrentData.RecruitedPrisonersHistory)
            {
                recruitedPrisonersRoster.Add(tuple.Item1, tuple.Item2, 0);
            }

            var partyScreenMode = __instance._partyScreenMode;
            if (Game.Current.GameStateManager.ActiveState is PartyState partyState)
            {
                partyScreenMode = partyState.PartyScreenMode;
            }

            var message = new PartyDoneLogicAttempted(
                Hero.MainHero,
                takenPrisonersRoster,
                donatedPrisonersRoster,
                recruitedPrisonersRoster,
                __instance.MemberRosters[0],
                __instance.PrisonerRosters[0],
                __instance.MemberRosters[1],
                __instance.PrisonerRosters[1],
                __instance._initialData.LeftMemberRoster,
                __instance._initialData.LeftPrisonerRoster,
                __instance._initialData.RightMemberRoster,
                __instance._initialData.RightPrisonerRoster,
                __instance.RightOwnerParty.ItemRoster,
                __instance.CurrentData.UpgradedTroopsHistory,
                __instance.CurrentData.LeftParty,
                __instance.CurrentData.PartyGoldChangeAmount,
                __instance.CurrentData.PartyInfluenceChangeAmount.Item2,
                __instance.CurrentData.PartyMoraleChangeAmount,
                __instance.DoNotApplyGoldTransactions,
                partyScreenMode
            );

            MessageBroker.Instance.Publish(__instance, message);
            // Manage changing rosters on the server
            using (new AllowedThread())
            {
                TroopRoster duplicateLeftMemberRoster = __instance.MemberRosters[0].CloneRosterData();
                TroopRoster duplicateLeftPrisonerRoster = __instance.PrisonerRosters[0].CloneRosterData();

                InCommit = true;
                try
                {
                    __instance.Reset(true);

                    //__instance.FireCampaignRelatedEvents(); // Managed on server
                    __instance.SetPartyGoldChangeAmount(0);
                    __instance.SetHorseChangeAmount(0);
                    __instance.SetInfluenceChangeAmount(0, 0, 0);
                    __instance.SetMoraleChangeAmount(0);
                    __instance.CurrentData.UpgradedTroopsHistory = new List<Tuple<CharacterObject, CharacterObject, int>>();
                    __instance.CurrentData.TransferredPrisonersHistory = new List<Tuple<CharacterObject, int>>();
                    __instance.CurrentData.RecruitedPrisonersHistory = new List<Tuple<CharacterObject, int>>();
                    __instance.CurrentData.UsedUpgradeHorsesHistory = new List<Tuple<EquipmentElement, int>>();
                    __instance._initialData.CopyFromScreenData(__instance.CurrentData);

                    // In vanilla, the rosters would already be updated but with this patch the rosters are reset on the client to be managed by the server.
                    // This assigns a duplicate version of the left rosters needed in extra logic handled by the PartyScreenHelper when closing the party screen.
                    // For example, the left member roster when creating a new clan party is not managed on the server but the server does need this data.
                    __instance.MemberRosters[0] = duplicateLeftMemberRoster;
                    __instance.PrisonerRosters[1] = duplicateLeftPrisonerRoster;
                }
                finally
                {
                    InCommit = false;
                }
            }
        }
        __result = flag;
        return false;
    }

    /// <summary>
    /// Executing prisoner heroes is disabled in coop: the kill rides KillCharacterAction and its follow-on
    /// death/inheritance handling, which crashes the game when it targets a lord or player
    /// (<see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/2310">issue #2310</see>).
    /// Skipping the original also skips its local prisoner-roster mutation, so nothing diverges from the server.
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.ExecuteTroop))]
    [HarmonyPrefix]
    public static bool ExecuteTroopPrefix() => false;

    /// <summary>
    /// Reports every prisoner as non-executable so the party screen disables the execute button and
    /// <see cref="PartyScreenLogic.ValidateCommand"/> rejects any ExecuteTroop command (issue #2310).
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.IsExecutable))]
    [HarmonyPrefix]
    public static bool IsExecutablePrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    internal const string ExecutionDisabledReason = "Executing prisoners is disabled in Co-op.";

    /// <summary>
    /// The disabled execute button's tooltip; the native "Cannot execute hero right now" would suggest
    /// execution can become available.
    /// </summary>
    [HarmonyPatch(nameof(PartyScreenLogic.GetExecutableReasonString))]
    [HarmonyPrefix]
    public static bool GetExecutableReasonStringPrefix(ref string __result)
    {
        __result = ExecutionDisabledReason;
        return false;
    }
}