using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Models.E2E;
using Scaffolderlord.Models.Lifetime;
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
        Name = "e2e-props",
        Description = "Generates E2E property tests for the specified type (server side tests)",
        Parent = typeof(RootCliCommand)
        )]
    public class GeneratePropertyTests : GenerateCommandBase<E2EPropertyTestsTemplateModel>
    {
        public GeneratePropertyTests(IScaffoldingService scaffoldingService) : base(scaffoldingService)
        {
        }

        [CliOption(Name = "--members",
        Description = "Specify the name of members to create tests for",
        AllowMultipleArgumentsPerToken = true)]
        public string[] MembersOption { get; set; } = Array.Empty<string>();

        protected override ServiceTypeInfo GetServiceTypeInfo() => ReflectionHelper.GetServiceTypeInfo(TypeFullyQualifiedName!, MembersOption);
    }
}
