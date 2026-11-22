using Common;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Traits.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Traits.Patches;

[HarmonyPatch(typeof(TraitLevelingHelper))]
internal class TraitLevelingHelperPatches
{
    [HarmonyPatch(nameof(TraitLevelingHelper.AddPlayerTraitXPAndLogEntry))]
    [HarmonyPrefix]
    public static bool AddPlayerTraitXPAndLogEntryPrefix(TraitObject trait, int xpValue, ActionNotes context, Hero referenceHero)
    {
        if (ModInformation.IsClient) return false;

        // Callers set the player hero through MainHeroSubstitutionScope
        var playerHero = ResolvedMainHeroContext.ResolvedMainHero;
        if (playerHero == null || !playerHero.IsPlayerHero()) return false;

        if (!ContainerProvider.TryResolve<ITraitLevelingHelperInterface>(out var traitLevelingHelperInterface)) return false;

        traitLevelingHelperInterface.AddTraitXp(playerHero, trait, xpValue, context, referenceHero);

        return false;
    }

    [HarmonyPatch(nameof(TraitLevelingHelper.OnBattleWon))]
    [HarmonyPrefix]
    public static bool OnBattleWonPrefix(MapEvent mapEvent, float contribution)
    {
        if (ModInformation.IsClient) return false;

        // Vanilla reads the side from PlayerEncounter.Current, which doesn't exist on the server
        var playerParty = PartyBase.MainParty;
        if (playerParty == null || playerParty.MapEvent != mapEvent) return false;

        float strengthRatio = mapEvent.GetMapEventSide(playerParty.Side).StrengthRatio;
        if (strengthRatio <= 9f) return false;

        int xp = (int)(MBMath.Map(strengthRatio, 9f, 10f, 5f, 20f) * contribution);
        if (xp > 0)
        {
            TraitLevelingHelper.AddPlayerTraitXPAndLogEntry(DefaultTraits.Valor, xp, ActionNotes.BattleValor, null);
        }

        return false;
    }
}
