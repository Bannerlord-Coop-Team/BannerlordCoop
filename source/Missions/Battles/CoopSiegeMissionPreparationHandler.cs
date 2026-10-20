using GameInterface.Utils;
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
    private readonly uint _sceneSeed;

    /// <summary>Builds the vanilla walls-assault handler and derives the scene seed from the map event.</summary>
    public CoopSiegeMissionPreparationHandler(bool isSallyOut, float[] wallHitPointRatios, bool hasAnySiegeTower,
        string mapEventId)
        : base(isSallyOut, false, wallHitPointRatios, hasAnySiegeTower)
    {
        _sceneSeed = StableHash.FromString(mapEventId);
    }

    /// <summary>Runs vanilla scene setup (ArrangeDestructedMeshes draws from MBRandom) under the shared seed, then
    /// restores the campaign RNG even if setup throws.</summary>
    public override void OnBehaviorInitialize()
    {
        var campaignRandom = Game.Current.RandomGenerator;
        Game.Current.RandomGenerator = new MBFastRandom(_sceneSeed);
        try
        {
            base.OnBehaviorInitialize();
        }
        finally
        {
            Game.Current.RandomGenerator = campaignRandom;
        }
    }
}
