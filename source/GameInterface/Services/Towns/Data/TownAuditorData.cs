using ProtoBuf;

namespace GameInterface.Services.Towns.Data;

/// <summary>
/// Get all the sync values of a town
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class TownAuditorData
{
    [ProtoMember(1)]
    public string TownStringId { get; }
    [ProtoMember(2)]
    public string Name { get; }
    [ProtoMember(3)]
    public string Governor { get; }
    [ProtoMember(4)]
    public string LastCapturedBy { get; }
    [ProtoMember(5)]
    public string Prosperity { get; }
    [ProtoMember(6)]
    public string Loyalty { get; }
    [ProtoMember(7)]
    public string Security { get; }
    [ProtoMember(8)]
    public string InRebelliousState { get; }
    [ProtoMember(9)]
    public string GarrisonAutoRecruitmentIsEnabled { get; }
    [ProtoMember(10)]
    public string FoodStocks { get; }
    [ProtoMember(11)]
    public string TradeTaxAccumulated { get; }
    [ProtoMember(12)]
    public string SoldItems { get; }


    public TownAuditorData(
        string townStringId,
        string name,
        string governor,
        string lastCapturedBy,
        string prosperity,
        string loyalty,
        string security,
        string inRebelliousState,
        string garrisonAutoRecruitmentIsEnabled,
        string foodStocks,
        string tradeTaxAccumulated,
        string soldItems)
    {
        TownStringId = townStringId;
        Name = name;
        Governor = governor;
        LastCapturedBy = lastCapturedBy;
        Prosperity = prosperity;
        Loyalty = loyalty;
        Security = security;
        InRebelliousState = inRebelliousState;
        GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
        FoodStocks = foodStocks;
        TradeTaxAccumulated = tradeTaxAccumulated;
        SoldItems = soldItems;

    }
}