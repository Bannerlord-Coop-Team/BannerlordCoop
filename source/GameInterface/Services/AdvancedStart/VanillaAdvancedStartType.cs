using GameInterface.Configuration;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using StartOptionIds = TaleWorlds.CampaignSystem.AdvancedStartOptions.DefaultAdvancedStartOptions;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// One start type a joining player can pick in the starting options window, listed under its item id in vanilla's
/// StartType option. Register an implementation to offer it; vanilla's default start needs none.
/// </summary>
public interface IAdvancedStartType
{
    /// <summary>The start type's item id in vanilla's StartType option.</summary>
    string Id { get; }

    /// <summary>Hands out fiefs, workshops, kingdoms or wars, so it's only offered before the campaign is first resumed.</summary>
    bool ChangesWorld { get; }

    /// <summary>Joins the kingdom picked in the KingdomId option.</summary>
    bool NeedsKingdom { get; }

    /// <summary>Whether the host's config offers it.</summary>
    bool IsOffered(AdvancedStartConfigData config);

    /// <summary>Server, game thread: whether the world can still run this start. The policy's locks are checked separately.</summary>
    bool CanRunInWorld();

    /// <summary>Server, game thread: gives <paramref name="hero"/> the start. It runs with the hero standing in as the
    /// main hero and patches live, so vanilla start code can run unchanged and every change replicates.</summary>
    void Run(Hero hero, string kingdomId);
}

/// <inheritdoc cref="IAdvancedStartType"/>
internal sealed class VanillaAdvancedStartType : IAdvancedStartType
{
    internal static readonly VanillaAdvancedStartType[] All =
    {
        new VanillaAdvancedStartType(StartOptionIds.StartTypeOutlawId, changesWorld: false, needsKingdom: false,
            startTypes => startTypes?.Outlaw, (behavior, _) => behavior.StartGameAsOutlaw()),
        new VanillaAdvancedStartType(StartOptionIds.StartTypeBeggarId, changesWorld: false, needsKingdom: false,
            startTypes => startTypes?.Beggar, (behavior, _) => behavior.StartGameAsBeggar()),
        new VanillaAdvancedStartType(StartOptionIds.StartTypeTraderId, changesWorld: true, needsKingdom: false,
            startTypes => startTypes?.Trader, (behavior, _) => behavior.StartGameAsTrader(),
            // Vanilla's trader start picks its caravan leader from the companions it just handed out, and throws
            // when earlier joiners' starts left no free wanderer to hand out.
            canRunInWorld: () => Hero.AllAliveHeroes.Any(candidate => candidate.IsWanderer && candidate.IsActive && candidate.CompanionOf == null)),
        new VanillaAdvancedStartType(StartOptionIds.StartTypeMercenaryId, changesWorld: true, needsKingdom: true,
            startTypes => startTypes?.Mercenary, (behavior, kingdomId) => behavior.StartGameAsMercenary(kingdomId)),
        new VanillaAdvancedStartType(StartOptionIds.StartTypeVassalId, changesWorld: true, needsKingdom: true,
            startTypes => startTypes?.Vassal, (behavior, kingdomId) => behavior.StartGameAsVassal(kingdomId)),
        new VanillaAdvancedStartType(StartOptionIds.StartTypeKingId, changesWorld: true, needsKingdom: true,
            startTypes => startTypes?.King, (behavior, kingdomId) => behavior.StartGameAsRuler(kingdomId)),
    };

    private readonly Func<AdvancedStartTypesConfigData, bool?> isEnabled;
    private readonly Action<CampaignAdvancedStartingPlayerOptionsCampaignBehavior, string> run;
    private readonly Func<bool> canRunInWorld;

    private VanillaAdvancedStartType(string id, bool changesWorld, bool needsKingdom,
        Func<AdvancedStartTypesConfigData, bool?> isEnabled,
        Action<CampaignAdvancedStartingPlayerOptionsCampaignBehavior, string> run,
        Func<bool> canRunInWorld = null)
    {
        Id = id;
        ChangesWorld = changesWorld;
        NeedsKingdom = needsKingdom;
        this.isEnabled = isEnabled;
        this.run = run;
        this.canRunInWorld = canRunInWorld;
    }

    public string Id { get; }
    public bool ChangesWorld { get; }
    public bool NeedsKingdom { get; }

    // A missing key offers it.
    public bool IsOffered(AdvancedStartConfigData config) => isEnabled(config?.StartTypes) != false;

    public bool CanRunInWorld() => canRunInWorld?.Invoke() ?? true;

    public void Run(Hero hero, string kingdomId)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<CampaignAdvancedStartingPlayerOptionsCampaignBehavior>();
        if (behavior == null)
            throw new InvalidOperationException($"No {nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior)} to run start type {Id}");

        var clan = hero.Clan;
        var oldKingdom = clan.Kingdom;
        run(behavior, kingdomId);

        // Vanilla's kingdom starts set the clan's kingdom directly, which fires no kingdom change event, so other
        // clients' nameplates never pick up the clan's new banner colours. The event replicates to them.
        if (clan.Kingdom != oldKingdom)
        {
            var detail = clan.IsUnderMercenaryService
                ? ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary
                : ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom;
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, oldKingdom, clan.Kingdom, detail, showNotification: false);
        }
    }
}
