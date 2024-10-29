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
        Name = "e2e-fields",
        Description = "Generates E2E field tests for the specified type (server side tests)",
        Parent = typeof(RootCliCommand)
        )]
    public class GenerateFieldTests : GenerateCommandBase<E2EPropertyTestsTemplateModel>
    {
        public GenerateFieldTests(IScaffoldingService scaffoldingService) : base(scaffoldingService)
        {
        }

    }
}
