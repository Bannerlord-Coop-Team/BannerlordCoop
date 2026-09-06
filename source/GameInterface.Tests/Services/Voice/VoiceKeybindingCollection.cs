using Xunit;

namespace GameInterface.Tests.Services.Voice;

// Vanilla caches ViewModel metadata and input contexts in unsynchronized static dictionaries.
[CollectionDefinition("Voice keybinding UI", DisableParallelization = true)]
public sealed class VoiceKeybindingCollection
{
}
