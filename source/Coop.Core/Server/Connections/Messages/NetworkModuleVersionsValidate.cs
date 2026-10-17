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
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkModuleVersionsValidate : ICommand
{
    [ProtoMember(1)]
    public NetworkModuleInfo[] Modules { get; }
    [ProtoMember(2)]
    public string? CoopBuildVersion { get; }

    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules)
        : this(modules, ModInformation.BuildVersion)
    {
    }

    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules, string? coopBuildVersion)
        : this(modules, coopBuildVersion, null)
    {
    }

    /// <param name="displayName">Display name per module id, sent only so join refusals can name mods.</param>
    public NetworkModuleVersionsValidate(IEnumerable<ModuleInfo> modules, string? coopBuildVersion, Func<string, string>? displayName)
    {
        CoopBuildVersion = coopBuildVersion;
        if (modules is null)
        {
            Modules = Array.Empty<NetworkModuleInfo>();
            return;
        }

        Modules = modules.Select(m => new NetworkModuleInfo(m.Id, m.IsOfficial, m.IsDlc, m.Version, displayName?.Invoke(m.Id))).ToArray();
    }
}
