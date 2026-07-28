using Common;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.Save.Interfaces;

public interface ISaveNotificationInterface : IGameAbstraction
{
    void SetSaving(bool isSaving);
}

internal class SaveNotificationInterface : ISaveNotificationInterface
{
    public void SetSaving(bool isSaving)
    {
        GameThread.RunSafe(() =>
        {
            if (!ShouldApplySavingState(
                    isSaving,
                    GameStateManager.Current?.ActiveState is MapState))
            {
                return;
            }

            var dataSource = MapScreen.Instance?
                .GetMapView<GauntletMapSaveView>()?
                ._dataSource;
            if (dataSource == null) return;

            if (isSaving)
            {
                dataSource.OnSaveStarted();
            }
            else
            {
                dataSource.OnSaveOver(true, null);
            }
        }, context: nameof(SaveNotificationInterface));
    }

    internal static bool ShouldApplySavingState(bool isSaving, bool isCampaignMapActive) =>
        !isSaving || isCampaignMapActive;
}
