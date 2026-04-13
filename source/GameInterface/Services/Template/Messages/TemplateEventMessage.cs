using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Template.Messages;

/// <summary>
/// TODO update summary
/// A event is a reaction to something
/// Normally used in patches
/// </summary>
public record TemplateEventMessage : IEvent
{
    // This data can be anything you need
    public TemplateEventMessage(MobileParty instance, float value)
    {
        Instance = instance;
        Value = value;
    }

    // Example instance
    public MobileParty Instance { get; }

    // Example value
    public float Value { get; }
}

