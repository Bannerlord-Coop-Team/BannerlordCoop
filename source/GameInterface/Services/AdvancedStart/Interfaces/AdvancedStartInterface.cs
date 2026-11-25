using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart.Data;
using GameInterface.Services.AdvancedStart.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Interfaces;
using SandBox.AdvancedStartOptions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using StartOptionIds = TaleWorlds.CampaignSystem.AdvancedStartOptions.DefaultAdvancedStartOptions;
using StartOptions = SandBox.AdvancedStartOptions.AdvancedStartOptions;

namespace GameInterface.Services.AdvancedStart.Interfaces;

/// <summary>
/// Offers vanilla's advanced starting options to a joining player after character creation and applies the
/// picked start to their hero on the server, since the client creates its character in a throwaway campaign.
/// </summary>
public interface IAdvancedStartInterface : IGameAbstraction
{
    /// <summary>Server, game thread: what a joining player may pick, or null when the host turned the window off.</summary>
    AdvancedStartPolicy CreatePolicy();

    /// <summary>Server, game thread: publishes <see cref="AdvancedStartPolicyChanged"/> the first time the campaign
    /// clock moves, so players still picking a start lock the world starts. Does nothing once it has moved.</summary>
    void WatchForWorldStart();

    /// <summary>Client: the joining server's latest policy.</summary>
    AdvancedStartPolicy ServerPolicy { get; }

    /// <summary>Client: stores the server's latest policy and re-checks the window if it's open, locking what is
    /// no longer offered and telling the player if their pick was one of them.</summary>
    void UpdateServerPolicy(AdvancedStartPolicy policy);

    /// <summary>Client: keeps the window shut for this join. A debug build calls it after skipping character
    /// creation for an automated join, which would otherwise stop at the window waiting for a pick.</summary>
    void SkipStartOptions();

    /// <summary>Client: whether the starting options window will open after character creation.</summary>
    bool WillShowStartOptions();

    /// <summary>Client: opens the starting options window filtered by <see cref="ServerPolicy"/>.
    /// Returns false when there is nothing to pick. Otherwise <paramref name="onJoin"/> gets the picks
    /// on confirm, and <paramref name="onLeave"/> runs if the player confirms going back to the main menu.</summary>
    bool TryShowStartOptions(Action<AdvancedStartSelection[]> onJoin, Action onLeave);

    /// <summary>Client: closes the window without running its callbacks and forgets this join's policy.</summary>
    void EndJoin();

    /// <summary>Server: applies the picked start to a newly joined hero. Returns false when the pick
    /// can no longer be applied, such as a king start for a kingdom another player took first,
    /// and the hero keeps the default start.</summary>
    bool ApplySelections(Hero hero, AdvancedStartSelection[] selections);
}

/// <inheritdoc cref="IAdvancedStartInterface"/>
internal class AdvancedStartInterface : IAdvancedStartInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<AdvancedStartInterface>();

    // Start types that hand out fiefs, workshops, kingdoms or wars, so they're only offered before the campaign is first resumed.
    private static readonly string[] WorldStartTypes =
    {
        StartOptionIds.StartTypeTraderId,
        StartOptionIds.StartTypeMercenaryId,
        StartOptionIds.StartTypeVassalId,
        StartOptionIds.StartTypeKingId,
    };

    // Start types that join the kingdom picked in the KingdomId option.
    private static readonly string[] KingdomStartTypes =
    {
        StartOptionIds.StartTypeMercenaryId,
        StartOptionIds.StartTypeVassalId,
        StartOptionIds.StartTypeKingId,
    };

    // The window's sections in order and the vanilla options each one shows. A section with nothing offered
    // doesn't show, so War Sails and co-op options can each get their own section here.
    private static readonly (string CategoryId, string[] OptionIds)[] Categories =
    {
        (StartOptionIds.ScenariosCategoryId, new[] { StartOptionIds.StartTypeId, StartOptionIds.KingdomIdKey }),
    };

    internal const string WorldStartedLockId = "str_coop_advanced_start_world_started";
    internal const string NoKingdomLockId = "str_coop_advanced_start_no_kingdom";
    internal const string KingdomMissingLockId = "str_coop_advanced_start_kingdom_missing";
    internal const string KingdomRuledLockId = "str_coop_advanced_start_kingdom_ruled";

    private readonly IModConfig modConfig;
    private readonly ILoadingInterface loadingInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly IPlayerManager playerManager;

    private CoopStartingOptionsView startOptionsView;
    // Client: set when a debug build skipped character creation for this join.
    private bool startOptionsSkipped;
    // Server: the campaign whose clock is being watched for its first move.
    private Campaign worldStartWatchCampaign;

    public AdvancedStartInterface(
        IModConfig modConfig,
        ILoadingInterface loadingInterface,
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IPlayerManager playerManager)
    {
        this.modConfig = modConfig;
        this.loadingInterface = loadingInterface;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.playerManager = playerManager;
    }

    public AdvancedStartPolicy CreatePolicy()
    {
        var config = modConfig.Data.AdvancedStart;
        if (config?.Enabled == false) return null;

        // Through vanilla's ResolveKingdom, as the start itself does, so a world scenario's merged kingdoms
        // (an imperial pick joins the united empire) count and eliminated ones don't. A campaign started without
        // vanilla's start options has no scenario data, which ResolveKingdom and the kingdom starts can't read.
        var joinableKingdomIds = new List<string>();
        var playerRuledKingdomIds = new List<string>();
        if (Campaign.Current.AdvancedStartData != null)
        {
            foreach (var kingdom in Campaign.Current.Kingdoms)
            {
                var resolved = CampaignAdvancedStartingPlayerOptionsCampaignBehavior.ResolveKingdom(kingdom.StringId);
                if (resolved == null || resolved.IsEliminated) continue;

                joinableKingdomIds.Add(kingdom.StringId);
                if (resolved.RulingClan != null && playerManager.Contains(resolved.RulingClan))
                    playerRuledKingdomIds.Add(kingdom.StringId);
            }
        }

        return new AdvancedStartPolicy(GetOfferedStartTypes(config), config?.DefaultStartType, HasWorldStarted(),
            playerRuledKingdomIds.ToArray(), joinableKingdomIds.ToArray());
    }

    /// <summary>The start types the host's config offers: the default start always, the others unless switched off.</summary>
    internal static string[] GetOfferedStartTypes(AdvancedStartConfigData config)
    {
        var startTypes = config?.StartTypes;
        var offered = new (string Id, bool? Enabled)[]
        {
            (StartOptionIds.StartTypeDefaultId, true),
            (StartOptionIds.StartTypeOutlawId, startTypes?.Outlaw),
            (StartOptionIds.StartTypeBeggarId, startTypes?.Beggar),
            (StartOptionIds.StartTypeTraderId, startTypes?.Trader),
            (StartOptionIds.StartTypeMercenaryId, startTypes?.Mercenary),
            (StartOptionIds.StartTypeVassalId, startTypes?.Vassal),
            (StartOptionIds.StartTypeKingId, startTypes?.King),
        };

        return offered.Where(startType => startType.Enabled != false).Select(startType => startType.Id).ToArray();
    }

    // The campaign clock only moves once someone resumes it, so an unmoved clock means no one has played yet.
    private static bool HasWorldStarted() =>
        CampaignTime.Now > Campaign.Current.Models.CampaignTimeModel.CampaignStartTime;

    public void WatchForWorldStart()
    {
        if (worldStartWatchCampaign == Campaign.Current || HasWorldStarted()) return;
        worldStartWatchCampaign = Campaign.Current;

        CampaignEvents.TickEvent.AddNonSerializedListener(this, _ =>
        {
            if (!HasWorldStarted()) return;

            CampaignEvents.TickEvent.ClearListeners(this);
            messageBroker.Publish(this, new AdvancedStartPolicyChanged());
        });
    }

    public AdvancedStartPolicy ServerPolicy { get; private set; }

    public void UpdateServerPolicy(AdvancedStartPolicy policy)
    {
        ServerPolicy = policy;
        startOptionsView?.Refresh();
    }

    public void SkipStartOptions()
    {
        startOptionsSkipped = true;
    }

    public bool WillShowStartOptions() => !startOptionsSkipped && OffersPickableStart(ServerPolicy);

    /// <summary>Whether the policy offers any start other than the default that can be picked right now.</summary>
    internal static bool OffersPickableStart(AdvancedStartPolicy policy)
    {
        if (policy?.StartTypes == null) return false;

        return policy.StartTypes.Any(startType => startType != StartOptionIds.StartTypeDefaultId &&
                                                  GetStartTypeLockId(policy, startType, policy.JoinableKingdomIds) == null);
    }

    internal static bool IsWorldLocked(AdvancedStartPolicy policy, string startType) =>
        policy.WorldStarted && WorldStartTypes.Contains(startType);

    /// <summary>The string id of why a start type can't be picked, or null when it can. A kingdom start needs one of
    /// <paramref name="kingdomIds"/> to be able to take it.</summary>
    internal static string GetStartTypeLockId(AdvancedStartPolicy policy, string startType, IEnumerable<string> kingdomIds)
    {
        if (IsWorldLocked(policy, startType)) return WorldStartedLockId;

        if (KingdomStartTypes.Contains(startType) &&
            !(kingdomIds ?? Array.Empty<string>()).Any(kingdomId => GetKingdomLockId(policy, startType, kingdomId) == null))
            return NoKingdomLockId;

        return null;
    }

    /// <summary>The string id of why a kingdom start can't join this kingdom, or null when it can.</summary>
    internal static string GetKingdomLockId(AdvancedStartPolicy policy, string startType, string kingdomId)
    {
        if (policy.JoinableKingdomIds?.Contains(kingdomId) != true) return KingdomMissingLockId;
        if (startType == StartOptionIds.StartTypeKingId && policy.PlayerRuledKingdomIds?.Contains(kingdomId) == true)
            return KingdomRuledLockId;
        return null;
    }

    /// <summary>The start type the window opens on: the host's default when it can be picked, otherwise vanilla's.</summary>
    internal static string GetDefaultStartType(AdvancedStartPolicy policy)
    {
        bool hostDefaultOffered = policy.StartTypes.Contains(policy.DefaultStartType) &&
                                  !IsWorldLocked(policy, policy.DefaultStartType);
        if (hostDefaultOffered) return policy.DefaultStartType;

        return StartOptionIds.StartTypeDefaultId;
    }

    public bool TryShowStartOptions(Action<AdvancedStartSelection[]> onJoin, Action onLeave)
    {
        if (!WillShowStartOptions()) return false;

        var policy = ServerPolicy;
        var vanillaOptions = AdvancedStartOptionsManager.CreateCampaignStartOptions();
        var options = new StartOptions();
        foreach (var (categoryId, optionIds) in Categories)
        {
            foreach (var optionId in optionIds)
            {
                if (!(vanillaOptions.GetOption(optionId) is ListAdvancedStartOption option)) continue;

                options.Add(optionId == StartOptionIds.StartTypeId
                    ? CopyStartTypeOption(option, categoryId, policy)
                    : CopyKingdomOption(option, categoryId, policy));
            }
        }

        if (options.GetOption(StartOptionIds.StartTypeId) == null)
        {
            Logger.Error("Vanilla starting options have no {OptionId} option", StartOptionIds.StartTypeId);
            return false;
        }

        loadingInterface.HideLoadingScreen();

        var view = new CoopStartingOptionsView(
            options,
            picked =>
            {
                startOptionsView = null;
                // Every value, not just changed ones, so the server doesn't need vanilla's defaults to read the pick.
                onJoin(picked.GetAllOptions()
                    .Select(option => new AdvancedStartSelection(option.StringId, option.Value.GetData()?.ToString()))
                    .ToArray());
            },
            () =>
            {
                startOptionsView = null;
                onLeave();
            });
        ScreenManager.AddGlobalLayer(view, isFocusable: true);
        startOptionsView = view;
        return true;
    }

    // The item locks below read the latest policy, so a refresh picks up a kingdom taken or the clock started after the
    // window opened. Vanilla's own item locks are left out: the host decides what's offered, not the joining player's
    // single-player playthroughs, and its kingdom locks read world scenario options this window doesn't have.

    // The start type option in the given section, keeping only the start types the policy offers.
    private ListAdvancedStartOption CopyStartTypeOption(ListAdvancedStartOption option, string categoryId, AdvancedStartPolicy policy)
    {
        var items = option.GetItems()
            .Where(item => policy.StartTypes.Contains(item.Identifier))
            .Select(item => (item.Identifier, (ListAdvancedStartOption.ListItemCondition)((StartOptions options, out TextObject disabledText) =>
                IsLocked(GetStartTypeLockId(ServerPolicy ?? policy, item.Identifier, ListedKingdomIds(options)), out disabledText))))
            .ToList();

        return new ListAdvancedStartOption(option.StringId, categoryId, items, option.GetIsHidden, GetDefaultStartType(policy));
    }

    // The kingdom option in the given section. Vanilla's defaults to no kingdom, which a kingdom start can't use.
    private ListAdvancedStartOption CopyKingdomOption(ListAdvancedStartOption option, string categoryId, AdvancedStartPolicy policy)
    {
        var items = option.GetItems()
            .Select(item => (item.Identifier, (ListAdvancedStartOption.ListItemCondition)((StartOptions options, out TextObject disabledText) =>
            {
                string startType = options.GetOption(StartOptionIds.StartTypeId)?.GetValue<string>();
                return IsLocked(GetKingdomLockId(ServerPolicy ?? policy, startType, item.Identifier), out disabledText);
            })))
            .ToList();

        string defaultValue = option.GetDefaultValue<string>();
        if (string.IsNullOrEmpty(defaultValue) && items.Count > 0) defaultValue = items[0].Identifier;

        return new ListAdvancedStartOption(option.StringId, categoryId, items, option.GetIsHidden, defaultValue);
    }

    private static IEnumerable<string> ListedKingdomIds(StartOptions options) =>
        options.GetOption<ListAdvancedStartOption>(StartOptionIds.KingdomIdKey)?.GetItems().Select(item => item.Identifier)
        ?? Enumerable.Empty<string>();

    private static bool IsLocked(string lockId, out TextObject disabledText)
    {
        disabledText = lockId == null ? null : GameTexts.FindText(lockId);
        return lockId != null;
    }

    public void EndJoin()
    {
        startOptionsView?.Remove();
        startOptionsView = null;
        ServerPolicy = null;
        startOptionsSkipped = false;
    }

    public bool ApplySelections(Hero hero, AdvancedStartSelection[] selections)
    {
        string startType = selections.FirstOrDefault(selection => selection?.Id == StartOptionIds.StartTypeId)?.Value;
        if (startType == null || startType == StartOptionIds.StartTypeDefaultId) return true;
        string kingdomId = selections.FirstOrDefault(selection => selection?.Id == StartOptionIds.KingdomIdKey)?.Value;

        bool applied = false;
        // Patches stay live so every change to the hero, party and factions replicates as its own message.
        GameThread.RunSafe(() =>
        {
            // Checked again here because the world can move on, or another player take the kingdom, after the window opened.
            if (!CanApply(startType, kingdomId))
            {
                Logger.Warning("Start type {StartType} with kingdom {KingdomId} can't be applied to {Hero}; keeping the default start",
                    startType, kingdomId, hero.StringId);
                return;
            }

            var behavior = Campaign.Current.GetCampaignBehavior<CampaignAdvancedStartingPlayerOptionsCampaignBehavior>();
            if (behavior == null)
            {
                Logger.Error("No {Behavior} to apply start type {StartType}",
                    nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior), startType);
                return;
            }

            try
            {
                RunStart(behavior, hero, startType, kingdomId);
                applied = true;
            }
            finally
            {
                // What the start already changed stays even if it threw partway, so every client still gets it.
                ReplicateAppliedStart(hero, startType);
            }
        }, blocking: true, context: nameof(ApplySelections));

        return applied;
    }

    // Server, game thread: whether the pick is still offered and, for a kingdom start, its kingdom can still take the player.
    private bool CanApply(string startType, string kingdomId)
    {
        var policy = CreatePolicy();
        if (policy == null || !policy.StartTypes.Contains(startType)) return false;
        if (GetStartTypeLockId(policy, startType, policy.JoinableKingdomIds) != null) return false;

        // Vanilla's trader start picks its caravan leader from the companions it just handed out, and throws
        // when earlier joiners' starts left no free wanderer to hand out.
        if (startType == StartOptionIds.StartTypeTraderId &&
            !Hero.AllAliveHeroes.Any(candidate => candidate.IsWanderer && candidate.IsActive && candidate.CompanionOf == null))
            return false;

        return !KingdomStartTypes.Contains(startType) || GetKingdomLockId(policy, startType, kingdomId) == null;
    }

    // Runs vanilla's start with the joining hero standing in as the main hero.
    private static void RunStart(CampaignAdvancedStartingPlayerOptionsCampaignBehavior behavior, Hero hero, string startType, string kingdomId)
    {
        var party = hero.PartyBelongedTo;
        var clan = hero.Clan;
        var oldKingdom = clan.Kingdom;
        using (new MainHeroSubstitutionScope(hero, party))
        {
            switch (startType)
            {
                case StartOptionIds.StartTypeOutlawId:
                    behavior.StartGameAsOutlaw();
                    break;
                case StartOptionIds.StartTypeBeggarId:
                    behavior.StartGameAsBeggar();
                    break;
                case StartOptionIds.StartTypeTraderId:
                    behavior.StartGameAsTrader();
                    break;
                case StartOptionIds.StartTypeMercenaryId:
                    behavior.StartGameAsMercenary(kingdomId);
                    break;
                case StartOptionIds.StartTypeVassalId:
                    behavior.StartGameAsVassal(kingdomId);
                    break;
                case StartOptionIds.StartTypeKingId:
                    behavior.StartGameAsRuler(kingdomId);
                    break;
            }

            // The kingdom starts set the clan's kingdom directly, which fires no kingdom change event, so other
            // clients' nameplates never pick up the clan's new banner colours. The event replicates to them.
            if (clan.Kingdom != oldKingdom)
            {
                var detail = startType == StartOptionIds.StartTypeMercenaryId
                    ? ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary
                    : ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom;
                CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, oldKingdom, clan.Kingdom, detail, showNotification: false);
            }

            party.MemberRoster.UpdateVersion();
        }
    }

    // Sends what the start changed that doesn't replicate by itself.
    private void ReplicateAppliedStart(Hero hero, string startType)
    {
        var party = hero.PartyBelongedTo;

        // Players still picking a start can no longer take this kingdom.
        if (startType == StartOptionIds.StartTypeKingId)
            messageBroker.Publish(this, new AdvancedStartPolicyChanged());

        // The start sets the party's position directly, which doesn't replicate, so force it like a teleport.
        if (behaviorSnapshot.TryCreate(party, out var movement))
        {
            movement.ForcePosition = true;
            movement.ResetMovementToHold = true;
            messageBroker.Publish(this, new UpdatePartyBehavior(ref movement));
        }

        // The party visual was built in hero setup, before the start changed the hero's equipment. Clients redraw
        // theirs as each equipment slot write reaches them.
        party.Party.SetVisualAsDirty();
    }
}
