using GameInterface.Services.Heroes.Interfaces;
using Serilog;
using System;

namespace GameInterface.Services.BugReporting;

/// <summary>Creates the persistent server campaign save attached to a diagnostic report.</summary>
public interface IBugReportServerSaveProvider
{
    bool TryCapture(out CollectedBugReportServerSave save);
}

/// <inheritdoc />
public class BugReportServerSaveProvider : IBugReportServerSaveProvider
{
    internal const string SaveName = "coop_bug_report";

    private readonly ISaveInterface saveInterface;
    private readonly ILogger logger;

    public BugReportServerSaveProvider(ISaveInterface saveInterface, ILogger logger)
    {
        if (saveInterface == null) throw new ArgumentNullException(nameof(saveInterface));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        this.saveInterface = saveInterface;
        this.logger = logger;
    }

    public bool TryCapture(out CollectedBugReportServerSave save)
    {
        save = null;
        try
        {
            var result = saveInterface.SaveCurrentGameToFile(SaveName);
            if (!result.Success || result.Data == null || result.Data.Length == 0)
            {
                logger.Warning("The server campaign save for the diagnostic bug report could not be created");
                return false;
            }

            save = new CollectedBugReportServerSave(SaveName + ".sav", result.Data);
            return true;
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Creating the server campaign save for the diagnostic bug report failed");
            return false;
        }
    }
}
