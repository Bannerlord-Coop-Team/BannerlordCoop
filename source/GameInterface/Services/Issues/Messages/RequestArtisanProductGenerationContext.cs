using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestArtisanProductGenerationContext : ICommand
{
}
