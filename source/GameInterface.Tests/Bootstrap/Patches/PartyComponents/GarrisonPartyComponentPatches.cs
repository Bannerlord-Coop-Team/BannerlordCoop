using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch(typeof(GarrisonPartyComponent.InitializationArgs))]
internal class GarrisonPartyComponentPatches
{
    [HarmonyPatch(nameof(GarrisonPartyComponent.InitializationArgs.InitializeGarrisonPartyProperties))]
    private static bool Prefix() => false;
}
