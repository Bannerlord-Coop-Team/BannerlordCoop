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

    /// <summary>Server: applies the picked start and ticked modifiers to a newly joined hero. Returns false when the
    /// start can no longer be applied, such as a king start for a kingdom another player took first, and the hero
    /// keeps the default start.</summary>
    bool ApplySelections(Hero hero, AdvancedStartSelection[] selections);
}

/// <inheritdoc cref="IAdvancedStartInterface"/>
internal class AdvancedStartInterface : IAdvancedStartInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<AdvancedStartInterface>();

    internal const string WorldStartedLockId = "str_coop_advanced_start_world_started";
    internal const string NoKingdomLockId = "str_coop_advanced_start_no_kingdom";
    internal const string KingdomMissingLockId = "str_coop_advanced_start_kingdom_missing";
    internal const string KingdomRuledLockId = "str_coop_advanced_start_kingdom_ruled";

    private readonly IModConfig modConfig;
    private readonly ILoadingInterface loadingInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly IPlayerManager playerManager;
    private readonly IAdvancedStartType[] startTypes;
    private readonly IAdvancedStartModifier[] modifiers;

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
        IPlayerManager playerManager,
        IEnumerable<IAdvancedStartType> startTypes,
        IEnumerable<IAdvancedStartModifier> modifiers)
    {
        this.modConfig = modConfig;
        this.loadingInterface = loadingInterface;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.playerManager = playerManager;
        this.startTypes = startTypes.ToArray();
        this.modifiers = modifiers.ToArray();
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

        return CreatePolicy(startTypes, modifiers, config, HasWorldStarted(), playerRuledKingdomIds.ToArray(), joinableKingdomIds.ToArray());
    }

    /// <summary>The policy offering the default start first, then each start type and modifier the host's config
    /// doesn't switch off.</summary>
    internal static AdvancedStartPolicy CreatePolicy(IEnumerable<IAdvancedStartType> startTypes,
        IEnumerable<IAdvancedStartModifier> modifiers, AdvancedStartConfigData config,
        bool worldStarted, string[] playerRuledKingdomIds, string[] joinableKingdomIds)
    {
        var offered = startTypes.Where(startType => startType.IsOffered(config)).ToArray();
        string[] IdsWhere(Func<IAdvancedStartType, bool> trait) => offered.Where(trait).Select(startType => startType.Id).ToArray();

        return new AdvancedStartPolicy(
            startTypes: new[] { StartOptionIds.StartTypeDefaultId }.Concat(offered.Select(startType => startType.Id)).ToArray(),
            worldStartTypes: IdsWhere(startType => startType.ChangesWorld),
            kingdomStartTypes: IdsWhere(startType => startType.NeedsKingdom),
            modifierIds: modifiers.Where(modifier => modifier.IsOffered(config)).Select(modifier => modifier.OptionId).ToArray(),
            defaultStartType: config?.DefaultStartType,
            worldStarted: worldStarted,
            playerRuledKingdomIds: playerRuledKingdomIds,
            joinableKingdomIds: joinableKingdomIds);
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

    /// <summary>Whether the policy offers a modifier, or any start other than the default that can be picked right now.</summary>
    internal static bool OffersPickableStart(AdvancedStartPolicy policy)
    {
        if (policy?.StartTypes == null) return false;
        if (policy.ModifierIds?.Length > 0) return true;

        return policy.StartTypes.Any(startType => startType != StartOptionIds.StartTypeDefaultId &&
                                                  GetStartTypeLockId(policy, startType, policy.JoinableKingdomIds) == null);
    }

    internal static bool IsWorldLocked(AdvancedStartPolicy policy, string startType) =>
        policy.WorldStarted && policy.WorldStartTypes?.Contains(startType) == true;

    /// <summary>The string id of why a start type can't be picked, or null when it can. A kingdom start needs one of
    /// <paramref name="kingdomIds"/> to be able to take it.</summary>
    internal static string GetStartTypeLockId(AdvancedStartPolicy policy, string startType, IEnumerable<string> kingdomIds)
    {
        if (IsWorldLocked(policy, startType)) return WorldStartedLockId;

        if (policy.KingdomStartTypes?.Contains(startType) == true &&
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

    /// <summary>The start type the window opens on: the host's default when it can be picked, otherwise vanilla's.
    /// The host's default matches ignoring case, since the config keys spell ids like fleetadmiral as fleetAdmiral.</summary>
    internal static string GetDefaultStartType(AdvancedStartPolicy policy)
    {
        string hostDefault = policy.StartTypes.FirstOrDefault(startType =>
            string.Equals(startType, policy.DefaultStartType, StringComparison.OrdinalIgnoreCase));
        if (hostDefault != null && !IsWorldLocked(policy, hostDefault)) return hostDefault;

        return StartOptionIds.StartTypeDefaultId;
    }

    public bool TryShowStartOptions(Action<AdvancedStartSelection[]> onJoin, Action onLeave)
    {
        if (!WillShowStartOptions()) return false;

        var policy = ServerPolicy;
        var vanillaOptions = AdvancedStartOptionsManager.CreateCampaignStartOptions();
        var options = new StartOptions();
        // Each option shows in its own vanilla section, and a section with nothing shown is left out.
        if (vanillaOptions.GetOption(StartOptionIds.StartTypeId) is ListAdvancedStartOption startTypeOption)
            options.Add(CopyStartTypeOption(startTypeOption, policy));
        if (vanillaOptions.GetOption(StartOptionIds.KingdomIdKey) is ListAdvancedStartOption kingdomOption)
            options.Add(CopyKingdomOption(kingdomOption, policy));
        foreach (var modifierId in policy.ModifierIds ?? Array.Empty<string>())
        {
            var modifierOption = vanillaOptions.GetOption(modifierId);
            if (modifierOption != null) options.Add(modifierOption);
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

    // The start type option, keeping only the start types the policy offers.
    private ListAdvancedStartOption CopyStartTypeOption(ListAdvancedStartOption option, AdvancedStartPolicy policy)
    {
        var items = option.GetItems()
            .Where(item => policy.StartTypes.Contains(item.Identifier))
            .Select(item => (item.Identifier, (ListAdvancedStartOption.ListItemCondition)((StartOptions options, out TextObject disabledText) =>
                IsLocked(GetStartTypeLockId(ServerPolicy ?? policy, item.Identifier, ListedKingdomIds(options)), out disabledText))))
            .ToList();

        return new ListAdvancedStartOption(option.StringId, option.CategoryId, items, option.GetIsHidden, GetDefaultStartType(policy));
    }

    // The kingdom option. Vanilla's defaults to no kingdom, which a kingdom start can't use.
    private ListAdvancedStartOption CopyKingdomOption(ListAdvancedStartOption option, AdvancedStartPolicy policy)
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

        return new ListAdvancedStartOption(option.StringId, option.CategoryId, items, option.GetIsHidden, defaultValue);
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
        bool applied = false;
        // Patches stay live so every change to the hero, party and factions replicates as its own message.
        GameThread.RunSafe(() =>
        {
            // Picked against a fresh policy because the world can move on, or another player take the kingdom, after the window opened.
            var pick = PickStart(CreatePolicy(), startTypes, modifiers, selections);
            if (!pick.StartApplies)
                Logger.Warning("Start type {StartType} with kingdom {KingdomId} can't be applied to {Hero}; keeping the default start",
                    pick.StartTypeId, pick.KingdomId, hero.StringId);

            if (pick.StartType == null && pick.Modifiers.Length == 0)
            {
                applied = pick.StartApplies;
                return;
            }

            var party = hero.PartyBelongedTo;
            try
            {
                using (new MainHeroSubstitutionScope(hero, party))
                {
                    pick.StartType?.Run(hero, pick.KingdomId);
                    // Before the modifiers, so one that throws doesn't report a start that did apply as failed.
                    applied = pick.StartApplies;
                    foreach (var modifier in pick.Modifiers)
                        modifier.Run(hero);
                    party.MemberRoster.UpdateVersion();
                }
            }
            finally
            {
                // What the start already changed stays even if it threw partway, so every client still gets it.
                ReplicateAppliedStart(hero);
            }
        }, blocking: true, context: nameof(ApplySelections));

        return applied;
    }

    /// <summary>The start and ticked modifiers to give a joining player. A start the policy no longer allows falls back
    /// to the default start, reported as not applied, and the ticked modifiers still apply.</summary>
    internal static (IAdvancedStartType StartType, string StartTypeId, string KingdomId, IAdvancedStartModifier[] Modifiers, bool StartApplies)
        PickStart(AdvancedStartPolicy policy, IEnumerable<IAdvancedStartType> startTypes,
            IEnumerable<IAdvancedStartModifier> modifiers, AdvancedStartSelection[] selections)
    {
        selections = selections ?? Array.Empty<AdvancedStartSelection>();
        string startTypeId = selections.FirstOrDefault(selection => selection?.Id == StartOptionIds.StartTypeId)?.Value;
        string kingdomId = selections.FirstOrDefault(selection => selection?.Id == StartOptionIds.KingdomIdKey)?.Value;

        // A modifier's value is its ticked state as text.
        var offeredModifierIds = policy?.ModifierIds ?? Array.Empty<string>();
        var tickedModifiers = modifiers
            .Where(modifier => offeredModifierIds.Contains(modifier.OptionId) &&
                               selections.Any(selection => selection?.Id == modifier.OptionId &&
                                                           bool.TryParse(selection.Value, out bool ticked) && ticked))
            .ToArray();

        if (startTypeId == null || startTypeId == StartOptionIds.StartTypeDefaultId)
            return (null, startTypeId, kingdomId, tickedModifiers, true);

        var startType = startTypes.FirstOrDefault(candidate => candidate.Id == startTypeId);
        bool startApplies = startType != null && CanApply(policy, startType, kingdomId);
        return (startApplies ? startType : null, startTypeId, kingdomId, tickedModifiers, startApplies);
    }

    // Whether the pick is still offered and, for a kingdom start, its kingdom can still take the player.
    private static bool CanApply(AdvancedStartPolicy policy, IAdvancedStartType startType, string kingdomId)
    {
        if (policy == null || !policy.StartTypes.Contains(startType.Id)) return false;
        if (GetStartTypeLockId(policy, startType.Id, policy.JoinableKingdomIds) != null) return false;
        if (!startType.CanRunInWorld()) return false;

        return !startType.NeedsKingdom || GetKingdomLockId(policy, startType.Id, kingdomId) == null;
    }

    // Sends what the start changed that doesn't replicate by itself.
    private void ReplicateAppliedStart(Hero hero)
    {
        var party = hero.PartyBelongedTo;

        // The start made the player a ruler, so players still picking a start can no longer take this kingdom.
        if (hero.Clan.Kingdom?.RulingClan == hero.Clan)
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
