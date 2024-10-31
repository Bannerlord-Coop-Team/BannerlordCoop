using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Models.Lifetime;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.CLI.Commands
{
    [CliCommand(
        Name = "e2e",
        Description = "Generates all e2e tests for a specified type",
        Parent = typeof(RootCliCommand)
        )]
    public class GenerateE2ECommand : GenerateAutoSyncCommand
    {
        public GenerateE2ECommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
        {
        }

        protected override ITemplateModel GetTemplateModel() => null;
        public override async Task RunAsync()
        {
            var commands = new ICliCommand[]
            {
                new GeneratePropertyTests(scaffolder),
                new GenerateFieldTests(scaffolder),
                new GenerateLifetimeTests(scaffolder)
            };
            this.PropagateCliArgumentsAndOptions(commands);
            foreach (var command in commands) await command.RunAsync();

            if (!CommandPropagated) PrintCommandSucceededMessage();
        }
    }
}
