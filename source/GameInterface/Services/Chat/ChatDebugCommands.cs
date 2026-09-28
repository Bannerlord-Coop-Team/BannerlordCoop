#if DEBUG
using Common.Commands;
using GameInterface.Services.Chat.Messages;

namespace GameInterface.Services.Chat;

/// <summary>[Debug] Sends a chat line through the chat's own Send action, so a live run needs no operating-system input.</summary>
internal class ChatDebugCommands
{
    public sealed class ChatSubmitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";
        public string Name => "chat_submit";
        public string Description => "Puts the text in the chat input and runs the Send button's action: !motd reopens the server info panel and sends nothing, other text is sent.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            new ExpectedArgs("text", "the chat line, for example !motd; quote a line with spaces", isRequired: true)
        };

        // Refuses text the chat input could not hold before anything is typed.
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string text = args.Count == 1 ? args[0] : null;
            if (string.IsNullOrWhiteSpace(text))
                return new CoopCommandResult(false, "Give the chat line, for example !motd or \"hello there\".", "invalid_arguments");
            if (text.Length > ChatMessageLimits.MaxMessageLength)
                return new CoopCommandResult(false, $"The chat input holds at most {ChatMessageLimits.MaxMessageLength} characters.", "text_too_long");
            if (!ContainerProvider.TryResolve<IChatService>(out var chat))
                return new CoopCommandResult(false, "Chat unavailable.", "unavailable");
            return Submit(chat, text);
        }

        internal static CoopCommandResult Submit(IChatService chat, string text) =>
            chat.SubmitForLiveTest(text, out string state)
                ? new CoopCommandResult(true, state)
                : new CoopCommandResult(false, state, "chat_disabled");
    }
}
#endif
