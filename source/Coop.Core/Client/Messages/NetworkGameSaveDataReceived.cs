// Ignore Spelling: Guids

using Common.Messaging;
using GameInterface.Services.Caravans;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using ProtoBuf;

namespace Coop.Core.Client.Messages;

/// <summary>
/// Received Game save data from the network event
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkGameSaveDataReceived : IEvent
{
    [ProtoMember(1)]
    public byte[] GameSaveData { get; }
    [ProtoMember(2)]
    public string CampaignID { get; }
    [ProtoMember(3)]
    public CraftingPlayerData CraftingPlayerData { get; }
    [ProtoMember(4)]
    public WorkshopPlayerData WorkshopPlayerData { get; }
    [ProtoMember(5)]
    public CaravansPlayerData CaravansPlayerData { get; }

    public NetworkGameSaveDataReceived(
        byte[] gameSaveData,
        string campaignID,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData)
    {
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
    }
}
