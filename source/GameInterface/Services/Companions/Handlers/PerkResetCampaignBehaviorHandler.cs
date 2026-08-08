using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Companions.Handlers;

internal class PerkResetCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PerkResetCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PerkResetCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ResetPerksByArenaMaster>(Handle_ResetPerksByArenaMaster);
        messageBroker.Subscribe<NetworkResetPerksByArenaMaster>(Handle_NetworkResetPerksByArenaMaster);

        messageBroker.Subscribe<RemoveACompanionFromPlayerParty>(Handle_RemoveACompanionFromPlayerParty);
        messageBroker.Subscribe<NetworkRemoveACompanionFromPlayerParty>(Handle_NetworkRemoveACompanionFromPlayerParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ResetPerksByArenaMaster>(Handle_ResetPerksByArenaMaster);
        messageBroker.Unsubscribe<NetworkResetPerksByArenaMaster>(Handle_NetworkResetPerksByArenaMaster);

        messageBroker.Unsubscribe<RemoveACompanionFromPlayerParty>(Handle_RemoveACompanionFromPlayerParty);
        messageBroker.Unsubscribe<NetworkRemoveACompanionFromPlayerParty>(Handle_NetworkRemoveACompanionFromPlayerParty);
    }

    private void Handle_ResetPerksByArenaMaster(MessagePayload<ResetPerksByArenaMaster> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.HeroForPerkReset, out var heroForPerkResetId)) return;
        if (!objectManager.TryGetIdWithLogging(data.SelectedSkillForReset, out var selectedSkillForResetId)) return;

        var message = new NetworkResetPerksByArenaMaster(
            mainHeroId,
            data.PerkResetCost,
            heroForPerkResetId,
            selectedSkillForResetId
        );

        network.SendAll(message);
    }

    private void Handle_NetworkResetPerksByArenaMaster(MessagePayload<NetworkResetPerksByArenaMaster> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetPerkResetCampaignBehavior(out var perkResetBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroForPerkResetId, out var heroForPerkReset)) return;
            if (!objectManager.TryGetObjectWithLogging<SkillObject>(data.SelectedSkillForResetId, out var selectedSkillForReset)) return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, data.PerkResetCost, false);
            perkResetBehavior.ResetPerkTreeForHero(heroForPerkReset, selectedSkillForReset);
        });
    }

    private void Handle_RemoveACompanionFromPlayerParty(MessagePayload<RemoveACompanionFromPlayerParty> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.PlayerClan, out var playerClanId)) return;

        var message = new NetworkRemoveACompanionFromPlayerParty(playerClanId);
        network.SendAll(message);
    }

    private void Handle_NetworkRemoveACompanionFromPlayerParty(MessagePayload<NetworkRemoveACompanionFromPlayerParty> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.PlayerClanId, out var playerClan)) return;

            // Re-implement vanilla to not use Clan.PlayerClan
            int companionsCount = playerClan.Companions.Count;
            int num = MBRandom.RandomInt(companionsCount);
            for (int i = 0; i < companionsCount; i++)
            {
                int index = (i + num) % companionsCount;
                Hero targetCompanion = playerClan.Companions[index];
                if ((targetCompanion.PartyBelongedTo?.MapEvent) == null)
                {
                    Settlement currentSettlement = targetCompanion.CurrentSettlement;
                    if ((currentSettlement?.Party.MapEvent) == null && !Campaign.Current.IssueManager.IssueSolvingCompanionList.Contains(targetCompanion))
                    {
                        KillCharacterAction.ApplyByRemove(targetCompanion, true, true);
                        return;
                    }
                }
            }
        });
    }

    private bool TryGetPerkResetCampaignBehavior(out PerkResetCampaignBehavior perkResetBehavior)
    {
        perkResetBehavior = Campaign.Current?.GetCampaignBehavior<PerkResetCampaignBehavior>();
        if (perkResetBehavior != null) return true;

        Logger.Debug("Skipping perk reset update because the campaign behavior is unavailable.");
        return false;
    }
}
