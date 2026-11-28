using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart;
using NavalDLC.CampaignBehaviors;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using StartOptionIds = TaleWorlds.CampaignSystem.AdvancedStartOptions.DefaultAdvancedStartOptions;

namespace Coop.Naval.AdvancedStarts;

/// <summary>War Sails' start types, run through its own start behavior.</summary>
internal sealed class NavalAdvancedStartType : IAdvancedStartType
{
    internal static readonly NavalAdvancedStartType[] All =
    {
        // Joins the kingdom through ChangeKingdomAction, which fires the kingdom change event itself.
        new NavalAdvancedStartType(StartOptionIds.StartTypeFleetAdmiralId, needsKingdom: true,
            startTypes => startTypes?.FleetAdmiral, (behavior, kingdomId) => behavior.StartGameAsFleetAdmiral(kingdomId)),
        new NavalAdvancedStartType(StartOptionIds.StartTypeMerchantVenturerId, needsKingdom: false,
            startTypes => startTypes?.MerchantVenturer, (behavior, _) => behavior.StartGameAsMerchantVenturer(),
            // Like vanilla's trader, it picks its caravan leader from the companions it just handed out, and throws
            // when earlier joiners' starts left no free wanderer to hand out.
            canRunInWorld: () => Hero.AllAliveHeroes.Any(candidate => candidate.IsWanderer && candidate.IsActive && candidate.CompanionOf == null)),
    };

    private readonly Func<AdvancedStartTypesConfigData, bool?> isEnabled;
    private readonly Action<NavalAdvancedStartingPlayerOptionsCampaignBehavior, string> run;
    private readonly Func<bool> canRunInWorld;

    private NavalAdvancedStartType(string id, bool needsKingdom,
        Func<AdvancedStartTypesConfigData, bool?> isEnabled,
        Action<NavalAdvancedStartingPlayerOptionsCampaignBehavior, string> run,
        Func<bool> canRunInWorld = null)
    {
        Id = id;
        NeedsKingdom = needsKingdom;
        this.isEnabled = isEnabled;
        this.run = run;
        this.canRunInWorld = canRunInWorld;
    }

    public string Id { get; }

    // Both hand out a fief or workshop, ships and companions.
    public bool ChangesWorld => true;

    public bool NeedsKingdom { get; }

    // A missing key offers it.
    public bool IsOffered(AdvancedStartConfigData config) => isEnabled(config?.StartTypes) != false;

    public bool CanRunInWorld() => canRunInWorld?.Invoke() ?? true;

    public void Run(Hero hero, string kingdomId)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<NavalAdvancedStartingPlayerOptionsCampaignBehavior>();
        if (behavior == null)
            throw new InvalidOperationException($"No {nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior)} to run start type {Id}");

        run(behavior, kingdomId);
    }
}
