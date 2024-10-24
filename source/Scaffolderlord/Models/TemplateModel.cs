using Scaffolderlord.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models
{
    // decided to just go with default interface impl
    //public abstract class BaseTemplateModel
    //{
    //    public string? Namespace { get; set; }
    //    public string[] Usings { get; set; } = Array.Empty<string>();

    //    public abstract string GetTemplatePath();
    //    public abstract string GetOutputPath();
    //}

    public interface ITemplateModel
    {
        string TemplateFileName { get; }
        string? Namespace { get; }
        string[] Usings { get; }

        string GetTemplateFilePath()
        {
            return Path.Combine(GetRelativePath("Templates"), $"{TemplateFileName}.cshtml");
        }
        string GetOutputPath();
    }

}
