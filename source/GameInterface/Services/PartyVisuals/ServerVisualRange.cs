using Common;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals;

/// <summary>
/// Limits which party visuals the server builds to those near its map camera.
/// Without this, too many party visuals can be created by the server at once and causes frequent system access violations.
/// </summary>
public interface IServerVisualRange : IGameAbstraction
{
    // Whether a dirty visual for this party should be rebuilt now.
    bool ShouldRefresh(PartyBase party);
}

public class ServerVisualRange : IServerVisualRange
{
    private const float VisualRange = 50f;

    public bool ShouldRefresh(PartyBase party)
    {
        if (ModInformation.IsClient) return true;

        var cameraView = MapScreen.Instance?.MapCameraView;
        if (cameraView == null || party == null) return true;

        var cameraPosition = cameraView.CameraFrame.origin.AsVec2;
        return party.Position.ToVec2().DistanceSquared(cameraPosition) <= VisualRange * VisualRange;
    }
}
