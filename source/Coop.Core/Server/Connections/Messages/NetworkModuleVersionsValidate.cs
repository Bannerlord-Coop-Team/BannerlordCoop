using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Modules;
using ProtoBuf;
using Serilog;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Message from Client to Server for validating the module versions.
/// Responsibilities
/// 1. Make sure that all active modules have the same version as on the server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkModuleVersionsValidate : ICommand
{
    [ProtoMember(1)]
    public NetworkModuleInfo[] Modules { get; }

    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules)
    {
        Modules = modules.Select(m => new NetworkModuleInfo(m.Id, m.IsOfficial, m.Version)).ToArray();
    }
}
