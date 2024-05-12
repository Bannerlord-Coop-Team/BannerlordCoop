using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch(typeof(GarrisonPartyComponent))]
internal class GarrisonPartyComponentPatches
{
    [HarmonyPatch(nameof(GarrisonPartyComponent.InitializeGarrisonPartyProperties))]
    private static bool Prefix() => false;
}
