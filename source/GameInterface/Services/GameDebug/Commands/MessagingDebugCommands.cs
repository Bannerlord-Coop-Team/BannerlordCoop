#if DEBUG
using Common.Commands;
using Common.Logging;
using Common.Messaging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// [Debug] Message broker commands. <c>coop.debug.messaging.publish_throwing</c> publishes a probe message to a
/// subscriber that throws and returns the failure line the broker logged for it, so what a live session logs
/// for a faulting handler can be read on either side without staging a real handler fault.
/// </summary>
internal class MessagingDebugCommands
{
    private const string ProbeFailureMessage = "messaging probe failure";

    public static readonly ILogger Logger = LogManager.GetLogger<MessagingDebugCommands>();

    public sealed class MessagingPublishThrowingCoopCommand : ICoopCommand
    {
        private readonly IMessageBroker messageBroker;

        public MessagingPublishThrowingCoopCommand(IMessageBroker messageBroker)
        {
            if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));

            this.messageBroker = messageBroker;
        }

        public string Prefix => "coop.debug.messaging";

        public string Name => "publish_throwing";

        public string Description => "Publishes a probe message to a subscriber that throws and returns the failure line the broker logged.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var captured = new List<string>();
            // The sink calls back on whichever thread logs, so the capture is guarded
            Action<string> capture = line => { lock (captured) captured.Add(line); };
            var subscriber = new ThrowingProbeSubscriber();
            messageBroker.Subscribe<MessagingProbeMessage>(subscriber.Handle);
            OutputSinkManager.AddLogCallback(capture);
            try
            {
                messageBroker.Publish(this, new MessagingProbeMessage());
            }
            finally
            {
                OutputSinkManager.RemoveLogCallback(capture);
                // Subscriptions are weak; the explicit unsubscribe keeps a rerun from hitting two probes
                messageBroker.Unsubscribe<MessagingProbeMessage>(subscriber.Handle);
                GC.KeepAlive(subscriber);
            }

            string[] failureLines;
            lock (captured)
            {
                // Only the probe's own failure counts; another handler failing at the same moment is not this result
                failureLines = captured.Where(line => line.Contains(ProbeFailureMessage)).ToArray();
            }
            if (failureLines.Length == 0)
                return new CoopCommandResult(false, "The broker logged nothing for the throwing probe subscriber.", "failure_line_missing");

            ShowInGameWindow(failureLines[0]);
            return new CoopCommandResult(true, string.Join(Environment.NewLine, failureLines));
        }

        // Mirrors the first two lines of the logged failure in the campaign message log so a screenshot of the
        // session shows what the file log holds; a UI that is not up yet must not turn the result into a failure
        private static void ShowInGameWindow(string failureLine)
        {
            try
            {
                foreach (var line in failureLine.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(2))
                    InformationManager.DisplayMessage(new InformationMessage("[coop.debug.messaging] " + line));
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "The probe failure line could not be shown in the game window");
            }
        }
    }

    private sealed class MessagingProbeMessage : IMessage
    {
    }

    private sealed class ThrowingProbeSubscriber
    {
        public void Handle(MessagePayload<MessagingProbeMessage> payload)
        {
            throw new InvalidOperationException(ProbeFailureMessage);
        }
    }
}
#endif
