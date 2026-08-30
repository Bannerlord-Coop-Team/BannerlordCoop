using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.LogEntries.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.UI.LogEntries.Handlers;

internal class LogEntriesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LogEntriesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public LogEntriesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<LogPlayerRetired>(Handle_LogPlayerRetired);
        messageBroker.Subscribe<NetworkLogPlayerRetired>(Handle_NetworkLogPlayerRetired);

        messageBroker.Subscribe<CommentHeroKilled>(Handle_CommentHeroKilled);
        messageBroker.Subscribe<NetworkCommentHeroKilled>(Handle_NetworkCommentHeroKilled);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LogPlayerRetired>(Handle_LogPlayerRetired);
        messageBroker.Unsubscribe<NetworkLogPlayerRetired>(Handle_NetworkLogPlayerRetired);

        messageBroker.Unsubscribe<CommentHeroKilled>(Handle_CommentHeroKilled);
        messageBroker.Unsubscribe<NetworkCommentHeroKilled>(Handle_NetworkCommentHeroKilled);
    }

    private void Handle_LogPlayerRetired(MessagePayload<LogPlayerRetired> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.RetiredHero, out var retiredHeroId)) return;

            var message = new NetworkLogPlayerRetired(retiredHeroId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkLogPlayerRetired(MessagePayload<NetworkLogPlayerRetired> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.RetiredHeroId, out var retiredHero)) return;

            LogEntry.AddLogEntry(new PlayerRetiredLogEntry(retiredHero));
        });
    }

    private void Handle_CommentHeroKilled(MessagePayload<CommentHeroKilled> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Victim, out var victimId)) return;

            string killerId = null;
            if (obj.What.Killer != null && !objectManager.TryGetIdWithLogging(obj.What.Killer, out killerId)) return;

            var message = new NetworkCommentHeroKilled(victimId, killerId, obj.What.Detail);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommentHeroKilled(MessagePayload<NetworkCommentHeroKilled> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.VictimId, out var victim)) return;

            Hero killer = null;
            if (obj.What.KillerId != null && !objectManager.TryGetObjectWithLogging(obj.What.KillerId, out killer)) return;

            var commentOnKilledBehavior = Campaign.Current?.GetCampaignBehavior<CommentOnCharacterKilledBehavior>();
            commentOnKilledBehavior?.OnBeforeHeroKilled(victim, killer, obj.What.Detail, true);
        });
    }
}
