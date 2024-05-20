using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _lastCalculatedBaseSpeedExplained
/// </summary>
public record ChangeLastCalculatedBaseSpeedExplained(float Number, bool IncludeDescriptions, string TextObjectValue, string MobilePartyId) : ICommand
{
    public float Number { get; } = Number;

    public bool IncludeDescriptions { get; } = IncludeDescriptions;
    
    public string TextObjectValue { get; } = TextObjectValue;
    public string MobilePartyId { get; } = MobilePartyId;
}