using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.CoopSessionData;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public interface IClanFinance : IGameAbstraction
{
    bool IsNonLeaderMember(Hero hero);
    bool CanChangeGold(Hero hero);
    void UpdateDisconnectedHeroes(string[] heroIds);
    ClanFinanceSettings GetSettings(Hero member);
    void Initialize(Dictionary<string, ClanFinanceSettings> settings);
    bool TryChange(Hero actor, Hero member, Clan clan, int amount, bool tribute);
    void Apply(string memberId, ClanFinanceSettings settings);
    void Clear(Hero hero);
    void AddPaymentExpenses(Clan clan, ref ExplainedNumber change);
    ExplainedNumber CalculateMemberGoldChange(Hero member, bool includeDescriptions = true, bool applyWithdrawals = false);
    int ApplyDailyTransfers(Clan clan);
}

public class ClanFinance : IClanFinance
{
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly INetwork network;
    private HashSet<string> disconnectedHeroes = new();

    private Dictionary<string, ClanFinanceSettings> Settings => coopSessionProvider.CoopSession.ClanFinance;

    public ClanFinance(
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        ICoopSessionProvider coopSessionProvider,
        INetwork network)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.coopSessionProvider = coopSessionProvider;
        this.network = network;
    }

    public bool IsNonLeaderMember(Hero hero) => hero?.Clan?.Leader != null && hero.Clan.Leader != hero && playerManager.Contains(hero);

    public void Initialize(Dictionary<string, ClanFinanceSettings> settings)
    {
        disconnectedHeroes.Clear();
        Settings.Clear();
        if (settings == null) return;

        foreach (var pair in settings)
        {
            Settings[pair.Key] = pair.Value;
        }
    }

    public void UpdateDisconnectedHeroes(string[] heroIds) =>
        disconnectedHeroes = new HashSet<string>(heroIds ?? Array.Empty<string>());

    public bool CanChangeGold(Hero hero)
    {
        if (hero == null) return false;

        // Calculate gold change for AI heroes normally
        if (!hero.IsPlayerHero()) return true;

        // Don't tick gold change for disconnected players based on config
        if (!ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers)
        {
            bool disconnected = ModInformation.IsServer
                ? playerManager.IsOwnerOfHeroDisconnected(hero)
                : objectManager.TryGetIdWithLogging(hero, out var heroId) && disconnectedHeroes.Contains(heroId);
            if (disconnected) return false;
        }

        // Don't tick gold change when the hero is in a settlement based on config
        if (hero.CurrentSettlement != null
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInSettlements) return false;

        var mapEvent = hero.PartyBelongedTo?.MapEvent;

        // Hero not in a map event, calculate gold change normally
        if (mapEvent == null) return true;

        // Gold change is disabled in battles, skip this tick
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInBattles == GoldFoodChangeMode.Disabled) return false;

        // Use gold food consumption window to determine if the gold change should be calculated based on config.
        // This way players only have a gold change at most once during a map event when set to OneDayMax.
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeInBattles == GoldFoodChangeMode.OneDayMax
            && !InteractionPatches.IsWithinGoldFoodConsumptionWindow(mapEvent)) return false;

        return true;
    }

    public ClanFinanceSettings GetSettings(Hero member)
    {
        if (!IsNonLeaderMember(member)) return null;
        if (!objectManager.TryGetIdWithLogging(member, out var memberId)) return null;
        if (!Settings.TryGetValue(memberId, out var settings)) return null;

        return settings.ClanId == member.Clan.StringId && settings.LeaderId == member.Clan.Leader.StringId
            ? settings : null;
    }

    public bool TryChange(Hero actor, Hero member, Clan clan, int amount, bool isTribute)
    {
        // Guard against invalid changes
        if (amount < 0 || !IsNonLeaderMember(member) || member.Clan != clan || actor?.Clan != clan ||
            (isTribute ? actor != member : actor != clan.Leader)) return false;

        // Non-leader members can send one time tribute payments to the clan leader
        if (isTribute)
        {
            amount = Transfer(member, clan.Leader, amount, disableNotification: false);

            if (!PlayerManager.TryGetControlledObjectInfo(clan.Leader, out var controller)) return false;
            if (!playerManager.TryGetPeer(controller.ObjectControllerId, out var peer)) return false;

            if (amount > 0)
            {
                var message = new SendInformationMessage(GameTexts.FindText("str_coop_clan_tribute_received")
                    .SetTextVariable("HERO", member.Name).SetTextVariable("AMOUNT", amount).ToString());

                network.Send(peer, message);
            }

            return true;
        }

        if (!objectManager.TryGetIdWithLogging(member, out var memberId)) return false;

        // Clan leader changed a payment for a player, update these finance settings
        var settings = new ClanFinanceSettings(clan.StringId, clan.Leader.StringId, amount);
        Apply(memberId, settings);

        messageBroker.Publish(this, new ClanFinanceChanged(memberId, settings));
        messageBroker.Publish(this, new ClanManagementChanged(clan, ClanManagementRefresh.Finances));

        return true;
    }

    public void Apply(string memberId, ClanFinanceSettings settings)
    {
        if (settings == null) Settings.Remove(memberId);
        else Settings[memberId] = settings;
    }

    public void Clear(Hero hero)
    {
        if (!objectManager.TryGetIdWithLogging(hero, out var memberId)) return;
        if (!Settings.Remove(memberId)) return;

        messageBroker.Publish(this, new ClanFinanceChanged(memberId, null));
    }

    private IEnumerable<Hero> GetNonLeaderMembers(Clan clan) => clan.Heroes.Where(hero =>
        hero.IsAlive && hero.Clan == clan && IsNonLeaderMember(hero));

    public void AddPaymentExpenses(Clan clan, ref ExplainedNumber change)
    {
        // Don't add expenses if clan leader is occupied
        if (!CanChangeGold(clan.Leader)) return;

        foreach (var member in GetNonLeaderMembers(clan))
        {
            // Don't send gold if non-leader member is occupied
            if (!CanChangeGold(member)) continue;

            // Retrieve current settings and add expense to explained change
            int amount = GetSettings(member)?.DailyPayment ?? 0;
            if (amount != 0)
            {
                change.Add(-amount, GameTexts.FindText("str_coop_clan_payment_to").SetTextVariable("HERO", member.Name));
            }
        }
    }

    public ExplainedNumber CalculateMemberGoldChange(Hero member, bool includeDescriptions = true, bool applyWithdrawals = false)
    {
        // Don't calculate gold change if member is a clan leader or if occupied
        var change = new ExplainedNumber(0, includeDescriptions);
        if (!IsNonLeaderMember(member) || !CanChangeGold(member)) return change;

        // Only add payment from clan leader if clan leader's gold change is available
        if (CanChangeGold(member.Clan.Leader))
        {
            int payment = GetSettings(member)?.DailyPayment ?? 0;
            if (applyWithdrawals)
            {
                payment = Transfer(member.Clan.Leader, member, payment);
            } 

            change.Add(payment, GameTexts.FindText("str_coop_clan_payment_from_leader"));
        }

        // Add regular main party wage
        var party = member.PartyBelongedTo;
        if (party?.LeaderHero == member)
        {
            int wage = ((DefaultClanFinanceModel)Campaign.Current.Models.ClanFinanceModel).CalculatePartyWage(party, Math.Max(0, member.Gold), applyWithdrawals);
            if (applyWithdrawals)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, member, -wage, true);
            } 

            change.Add(-wage, DefaultClanFinanceModel._mainPartywageText);
        }

        return change;
    }

    public int ApplyDailyTransfers(Clan clan)
    {
        int originalGold = clan.Leader.Gold;

        foreach (var member in GetNonLeaderMembers(clan))
        {
            int goldChange = MathF.Round(CalculateMemberGoldChange(member, includeDescriptions: false, applyWithdrawals: true).ResultNumber);
            if (goldChange != 0)
            {
                messageBroker.Publish(this, new NotifyDailyGoldChange(member, goldChange));
            }
        }

        return clan.Leader.Gold - originalGold;
    }

    private int Transfer(Hero payer, Hero recipient, int amount, bool disableNotification = true)
    {
        amount = Math.Max(0, Math.Min(amount, Math.Min(payer.Gold, int.MaxValue - recipient.Gold)));
        if (amount > 0)
        {
            GiveGoldAction.ApplyBetweenCharacters(payer, recipient, amount, disableNotification);
        }

        return amount;
    }
}
