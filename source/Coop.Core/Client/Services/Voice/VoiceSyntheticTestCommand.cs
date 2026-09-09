#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Voice;

namespace Coop.Core.Client.Services.Voice;

public sealed class VoiceSyntheticTestCommand : ICoopCommand
{
    private readonly IVoiceSyntheticTest test;
    private readonly IVoiceCodecFactory codecs;

    public VoiceSyntheticTestCommand(IVoiceSyntheticTest test, IVoiceCodecFactory codecs)
    {
        this.test = test;
        this.codecs = codecs;
    }

    public string Prefix => "coop.debug.voice";
    public string Name => "synthetic";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public string Description => "Runs a bounded synthetic tone through voice transport, not a physical microphone test.";
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "start, status, or stop", isRequired: true)
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient)
            return new CoopCommandResult(false, "Run this command on a joined client.", "command_failed");
        if (args.Count != 1)
            return new CoopCommandResult(false, "Usage: coop.debug.voice.synthetic start|status|stop", "command_failed");
        switch (args[0])
        {
            case "start": return new CoopCommandResult(true, test.StartSyntheticVoiceTest(codecs));
            case "status": return new CoopCommandResult(true, test.SyntheticVoiceTestStatus);
            case "stop": return new CoopCommandResult(true, test.StopSyntheticVoiceTest());
            default: return new CoopCommandResult(false, "Usage: coop.debug.voice.synthetic start|status|stop", "command_failed");
        }
    }
}
#endif
