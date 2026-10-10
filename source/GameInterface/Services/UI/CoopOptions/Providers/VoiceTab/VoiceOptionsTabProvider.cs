using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.Voice;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.VoiceTab;

public sealed class VoiceOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "VoiceTab";
    private readonly IVoiceClient client;
    private readonly ICoopOptionsStore store;
    public VoiceOptionsTabProvider(IVoiceClient client, ICoopOptionsStore store)
    {
        this.client = client;
        this.store = store;
    }
    public string Id => TabId;
    public bool IsAvailable(ModOptions modOptions) => true;

    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker, Action<CoopOptionsTabVM> onSelect)
        => new(Id, "Voice", new[] { new VoiceSection(client, Load(options), store) }, onSelect);

    public static VoiceSettings Load(CoopOptionsData options)
        => (options ?? new CoopOptionsData()).GetSectionOrDefault(TabId, VoiceSection.SectionId, new VoiceSettings());
}
