using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenLogic))]
internal class PartyScreenLogicPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyScreenLogic>();

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

            var message = new AttemptPartyDoneLogic(
                Hero.MainHero,
                takenPrisonersRoster,
                donatedPrisonersRoster,
                recruitedPrisonersRoster,
                __instance.MemberRosters[0],
                __instance.PrisonerRosters[0],
                __instance.MemberRosters[1],
                __instance.PrisonerRosters[1],
                __instance.RightOwnerParty.ItemRoster,
                __instance.CurrentData.UpgradedTroopsHistory,
                __instance.CurrentData.LeftParty,
                __instance.CurrentData.PartyGoldChangeAmount,
                __instance.CurrentData.PartyInfluenceChangeAmount.Item2,
                __instance.CurrentData.PartyMoraleChangeAmount,
                __instance.DoNotApplyGoldTransactions
            );

            MessageBroker.Instance.Publish(__instance, message);

            // Manage changing rosters on the server
            __instance.CurrentData.ResetUsing(__instance._initialData);

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
        }

        __result = flag;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenLogic.ExecuteTroop))]
    [HarmonyPostfix]
    public static void ExecuteTroopPostfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command)
    {
        if (!__instance.ValidateCommand(command)) return;

        // Send message to server to run KillCharacterAction.ApplyByExecution
        var message = new HeroExecuted(command.Character.HeroObject, Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);
    }
}