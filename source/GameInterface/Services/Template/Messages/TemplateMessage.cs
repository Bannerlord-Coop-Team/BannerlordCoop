using Common.Messaging;

namespace GameInterface.Services.Template.Messages;

/// <summary>
/// TODO update summary
/// A command changes the state of something
/// </summary>
public record TemplateCommandMessage : ICommand
{
}

/// <summary>
/// TODO update summary
/// A event is a reaction to something'
/// Normally used in patches
/// </summary>
public record TemplateEventMessage : ICommand
{
}

