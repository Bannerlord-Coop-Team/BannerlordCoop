// Ignore Spelling: Guids

using Common.Messaging;
using GameInterface.Services.Alleys;
using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.Caravans;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
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
    [ProtoMember(6)]
    public AlleyPlayerData AlleyPlayerData { get; }
    [ProtoMember(7)]
    public InteractionsPlayerData InteractionsPlayerData { get; }
    [ProtoMember(8)]
    public TradePlayerData TradePlayerData { get; }
    [ProtoMember(9)]
    public AttachmentIdMap AttachmentIdMap { get; }
    [ProtoMember(10)]
    public ServerOptions ServerOptions { get; }

    public NetworkGameSaveDataReceived(
        byte[] gameSaveData,
        string campaignID,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData,
        AlleyPlayerData alleyPlayerData,
        InteractionsPlayerData interactionsPlayerData,
        TradePlayerData tradePlayerData,
        AttachmentIdMap attachmentIdMap,
        ServerOptions serverOptions)
    {
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
        AlleyPlayerData = alleyPlayerData;
        InteractionsPlayerData = interactionsPlayerData;
        TradePlayerData = tradePlayerData;
        AttachmentIdMap = attachmentIdMap;
        ServerOptions = serverOptions;
    }
}
