using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch(typeof(CaravanPartyComponent))]
internal class CaravanPartyComponentPatches
{
    [HarmonyPatch(nameof(CaravanPartyComponent.InitializeCaravanOnCreation))]
    private static bool Prefix() => false;
}
