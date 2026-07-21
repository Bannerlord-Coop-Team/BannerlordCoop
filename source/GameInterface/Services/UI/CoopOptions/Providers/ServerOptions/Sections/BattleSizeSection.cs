using Common.Messaging;
using GameInterface.Services.UI.Messages;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.ServerOptions.Sections;

/// <summary>Exposes the server battle-size slider and publishes its applied value.</summary>
public class BattleSizeSection : CoopOptionsSectionVM
{
    public const string SectionId = "BattleSizeSection";

    private readonly IMessageBroker messageBroker;

    private int battleSizeIndex;

    public BattleSizeSection(
        int battleSize,
        IMessageBroker messageBroker)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));

        battleSizeIndex = ServerOptionsTabProvider.GetNearestBattleSizeIndex(battleSize);
        this.messageBroker = messageBroker;
    }

    public override string Id => SectionId;

    public string TitleText => ServerOptionsTabProvider.SectionTitleText;
    public string DescriptionText => ServerOptionsTabProvider.SectionDescriptionText;

    [DataSourceProperty]
    public int BattleSizeIndex
    {
        get => battleSizeIndex;
        set
        {
            var clampedIndex = Math.Max(0, Math.Min(ServerOptionsTabProvider.MaximumBattleSizeIndex, value));
            if (battleSizeIndex == clampedIndex) return;

            battleSizeIndex = clampedIndex;
            OnPropertyChanged(nameof(BattleSizeIndex));
            OnPropertyChanged(nameof(BattleSizeText));
        }
    }

    [DataSourceProperty]
    public string BattleSizeText => BattleSize.ToString();

    public int BattleSize => ServerOptionsTabProvider.GetBattleSizeForIndex(BattleSizeIndex);

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(tabId, Id, BattleSizeSectionOptions.FromBattleSize(BattleSize));
    }

    public override void AfterApply()
    {
        messageBroker.Publish(this, new ServerBattleSizeSelected(BattleSize));
    }
}
