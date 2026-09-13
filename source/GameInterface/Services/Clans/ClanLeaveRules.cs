using Common.Messaging;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans;

public interface IClanLeaveRules : IGameAbstraction
{
    bool CanLeave(Hero member);
    bool CanRemove(Hero actor, Hero member);
    bool TryApply(Hero member);
}

public class ClanLeaveRules : IClanLeaveRules
{
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IClanMemberGrouping grouping;
    private readonly ISessionAlleyPlayerDataInterface alleyData;

    public ClanLeaveRules(IPlayerManager playerManager, IObjectManager objectManager, IMessageBroker messageBroker,
        IClanMemberGrouping grouping, ISessionAlleyPlayerDataInterface alleyData)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.grouping = grouping;
        this.alleyData = alleyData;
    }

    public bool CanLeave(Hero member)
    {
        return member?.Clan != null && member.IsAlive && member.Clan.Leader != member && playerManager.Contains(member) &&
            !grouping.AreRelated(member, member.Clan.Leader) &&
            (member.Spouse == null || !playerManager.Contains(member.Spouse));
    }

    public bool CanRemove(Hero actor, Hero member)
    {
        return CanLeave(member) && actor != null && actor.Clan == member.Clan && member.Clan.Leader == actor;
    }

    public bool TryApply(Hero member)
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

        var sourceClan = member.Clan;
        var family = new HashSet<Hero>(sourceClan.AliveLords.Where(hero => hero != member &&
            !playerManager.Contains(hero) && (hero == member.Spouse || hero.Father == member || hero.Mother == member ||
                (grouping.GetGroup(hero, member) == ClanMemberGroup.Family &&
                 grouping.GetGroup(hero, sourceClan.Leader) != ClanMemberGroup.Family))));

        if ((player.ClanId != originalClanId || player.OriginalClanId != originalClanId) &&
            !playerManager.ReplacePlayer(player, new Player(player.ControllerId, player.HeroId,
                player.MobilePartyId, originalClanId, player.CharacterObjectId, originalClanId))) return false;

        // Captured players can leave too, even when their party no longer exists.
        objectManager.TryGetObject(player.MobilePartyId, out MobileParty party);
        if (party?.Army != null && party.MapFaction != originalClan.MapFaction)
            party.Army = null;

        RemoveAssignments(member, party);
        foreach (var relative in family)
        {
            RemoveAssignments(relative, party);
            relative.Clan = originalClan;
        }

        if (party != null)
        {
            foreach (var hero in party.MemberRoster.GetTroopRoster().Select(troop => troop.Character.HeroObject)
                .Where(hero => hero != null && !playerManager.Contains(hero) && !family.Contains(hero)).ToArray())
            {
                party.RemoveAllPartyRolesOfHero(hero);
                MakeHeroFugitiveAction.Apply(hero, false);
            }
            party.ActualClan = originalClan;
        }

        originalClan.SetLeader(member);
        messageBroker.Publish(this, new PlayerBannerChanged(originalClan));
        return true;
    }

    private void RemoveAssignments(Hero hero, MobileParty retainedParty)
    {
        if (hero.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(hero);

        var teleportation = Campaign.Current.GetCampaignBehavior<TeleportationCampaignBehavior>();
        if (teleportation != null)
        {
            foreach (var pending in teleportation._teleportationList.Where(data => data.TeleportingHero == hero ||
                (hero == retainedParty?.LeaderHero && data.TargetParty == retainedParty)).ToArray())
                teleportation.RemoveTeleportationData(pending, isCanceled: true);
        }

        if (objectManager.TryGetId(hero, out var heroId))
        {
            foreach (var alley in hero.Clan.Heroes.SelectMany(member => member.OwnedAlleys))
            {
                if (!objectManager.TryGetId(alley, out var alleyId) ||
                    !alleyData.TryGetManagementData(alleyId, out var data) || data.OverseerId != heroId) continue;
                // An occupied alley needs an overseer; its owner takes over until they assign another hero.
                if (objectManager.TryGetIdWithLogging(alley.Owner, out var ownerId))
                    messageBroker.Publish(this, new RequestChangeAlleyOverseer(alleyId, ownerId));
            }
        }

        if (playerManager.Contains(hero) || hero.IsPrisoner) return;
        var party = hero.PartyBelongedTo;
        if (party == null) return;
        party.RemoveAllPartyRolesOfHero(hero);
        if (party == retainedParty) return;

        bool disband = party.LeaderHero == hero && !playerManager.Contains(party);
        if (disband) party.RemovePartyLeader();
        MakeHeroFugitiveAction.Apply(hero, false);
        if (disband) DisbandPartyAction.StartDisband(party);
    }
}
