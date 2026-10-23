using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans;

public interface IPlayerDivorceRules : IGameAbstraction
{
    bool CanDivorce(Hero first, Hero second);
    bool CanLeaveClan(Hero first, Hero second);
    bool HaveSharedChildren(Hero first, Hero second);
    bool TryApply(Hero actor, Hero spouse, bool leaveClan);
}

public class PlayerDivorceRules : IPlayerDivorceRules
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerDivorceRules>();

    private readonly IClanLeaveRules clanLeaveRules;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public PlayerDivorceRules(
        IClanLeaveRules clanLeaveRules,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        INetwork network)
    {
        this.clanLeaveRules = clanLeaveRules;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.network = network;
    }

    public bool CanDivorce(Hero first, Hero second)
        => first != null && second != null && first.IsPlayerHero() && second.IsPlayerHero() &&
           first.Spouse == second && second.Spouse == first;

    public bool CanLeaveClan(Hero first, Hero second)
    {
        var junior = GetJuniorPartner(first, second);
        return junior != null && clanLeaveRules.CanLeaveAfterDivorce(junior);
    }

    public bool HaveSharedChildren(Hero first, Hero second)
        => first.Children.Any(child => child.IsAlive && (child.Father == second || child.Mother == second));

    public bool TryApply(Hero actor, Hero spouse, bool leaveClan)
    {
        if (!CanDivorce(actor, spouse) || (leaveClan && !CanLeaveClan(actor, spouse))) return false;

        var clan = actor.Clan;
        var junior = GetJuniorPartner(actor, spouse);
        actor.Spouse = null;
        NotifyDivorce(actor, spouse);
        NotifyDivorce(spouse, actor);
        messageBroker.Publish(this, new ClanManagementChanged(clan, ClanManagementRefresh.Members));

        // CanLeaveClan already checked the departure, so this only fails on a lookup error and the divorce still stands.
        if (leaveClan && !clanLeaveRules.TryApply(junior, isRemoval: junior != actor))
        {
            Logger.Warning("Divorced players but {Hero} could not leave the clan", junior.Name);
        } 

        return actor.Spouse == null && spouse.Spouse == null;
    }

    private void NotifyDivorce(Hero hero, Hero formerSpouse)
    {
        if (!PlayerManager.TryGetControlledObjectInfo(hero, out var controller) ||
            !playerManager.TryGetPeer(controller.ObjectControllerId, out var peer)) return;

        network.Send(peer, new SendInformationMessage(GameTexts.FindText("str_coop_divorce_completed")
            .SetTextVariable("SPOUSE", formerSpouse.Name).ToString()));
    }

    // The partner who doesn't lead the shared clan is the one who leaves it.
    private static Hero GetJuniorPartner(Hero first, Hero second)
    {
        if (first.Clan == null || first.Clan != second.Clan) return null;
        if (first.Clan.Leader == first) return second;
        return first.Clan.Leader == second ? first : null;
    }
}
