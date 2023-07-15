using Common.Logging;
using Serilog;
using TaleWorlds.PlatformService;

namespace GameInterface.Services.Entity;

public interface IControllerIdProvider
{
    string ControllerId { get; }

    void SetControllerAsPlatformId();
    void SetControllerId(string controllerId);
}

public class ControllerIdProvider : IControllerIdProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControllerIdProvider>();

    public string ControllerId { get; private set; }

    public void SetControllerId(string controllerId)
    {
        ControllerId = controllerId;
    }

    public void SetControllerAsPlatformId()
    {
        string controllerId = PlatformServices.UserId;

        if (string.IsNullOrEmpty(controllerId))
        {
            Logger.Error("{userId} was null", nameof(PlatformServices.UserId));
        }

        ControllerId = PlatformServices.UserId;
    }
}
