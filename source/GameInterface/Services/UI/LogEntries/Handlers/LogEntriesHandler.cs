using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.LogEntries.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

        messageBroker.Subscribe<CommentGivenBirth>(Handle_CommentGivenBirth);
        messageBroker.Subscribe<NetworkCommentGivenBirth>(Handle_NetworkCommentGivenBirth);

        messageBroker.Subscribe<CommentVillageStateChanged>(Handle_CommentVillageStateChanged);
        messageBroker.Subscribe<NetworkCommentVillageStateChanged>(Handle_NetworkCommentVillageStateChanged);

        messageBroker.Subscribe<CommentClanLeaderChanged>(Handle_CommentClanLeaderChanged);
        messageBroker.Subscribe<NetworkCommentClanLeaderChanged>(Handle_NetworkCommentClanLeaderChanged);

        messageBroker.Subscribe<CommentCharacterBorn>(Handle_CommentCharacterBorn);
        messageBroker.Subscribe<NetworkCommentCharacterBorn>(Handle_NetworkCommentCharacterBorn);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LogPlayerRetired>(Handle_LogPlayerRetired);
        messageBroker.Unsubscribe<NetworkLogPlayerRetired>(Handle_NetworkLogPlayerRetired);

        messageBroker.Unsubscribe<CommentHeroKilled>(Handle_CommentHeroKilled);
        messageBroker.Unsubscribe<NetworkCommentHeroKilled>(Handle_NetworkCommentHeroKilled);

        messageBroker.Unsubscribe<CommentGivenBirth>(Handle_CommentGivenBirth);
        messageBroker.Unsubscribe<NetworkCommentGivenBirth>(Handle_NetworkCommentGivenBirth);

        messageBroker.Unsubscribe<CommentVillageStateChanged>(Handle_CommentVillageStateChanged);
        messageBroker.Unsubscribe<NetworkCommentVillageStateChanged>(Handle_NetworkCommentVillageStateChanged);

        messageBroker.Unsubscribe<CommentClanLeaderChanged>(Handle_CommentClanLeaderChanged);
        messageBroker.Unsubscribe<NetworkCommentClanLeaderChanged>(Handle_NetworkCommentClanLeaderChanged);

        messageBroker.Unsubscribe<CommentCharacterBorn>(Handle_CommentCharacterBorn);
        messageBroker.Unsubscribe<NetworkCommentCharacterBorn>(Handle_NetworkCommentCharacterBorn);
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

    private void Handle_CommentGivenBirth(MessagePayload<CommentGivenBirth> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Mother, out var motherId)) return;

            var aliveChildrenIds = new List<string>();
            foreach (var aliveChild in obj.What.AliveChildren)
            {
                if (!objectManager.TryGetIdWithLogging(aliveChild, out var aliveChildId)) continue;

                aliveChildrenIds.Add(aliveChildId);
            }

            var message = new NetworkCommentGivenBirth(motherId, aliveChildrenIds, obj.What.StillbornCount);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommentGivenBirth(MessagePayload<NetworkCommentGivenBirth> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MotherId, out var mother)) return;

            if (obj.What.AliveChildrenIds == null) return;

            var aliveChildren = new List<Hero>();
            foreach (var aliveChildId in obj.What.AliveChildrenIds)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(aliveChildId, out var aliveChild)) return;

                aliveChildren.Add(aliveChild);
            }

            var commentChildbirthBehavior = Campaign.Current?.GetCampaignBehavior<CommentChildbirthBehavior>();
            commentChildbirthBehavior?.OnGivenBirthEvent(mother, aliveChildren, obj.What.StillbornCount);
        });
    }

    private void Handle_CommentVillageStateChanged(MessagePayload<CommentVillageStateChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Village, out var villageId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.RaiderParty, out var raiderPartyId)) return;

            var message = new NetworkCommentVillageStateChanged(villageId, obj.What.OldState, obj.What.NewState, raiderPartyId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommentVillageStateChanged(MessagePayload<NetworkCommentVillageStateChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Village>(obj.What.VillageId, out var village)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.RaiderPartyId, out var raiderParty)) return;

            var commentOnChangeVillageStateBehavior = Campaign.Current?.GetCampaignBehavior<CommentOnChangeVillageStateBehavior>();
            commentOnChangeVillageStateBehavior?.OnVillageStateChanged(village, obj.What.OldState, obj.What.NewState, raiderParty);
        });
    }

    private void Handle_CommentClanLeaderChanged(MessagePayload<CommentClanLeaderChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.OldLeader, out var oldLeaderId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.NewLeader, out var newLeaderId)) return;

            var message = new NetworkCommentClanLeaderChanged(oldLeaderId, newLeaderId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommentClanLeaderChanged(MessagePayload<NetworkCommentClanLeaderChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OldLeaderId, out var oldLeader)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NewLeaderId, out var newLeader)) return;

            if (Campaign.Current?.GetCampaignBehavior<CommentOnClanLeaderChangedBehavior>() == null) return;

            CommentOnClanLeaderChangedBehavior.OnClanLeaderChanged(oldLeader, newLeader);
        });
    }

    private void Handle_CommentCharacterBorn(MessagePayload<CommentCharacterBorn> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

            var message = new NetworkCommentCharacterBorn(heroId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkCommentCharacterBorn(MessagePayload<NetworkCommentCharacterBorn> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

            var commentCharacterBornBehavior = Campaign.Current?.GetCampaignBehavior<CommentCharacterBornBehavior>();
            commentCharacterBornBehavior?.HeroCreated(hero, true);
        });
    }
}
