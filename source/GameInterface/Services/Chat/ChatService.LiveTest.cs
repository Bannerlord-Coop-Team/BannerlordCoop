#if DEBUG
namespace GameInterface.Services.Chat;

/// <summary>[Debug] Lets the chat_submit command type into the chat on screen; Debug builds only.</summary>
internal interface IChatLiveTestHook
{
    /// <summary>Puts the text in the chat input and runs the Send button's action; false when chat is turned off.</summary>
    bool SubmitForLiveTest(string text, out string state);
}

public sealed partial class ChatService : IChatLiveTestHook
{
    private int sentMessageCount;

    // Goes through the same ActionSend as the Send button, so a live run proves !motd and a chat line
    // without operating-system input. It replaces anything typed and sends on the selected channel.
    public bool SubmitForLiveTest(string text, out string state)
    {
        if (!overlay.IsEnabled)
        {
            state = "Chat is turned off in the co-op options, so nothing was typed or sent.";
            return false;
        }

        int sentBefore = sentMessageCount;
        viewModel.WrittenText = text;
        viewModel.ActionSend();

        string transcript = viewModel.TranscriptText;
        string lastLine = transcript.Substring(transcript.LastIndexOf('\n') + 1);
        state = "Sent: " + (sentMessageCount != sentBefore) +
                "\nChannel: " + viewModel.ActiveChannelText +
                "\nInput: " + (viewModel.WrittenText.Length == 0 ? "empty" : viewModel.WrittenText) +
                "\nLast line: " + (lastLine.Length == 0 ? "none" : lastLine);
        return true;
    }
}
#endif
