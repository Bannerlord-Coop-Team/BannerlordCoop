using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal class BattleMissionInitializerResolver : IBattleMissionInitializerResolver
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleMissionInitializerResolver>();

    private readonly IReadOnlyList<IBattleMissionInitializer> initializers;

    public BattleMissionInitializerResolver(IEnumerable<IBattleMissionInitializer> initializers)
    {
        this.initializers = initializers
            .OrderByDescending(initializer => initializer.Priority)
            .ToArray();
    }

    public MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
    {
        foreach (var initializer in initializers)
        {
            if (initializer.CanHandle(mapEvent) == false)
                continue;

            Logger.Information("[BattleSync] Using {Initializer} for battle mission initializer", initializer.GetType().Name);
            return initializer.Create(mapEvent, randomTerrainSeed, atmosphereOnCampaign);
        }

        throw new InvalidOperationException("No battle mission initializer could handle the map event");
    }
}
