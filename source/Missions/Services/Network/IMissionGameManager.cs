using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network
{
    public interface IMissionGameManager
    {
        Agent SpawnAgent(Vec3 startingPos, CharacterObject character);
    }
}