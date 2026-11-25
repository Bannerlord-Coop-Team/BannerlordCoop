using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IOwnerCaptureValidator
{
    // Taken prisoner during this battle by the defenders who beat the assault, not merely held by someone who fought it
    bool WasCapturedLosingThisAssault(Hero owner, MapEvent mapEvent);
}
