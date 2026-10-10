using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Hideouts;

public interface ICoopHideoutMissionLauncher
{
    Mission OpenCoopHideoutMission(MissionInitializerRecord record, bool isDirectAssault);
}
