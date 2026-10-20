using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop <see cref="SiegeMissionPreparationHandler"/> that reseeds the campaign RNG from the map-event id while
/// vanilla sets up the scene, so every client pre-destroys the same random wall pieces and damage decals.
/// </summary>
public class CoopSiegeMissionPreparationHandler : SiegeMissionPreparationHandler
{
    /// <summary>Seed derived from the server-assigned map-event id, identical on every client.</summary>
    internal uint SceneSeed { get; }

    /// <summary>Builds the vanilla walls-assault handler and derives the scene seed from the map event.</summary>
    public CoopSiegeMissionPreparationHandler(bool isSallyOut, float[] wallHitPointRatios, bool hasAnySiegeTower,
        string mapEventId)
        : base(isSallyOut, false, wallHitPointRatios, hasAnySiegeTower)
    {
        SceneSeed = StableSeed(mapEventId);
    }

    /// <summary>Runs vanilla scene setup (ArrangeDestructedMeshes draws from MBRandom) under the shared seed, then
    /// restores the campaign RNG even if setup throws.</summary>
    public override void OnBehaviorInitialize()
    {
        var campaignRandom = Game.Current.RandomGenerator;
        Game.Current.RandomGenerator = new MBFastRandom(SceneSeed);
        try
        {
            base.OnBehaviorInitialize();
        }
        finally
        {
            Game.Current.RandomGenerator = campaignRandom;
        }
    }

    /// <summary>FNV-1a over the id's chars, stable across processes unlike string.GetHashCode; never zero.</summary>
    private static uint StableSeed(string id)
    {
        uint hash = 2166136261u;
        foreach (char c in id)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash == 0u ? 1u : hash;
    }
}
