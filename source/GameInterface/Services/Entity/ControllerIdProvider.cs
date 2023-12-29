using Common.Logging;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.PlatformService;

namespace GameInterface.Services.Entity;

public interface IControllerIdProvider
{
    string ControllerId { get; }
    void SetControllerId(string controllerId);
    void SetControllerAsPlatformId();
    void SetControllerFromProgramArgs();
}

public class ControllerIdProvider : IControllerIdProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger<ControllerIdProvider>();

    public string ControllerId { get; private set; }

    public void SetControllerFromProgramArgs()
    {
        try
        {
            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();

            var platformArgIndex = args.FindIndex(x => x.ToLower() == "/platformid");

            ControllerId = args[platformArgIndex + 1];
        }
        catch(Exception)
        {
            SetAsDefault();
        }        
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

    public void SetControllerId(string controllerId)
    {
        ControllerId = controllerId;
    }

    public void SetAsDefault()
    {
        ControllerId = "DefaultId";
    }
}
