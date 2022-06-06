using System;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace Coop.Mod.Serializers.Custom
{
    //[Serializable]
    //public class SiegeLaneSerializer : ICustomSerializer
    //{
    //    private readonly SiegeLaneEnum id;
    //    private readonly bool isGate;
    //    private readonly bool isSiegeMachineApplicable;
    //    private readonly bool isBroken;

    //    public SiegeLaneSerializer(SiegeLane value)
    //    {
    //        id = value.Id;
    //        isGate = value.IsGate;
    //        isSiegeMachineApplicable = value.IsSiegeMachineApplicable;
    //        isBroken = value.IsBroken;
    //    }

    //    public object Deserialize()
    //    {
    //        SiegeLane newSiegeLane = new SiegeLane(id, isGate, isSiegeMachineApplicable);
    //        newSiegeLane.IsBroken = isBroken;
    //        return newSiegeLane;
    //    }

    //    public void ResolveReferenceGuids()
    //    {
    //        // No references
    //    }
    //}
}