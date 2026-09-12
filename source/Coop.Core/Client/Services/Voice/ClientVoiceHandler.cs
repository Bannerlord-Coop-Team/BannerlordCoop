using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using Common.PacketHandlers;
using Common.Voice;
using GameInterface.Services.Voice;
using LiteNetLib;

namespace Coop.Core.Client.Services.Voice;

internal sealed class ClientVoiceHandler : IPacketHandler
{
    private readonly IPacketManager packets;
    private readonly IMessageBroker broker;
    private readonly IVoiceClient client;
    public PacketType PacketType => PacketType.Voice;

    public ClientVoiceHandler(IPacketManager packets, IMessageBroker broker, IVoiceClient client)
    {
        this.packets = packets;
        this.broker = broker;
        this.client = client;
        packets.RegisterPacketHandler(this);
        broker.Subscribe<VoiceConfiguration>(Configure);
        broker.Subscribe<ModConfigApplied>(ConfigurationApplied);
    }

    private void ConfigurationApplied(MessagePayload<ModConfigApplied> payload)
        => client.SetServerEnabled(payload.What.ModOptions.VoiceEnabled);

    private void Configure(MessagePayload<VoiceConfiguration> payload) => client.Configure(payload.What.Ranges, payload.What.SentAt);
    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (packet is VoicePacket voice) client.Receive(voice);
    }

    public void Dispose()
    {
        packets.RemovePacketHandler(this);
        broker.Unsubscribe<VoiceConfiguration>(Configure);
        broker.Unsubscribe<ModConfigApplied>(ConfigurationApplied);
    }
}
