using Common.Logging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Traits.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

namespace GameInterface.Services.Traits.Interfaces;

public interface ITraitLevelingHelperInterface : IGameAbstraction
{
    void AddTraitXp(Hero playerHero, TraitObject trait, int xp, ActionNotes note, Hero referenceHero);

    void InitializeTraitXp(Hero playerHero);
}

/// <summary>
/// Per player version of TraitLevelingHelper, which only tracks trait xp for Hero.MainHero
/// </summary>
public class TraitLevelingHelperInterface : ITraitLevelingHelperInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TraitLevelingHelperInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    public TraitLevelingHelperInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
    }

    public void AddTraitXp(Hero playerHero, TraitObject trait, int xp, ActionNotes note, Hero referenceHero)
    {
        if (!TryGetPlayerTraitXp(out var playerTraitXp)) return;
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(trait, out var traitId)) return;

        if (!playerTraitXp.TryGetValue(playerHeroId, out var traitXp))
        {
            traitXp = CreateTraitXp(playerHero);
            playerTraitXp[playerHeroId] = traitXp;
        }

        int previousLevel = playerHero.GetTraitLevel(trait);

        traitXp.TryGetValue(traitId, out var currentXp);
        Campaign.Current.Models.CharacterDevelopmentModel.GetTraitLevelForTraitXp(playerHero, trait, currentXp + xp, out var traitLevel, out var newXp);
        traitXp[traitId] = newXp;

        if (traitLevel != previousLevel)
        {
            playerHero.SetTraitLevel(trait, traitLevel);
        }

        bool levelChanged = previousLevel != playerHero.GetTraitLevel(trait);
        bool addLogEntry = MathF.Abs(xp) >= 10;
        if (!levelChanged && !addLogEntry) return;

        if (!objectManager.TryGetHandleWithLogging(playerHero, out var playerHeroHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(trait, out var traitHandle)) return;

        uint referenceHeroHandle = 0;
        if (referenceHero != null && !objectManager.TryGetHandleWithLogging(referenceHero, out referenceHeroHandle)) return;

        if (!PlayerManager.TryGetControlledObjectInfo(playerHero, out var controller)) return;
        if (!playerManager.TryGetPeer(controller.ObjectControllerId, out var peer)) return;

        network.Send(peer, new NetworkPlayerTraitXpAdded(playerHeroHandle, traitHandle, previousLevel, levelChanged, addLogEntry, note, referenceHeroHandle));
    }

    public void InitializeTraitXp(Hero playerHero)
    {
        if (!TryGetPlayerTraitXp(out var playerTraitXp)) return;
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;

        playerTraitXp[playerHeroId] = CreateTraitXp(playerHero);
    }

    // Matches TraitLevelingHelper.UpdateTraitXPAccordingToTraitLevels, seeding xp from the hero's current levels
    private Dictionary<string, int> CreateTraitXp(Hero playerHero)
    {
        var traitXp = new Dictionary<string, int>();

        foreach (TraitObject trait in TraitObject.All)
        {
            int traitLevel = playerHero.GetTraitLevel(trait);
            if (traitLevel == 0) continue;

            if (!objectManager.TryGetIdWithLogging(trait, out var traitId)) continue;

            traitXp[traitId] = Campaign.Current.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(trait, traitLevel);
        }

        return traitXp;
    }

    private bool TryGetPlayerTraitXp(out Dictionary<string, Dictionary<string, int>> playerTraitXp)
    {
        playerTraitXp = coopSessionProvider.CoopSession?.PlayerTraitData?.PlayerTraitXp;
        if (playerTraitXp != null) return true;

        Logger.Error("PlayerTraitData was null; cannot track player trait xp");
        return false;
    }
}
