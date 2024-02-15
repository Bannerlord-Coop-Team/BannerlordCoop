using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyPatches
{
    [HarmonyPatch(nameof(MobileParty.InitializeMobilePartyAroundPosition))]
    [HarmonyPatch(new Type[] { typeof(PartyTemplateObject), typeof(Vec2), typeof(float), typeof(float), typeof(int) })]
    private static bool Prefix() => false;
}
