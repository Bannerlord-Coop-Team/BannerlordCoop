using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Presents the server's message of the day as a titled list of paragraphs.</summary>
internal sealed class ServerInfoVM : ViewModel
{
    private readonly Action close;
    private bool isOpen;
    [DataSourceProperty] public MBBindingList<ServerInfoParagraphVM> Paragraphs { get; } = new MBBindingList<ServerInfoParagraphVM>();
    [DataSourceProperty] public string Title => new TextObject("{=coop_motd_title}Message of the Day").ToString();
    [DataSourceProperty] public string CloseText => new TextObject("{=coop_motd_close}Close").ToString();
    [DataSourceProperty] public bool IsOpen
    {
        get => isOpen;
        set { if (isOpen == value) return; isOpen = value; OnPropertyChanged(nameof(IsOpen)); }
    }

    // Supplies the overlay's close action without coupling the text to the screen system.
    public ServerInfoVM(Action close) => this.close = close;

    // Replaces the shown text; each server line becomes one paragraph.
    public void SetParagraphs(IEnumerable<string> paragraphs)
    {
        foreach (var paragraph in Paragraphs) paragraph.OnFinalize();
        Paragraphs.Clear();
        foreach (var text in paragraphs) Paragraphs.Add(new ServerInfoParagraphVM(text));
    }

    // Allows the native close button to release overlay input focus.
    public void ExecuteClose() => close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        foreach (var paragraph in Paragraphs) paragraph.OnFinalize();
    }
}

/// <summary>One operator-written paragraph, shown exactly as the server sent it.</summary>
internal sealed class ServerInfoParagraphVM : ViewModel
{
    // Bound as a plain string: a TextObject would expand {VARIABLES} and localization ids in operator text.
    [DataSourceProperty] public string Text { get; }

    public ServerInfoParagraphVM(string text) => Text = text;
}
