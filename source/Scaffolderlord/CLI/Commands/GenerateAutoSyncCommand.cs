using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.CLI.Commands
{

    [CliCommand(
    Name = "sync",
    Description = "Generates an AutoSync class",
    Parent = typeof(RootCliCommand)
    )]
    public class GenerateAutoSyncCommand : GenerateCommandBase<AutoSyncTemplateModel>
    {
        public GenerateAutoSyncCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
        {
        }

        [CliOption(Name = "--members",
        Description = "Specify the name of members to AutoSync (accepts fields and properties)",
        AllowMultipleArgumentsPerToken = true)]
        public string[] MembersOption { get; set; } = Array.Empty<string>();

        protected override ServiceTypeInfo GetServiceTypeInfo() => ReflectionHelper.GetServiceTypeInfo(TypeFullyQualifiedName!, MembersOption);
    }
}
