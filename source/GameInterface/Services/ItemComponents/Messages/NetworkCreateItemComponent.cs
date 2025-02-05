using Common.Messaging;
using GameInterface.Services.ItemComponents.Data;
using ProtoBuf;

namespace GameInterface.Services.ItemComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCreateItemComponent(ItemComponentData Data) : ICommand
{
    [ProtoMember(1)]
    public ItemComponentData Data { get; } = Data;
}
