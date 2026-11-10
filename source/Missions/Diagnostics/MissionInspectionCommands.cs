#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Missions.Diagnostics;

public abstract class MissionInspectionCommand : ICoopCommand
{
    private readonly IMissionInspection inspection;
    private readonly MissionInspectionSlice slice;
    private readonly bool paged;

    protected MissionInspectionCommand(IMissionInspection inspection, MissionInspectionSlice slice, bool paged = false)
    {
        if (inspection == null) throw new ArgumentNullException(nameof(inspection));
        this.inspection = inspection;
        this.slice = slice;
        this.paged = paged;
    }

    public string Prefix => "coop.debug.mission";
    public string Name => slice.ToString().ToLowerInvariant();
    public string Description => "Read bounded " + Name + " observations without changing mission state; unavailable fields are explicit.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => paged ? new IExpectedArgs[]
    {
        new ExpectedArgs("offset", "Collection offset 0..100000, default 0; pages are not stable across ticks.", false),
        new ExpectedArgs("limit", "Rows 1..16, default 8.", false)
    } : Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        int offset = 0, limit = 8;
        if (args.Count > (paged ? 2 : 0)
            || (args.Count > 0 && !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out offset))
            || (args.Count > 1 && !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out limit))
            || offset < 0 || offset > MissionInspection.MaximumOffset || limit < 1 || limit > MissionInspection.MaximumPageSize)
            return new CoopCommandResult(false, "Expected offset 0..100000 and limit 1..16.", "invalid_arguments");
        string json = JsonConvert.SerializeObject(inspection.Read(slice, offset, limit));
        if (json.Length > 24576) return new CoopCommandResult(false, "Mission inspection exceeded the output cap.", "output_limit");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + json);
    }
}

public sealed class MissionSummaryCommand : MissionInspectionCommand
{
    public MissionSummaryCommand(IMissionInspection inspection) : base(inspection, MissionInspectionSlice.Summary) { }
}

public sealed class MissionCameraCommand : MissionInspectionCommand
{
    public MissionCameraCommand(IMissionInspection inspection) : base(inspection, MissionInspectionSlice.Camera) { }
}

public sealed class MissionAgentsCommand : MissionInspectionCommand
{
    public MissionAgentsCommand(IMissionInspection inspection) : base(inspection, MissionInspectionSlice.Agents, true) { }
}

public sealed class MissionViewsCommand : MissionInspectionCommand
{
    public MissionViewsCommand(IMissionInspection inspection) : base(inspection, MissionInspectionSlice.Views, true) { }
}
#endif
