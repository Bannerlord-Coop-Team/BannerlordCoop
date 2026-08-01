using Common;
using Common.Messaging;
using GameInterface.Services.Modules;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Message from Client to Server for validating the module versions.
/// Responsibilities
/// 1. Make sure that all active modules have the same version as on the server
/// 2. Make sure the client and server use the same exact co-op build
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkModuleVersionsValidate : ICommand
{
    [ProtoMember(1)]
    public NetworkModuleInfo[] Modules { get; }
    [ProtoMember(2)]
    public string BuildVersion { get; }

    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules)
        : this(modules, ModInformation.BuildVersion)
    {
    }

    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules, string buildVersion)
    {
        BuildVersion = buildVersion;
        if (modules is null)
        {
            Modules = Array.Empty<NetworkModuleInfo>();
            return;
        }

        Modules = modules.Select(m => new NetworkModuleInfo(m.Id, m.IsOfficial, m.IsDlc, m.Version)).ToArray();
    }
}
