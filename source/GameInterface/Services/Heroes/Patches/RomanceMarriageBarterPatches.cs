using Common;
using Common.Network;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(BarterManager))]
internal class RomanceMarriageBarterPatches
{
    private static readonly FieldInfo PrisonerCharacterField =
        AccessTools.Field(typeof(TransferPrisonerBarterable), "_prisonerCharacter");

    [HarmonyPatch(nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    [HarmonyPrefix]
    private static bool ApplyAndFinalizePlayerBarterPrefix(Hero offererHero, BarterData barterData)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (offererHero == null || !offererHero.IsControlledByThisInstance()) return true;
        if (barterData == null) return true;

        var marriageBarterable = barterData.GetOfferedBarterables().OfType<MarriageBarterable>().FirstOrDefault();
        if (marriageBarterable == null) return true;

        if (!TryGetTarget(offererHero, marriageBarterable, out var targetHero))
        {
            ShowMessage("Only player-to-NPC marriages are supported in co-op.");
            return false;
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(targetHero, out var targetHeroId))
        {
            ShowMessage("Unable to send the marriage offer to the server.");
            return false;
        }

        if (!TryCreateTerms(barterData.GetOfferedBarterables(), objectManager, out var terms))
        {
            ShowMessage("This marriage offer contains a barter term that co-op cannot validate.");
            return false;
        }

        network.SendAll(new NetworkRequestRomanceMarriageBarter(targetHeroId, terms.ToArray()));
        return false;
    }

    private static bool TryGetTarget(Hero offererHero, MarriageBarterable marriageBarterable, out Hero targetHero)
    {
        targetHero = null;

        if (marriageBarterable.HeroBeingProposedTo == offererHero)
            targetHero = marriageBarterable.ProposingHero;
        else if (marriageBarterable.ProposingHero == offererHero)
            targetHero = marriageBarterable.HeroBeingProposedTo;

        return targetHero != null && !targetHero.IsPlayerHero();
    }

    private static bool TryCreateTerms(
        IEnumerable<Barterable> barterables,
        IObjectManager objectManager,
        out List<RomanceBarterTerm> terms)
    {
        terms = new List<RomanceBarterTerm>();
        foreach (var barterable in barterables)
        {
            if (barterable is MarriageBarterable) continue;
            if (!TryCreateTerm(barterable, objectManager, out var term)) return false;

            terms.Add(term);
        }

        return true;
    }

    private static bool TryCreateTerm(Barterable barterable, IObjectManager objectManager, out RomanceBarterTerm term)
    {
        term = default;
        if (barterable == null || barterable.CurrentAmount <= 0) return false;
        if (!objectManager.TryGetId(barterable.OriginalOwner, out var ownerHeroId)) return false;

        switch (barterable)
        {
            case GoldBarterable:
                term = new RomanceBarterTerm(
                    RomanceBarterTermType.Gold,
                    ownerHeroId,
                    null,
                    null,
                    true,
                    barterable.CurrentAmount);
                return true;
            case ItemBarterable itemBarterable:
                return TryCreateItemTerm(itemBarterable, ownerHeroId, objectManager, out term);
            case FiefBarterable fiefBarterable:
                if (!objectManager.TryGetId(fiefBarterable.TargetSettlement, out var settlementId)) return false;

                term = new RomanceBarterTerm(
                    RomanceBarterTermType.Fief,
                    ownerHeroId,
                    settlementId,
                    null,
                    true,
                    barterable.CurrentAmount);
                return true;
            case TransferPrisonerBarterable prisonerBarterable:
                return TryCreatePrisonerTerm(prisonerBarterable, ownerHeroId, objectManager, out term);
            default:
                return false;
        }
    }

    private static bool TryCreateItemTerm(
        ItemBarterable barterable,
        string ownerHeroId,
        IObjectManager objectManager,
        out RomanceBarterTerm term)
    {
        term = default;
        var equipmentElement = barterable.ItemRosterElement.EquipmentElement;
        if (!objectManager.TryGetId(equipmentElement.Item, out var itemId)) return false;

        var modifier = equipmentElement.ItemModifier;
        if (modifier == null)
        {
            term = new RomanceBarterTerm(
                RomanceBarterTermType.Item,
                ownerHeroId,
                itemId,
                null,
                true,
                barterable.CurrentAmount);
            return true;
        }

        if (!objectManager.TryGetId(modifier, out var modifierId)) return false;

        term = new RomanceBarterTerm(
            RomanceBarterTermType.Item,
            ownerHeroId,
            itemId,
            modifierId,
            false,
            barterable.CurrentAmount);
        return true;
    }

    private static bool TryCreatePrisonerTerm(
        TransferPrisonerBarterable barterable,
        string ownerHeroId,
        IObjectManager objectManager,
        out RomanceBarterTerm term)
    {
        term = default;
        var prisoner = PrisonerCharacterField?.GetValue(barterable) as Hero;
        if (prisoner?.CharacterObject == null ||
            !objectManager.TryGetId(prisoner.CharacterObject, out var characterId))
        {
            return false;
        }

        term = new RomanceBarterTerm(
            RomanceBarterTermType.Prisoner,
            ownerHeroId,
            characterId,
            null,
            true,
            barterable.CurrentAmount);
        return true;
    }

    private static void ShowMessage(string message)
        => InformationManager.DisplayMessage(new InformationMessage(message));
}
