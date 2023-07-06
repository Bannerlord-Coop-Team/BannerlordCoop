using Common.Util;
using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Disables party encounters
/// </summary>

public class DisablePartyEncounterPatch
{
    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix() => false;
}
