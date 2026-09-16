using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Cutscenes.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.Cutscenes.Handlers;

internal class OtherCutscenesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<OtherCutscenesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public OtherCutscenesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<InitiateCutsceneHeroComesOfAge>(Handle_InitiateCutsceneHeroComesOfAge);
        messageBroker.Subscribe<NetworkInitiateCutsceneHeroComesOfAge>(Handle_NetworkInitiateCutsceneHeroComesOfAge);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitiateCutsceneHeroComesOfAge>(Handle_InitiateCutsceneHeroComesOfAge);
        messageBroker.Unsubscribe<NetworkInitiateCutsceneHeroComesOfAge>(Handle_NetworkInitiateCutsceneHeroComesOfAge);
    }

    private void Handle_InitiateCutsceneHeroComesOfAge(MessagePayload<InitiateCutsceneHeroComesOfAge> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        network.SendAll(new NetworkInitiateCutsceneHeroComesOfAge(heroId));
    }

    private void Handle_NetworkInitiateCutsceneHeroComesOfAge(MessagePayload<NetworkInitiateCutsceneHeroComesOfAge> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryGetCutscenesBehavior(out var cutscenesBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            cutscenesBehavior.OnHeroComesOfAge(hero);
        });
    }

    private bool TryGetCutscenesBehavior(out DefaultCutscenesCampaignBehavior cutscenesBehavior)
    {
        cutscenesBehavior = Campaign.Current?.GetCampaignBehavior<DefaultCutscenesCampaignBehavior>();
        if (cutscenesBehavior != null) return true;

        Logger.Debug("Skipping cutscene update because DefaultCutscenesCampaignBehavior is unavailable.");
        return false;
    }
}
