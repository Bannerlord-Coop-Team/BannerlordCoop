using System.Collections.Generic;
using TaleWorlds.Library;

namespace GameInterface.Services.Voice;

public sealed class VoiceSpeakersVM : ViewModel
{
    private readonly IVoiceSpeakerNameResolver names;
    public VoiceSpeakersVM(IVoiceSpeakerNameResolver names) { this.names = names; }
    [DataSourceProperty] public MBBindingList<VoiceSpeakerVM> Speakers { get; } = new();
    [DataSourceProperty] public bool HasSpeakers => Speakers.Count > 0;
    [DataSourceProperty] public string Title => "Talking";

    public void Refresh(IReadOnlyList<string> audible)
    {
        int index = 0;
        foreach (string speaker in audible)
        {
            string name = names.Resolve(speaker);
            if (string.IsNullOrEmpty(name)) continue;
            if (index == Speakers.Count) Speakers.Add(new VoiceSpeakerVM(name));
            else Speakers[index].SetName(name);
            if (++index == 10) break;
        }
        while (Speakers.Count > index) Speakers.RemoveAt(Speakers.Count - 1);
        OnPropertyChanged(nameof(HasSpeakers));
    }
}

public sealed class VoiceSpeakerVM : ViewModel
{
    [DataSourceProperty] public string Name { get; private set; }
    public VoiceSpeakerVM(string name) { Name = name; }
    public void SetName(string name)
    {
        if (Name == name) return;
        Name = name;
        OnPropertyChanged(nameof(Name));
    }
}
