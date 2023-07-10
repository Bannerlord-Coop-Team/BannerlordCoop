using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Ends the current settlement encounter for the player
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkEndSettlementEncounter : ICommand
{
}