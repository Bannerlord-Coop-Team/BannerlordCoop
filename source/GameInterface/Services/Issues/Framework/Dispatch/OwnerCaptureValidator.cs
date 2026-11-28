using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class OwnerCaptureValidator : IOwnerCaptureValidator
{
    public bool WasCapturedLosingThisAssault(Hero owner, MapEvent mapEvent)
    {
        var captor = owner?.IsPrisoner == true ? owner.PartyBelongedToAsPrisoner : null;

        return captor?.MapEvent == mapEvent &&
            captor.Side == BattleSideEnum.Defender &&
            mapEvent.DefeatedSide == BattleSideEnum.Attacker &&
            owner.CaptivityStartTime >= mapEvent.BattleStartTime;
    }
}
