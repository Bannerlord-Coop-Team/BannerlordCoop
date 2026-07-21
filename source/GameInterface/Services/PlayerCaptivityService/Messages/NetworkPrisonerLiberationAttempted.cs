using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// Requests the vanilla relation reward for a client who liberated a prisoner.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPrisonerLiberationAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string PrisonerId;

    public NetworkPrisonerLiberationAttempted(string prisonerId)
    {
        PrisonerId = prisonerId;
    }
}
