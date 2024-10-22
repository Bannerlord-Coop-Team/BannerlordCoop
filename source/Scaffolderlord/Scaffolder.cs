using Scaffolderlord.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord
{
    public class Scaffolder
    {
        private readonly string templatePath;
        private readonly string outputPath;

        public Scaffolder(string templatePath, string outputPath)
        {
            this.templatePath = templatePath;
            this.outputPath = outputPath;
        }

        public async Task Generate()
        {
            var generator = new Mono.TextTemplating.TemplateGenerator();

            var session = generator.GetOrCreateSession();
            session.Add("ClassName", "MyClass");

            var result = await generator.ProcessTemplateAsync(templatePath, outputPath);

            foreach (var err in generator.Errors)
            {
                Console.WriteLine(err.ToString());
            }
        }

    }
}
