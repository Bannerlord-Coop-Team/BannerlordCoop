using Common.Logging;
using Common.Network.Session;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.PlatformService;

namespace GameInterface.Services.Entity;

public interface IControllerIdProvider
{
    string ControllerId { get; }
    string LegacyControllerId { get; }
    void SetControllerId(string controllerId);
    void SetControllerAsPlatformIdentity(PlatformIdentity identity);
    void SetControllerAsLocalId();
    void SetControllerFromProgramArgs();
}

public class ControllerIdProvider : IControllerIdProvider
{
    private readonly IControllerIdStore controllerIdStore;

    public string ControllerId { get; private set; }
    public string LegacyControllerId { get; private set; }

    public ControllerIdProvider() : this(new ControllerIdStore())
    {
    }

    public ControllerIdProvider(IControllerIdStore controllerIdStore)
    {
        if (controllerIdStore == null) throw new ArgumentNullException(nameof(controllerIdStore));

        this.controllerIdStore = controllerIdStore;
    }

    public void SetControllerFromProgramArgs()
    {
        try
        {
            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();

            var platformArgIndex = args.FindIndex(x => x.ToLower() == "/platformid");

            ControllerId = args[platformArgIndex + 1];
            LegacyControllerId = string.Empty;
        }
        catch(Exception)
        {
            SetControllerAsLocalId();
        }        
    }

    public void SetControllerAsPlatformIdentity(PlatformIdentity identity)
    {
        if (!identity.IsValid || !identity.IsStorefrontIdentity)
            throw new ArgumentException("A valid storefront transport identity is required", nameof(identity));

        ControllerId = identity.ControllerId;
        LegacyControllerId = identity.UserId;
    }

    public void SetControllerAsLocalId()
    {
        ControllerId = "local:" + controllerIdStore.GetOrCreateId();
        LegacyControllerId = string.Empty;
        try
        {
            string platformUserId = PlatformServices.UserId;
            if (ulong.TryParse(platformUserId, out _))
                LegacyControllerId = platformUserId;
        }
        catch
        {
        }
    }

    public void SetControllerId(string controllerId)
    {
        ControllerId = controllerId;
        LegacyControllerId = string.Empty;
    }
}
