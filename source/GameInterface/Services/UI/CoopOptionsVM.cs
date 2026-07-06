using Common.Messaging;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

public class CoopOptionsVM : ViewModel
{
    private readonly IPlayerKillFeedColorOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;

    private int red;
    private int green;
    private int blue;

    public string MovieTextHeader => "Coop Options";
    public string KillFeedColorText => "Kill-feed color";
    public string KillFeedColorDescriptionText => "Kills from you and your own troops will show this color in the kill-feed to all players";
    public string RedText => "Red";
    public string GreenText => "Green";
    public string BlueText => "Blue";
    public string ApplyButtonText => "Apply";

    public CoopOptionsVM() : this(ResolveOptionsStore(), MessageBroker.Instance)
    {
    }

    public CoopOptionsVM(IPlayerKillFeedColorOptionsStore optionsStore) : this(optionsStore, MessageBroker.Instance)
    {
    }

    public CoopOptionsVM(IPlayerKillFeedColorOptionsStore optionsStore, IMessageBroker messageBroker)
    {
        this.optionsStore = optionsStore;
        this.messageBroker = messageBroker;

        ApplyColor(optionsStore.LoadOrDefault());
    }

    [DataSourceProperty]
    public int Red
    {
        get => red;
        set => ApplyColor(PlayerKillFeedColor.Clamp(value, green, blue));
    }

    [DataSourceProperty]
    public int Green
    {
        get => green;
        set => ApplyColor(PlayerKillFeedColor.Clamp(red, value, blue));
    }

    [DataSourceProperty]
    public int Blue
    {
        get => blue;
        set => ApplyColor(PlayerKillFeedColor.Clamp(red, green, value));
    }

    [DataSourceProperty]
    public string PreviewColorString => CurrentColor.ToColorString();

    private PlayerKillFeedColor CurrentColor => new PlayerKillFeedColor(red, green, blue);

    public void ActionApply()
    {
        var color = CurrentColor;
        string message = "Coop options successfully updated.";

        try
        {
            optionsStore.Save(color);
            messageBroker.Publish(this, new PlayerKillFeedColorSelected(color));
        }
        catch
        {
            message = "Coop options unsuccessfully updated.";
        }

        InformationManager.DisplayMessage(new InformationMessage(message));
    }

    public void ActionCancel()
    {
        ScreenManager.PopScreen();
    }

    private void ApplyColor(PlayerKillFeedColor color)
    {
        bool rgbChanged =
            red != color.Red ||
            green != color.Green ||
            blue != color.Blue;

        red = color.Red;
        green = color.Green;
        blue = color.Blue;

        if (rgbChanged)
        {
            OnPropertyChanged(nameof(Red));
            OnPropertyChanged(nameof(Green));
            OnPropertyChanged(nameof(Blue));
            OnPropertyChanged(nameof(PreviewColorString));
        }
    }

    private static IPlayerKillFeedColorOptionsStore ResolveOptionsStore()
    {
        if (ContainerProvider.TryResolve<IPlayerKillFeedColorOptionsStore>(out var store))
        {
            return store;
        }

        return new PlayerKillFeedColorOptionsStore();
    }
}
