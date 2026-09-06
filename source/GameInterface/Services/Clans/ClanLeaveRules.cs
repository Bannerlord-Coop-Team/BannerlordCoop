using Common.Messaging;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans;

public interface IClanLeaveRules : IGameAbstraction
{
    bool CanLeave(Hero member);
    bool CanRemove(Hero actor, Hero member);
    bool TryApply(Hero member, MobileParty party);
}

public class ClanLeaveRules : IClanLeaveRules
{
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;

    public ClanLeaveRules(IPlayerManager playerManager, IObjectManager objectManager, IMessageBroker messageBroker)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
    }

    public bool CanLeave(Hero member)
    {
        return member?.Clan != null && member.Clan.Leader != member && playerManager.Contains(member) &&
            (member.Spouse == null || !playerManager.Contains(member.Spouse));
    }

    public bool CanRemove(Hero actor, Hero member)
    {
        return CanLeave(member) && actor != null && actor.Clan == member.Clan && member.Clan.Leader == actor;
    }

    public bool TryApply(Hero member, MobileParty party)
    {
        if (!CanLeave(member)) return false;
        if (!objectManager.TryGetIdWithLogging(member, out var heroId)) return false;
        var player = playerManager.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        if (player == null) return false;
        if (!objectManager.TryGetObjectWithLogging(player.OriginalClanId ?? player.ClanId, out Clan originalClan)) return false;

        // Older saves may have replaced ClanId on reconnect before OriginalClanId was recorded.
        if (originalClan == member.Clan)
            originalClan = Clan.All.FirstOrDefault(clan => clan != member.Clan && clan.Leader == member);
        if (originalClan == null) return false;
        if (!objectManager.TryGetIdWithLogging(originalClan, out var originalClanId)) return false;

        if ((player.ClanId != originalClanId || player.OriginalClanId != originalClanId) &&
            !playerManager.ReplacePlayer(player, new Player(player.ControllerId, player.HeroId,
                player.MobilePartyId, originalClanId, player.CharacterObjectId, originalClanId))) return false;

        if (party.Army != null && party.MapFaction != originalClan.MapFaction)
            party.Army = null;

        originalClan.SetLeader(member);
        party.ActualClan = originalClan;
        messageBroker.Publish(this, new PlayerBannerChanged(originalClan));
        return true;
    }
}
