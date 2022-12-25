using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace DXBuildGenerator
{
    class CommandLineOptions
    {
        [Option('x', HelpText = "Path to the DevExpress installation folder." +
            "If this option is specified, Source code directory,  references directory and the output path are determinated automatically.")]
        public string DevExpressRoot { get; set; }

        [Option("op", HelpText = "Output path for the compiled assemblies. If the value is not specified, the property value from the template will be used.")]
        public string OutputPath { get; set; }

        [Option('t', HelpText = "Template file name", Default = "Template.proj")]
        public string TemplateFileName { get; set; }

        [Option('o', HelpText = "Output file name", Default = "build.proj")]
        public string OutputFileName { get; set; }

        [Option('s', HelpText = "Source code directory")]
        public string SourceCodeDir { get; set; }

        [Option('r', HelpText = "Reference files root directory")]
        public string ReferencesPath { get; set; }

        [Option("notest", HelpText = "Skip test projects")]
        public bool SkipTestProjects { get; set; }

        [Option("nomvc", HelpText = "Skip ASP.NET MVC projects")]
        public bool SkipMvcProjects { get; set; }

        [Option("nowinrt", HelpText = "Skip WinRT (Windows 8) projects")]
        public bool SkipWinRTProjects { get; set; }

        [Option("copyrefdir", HelpText = "Reference files will be copied in the specified directory")]
        public string CopyReferencesDirecotry { get; set; }

        [Option("no-codedui", HelpText = "Skip CodedUI Proejcts")]
        public bool SkipCodedUIProject { get; set; }

        [Option("assemblyFoldersFile", HelpText = "If specified, a file containing a list of assemblynames and project folders will be generated")]
        public string AssemblyFoldersFile { get; set; }


        [Usage]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Example", UnParserSettings.WithGroupSwitchesOnly(),
                    new CommandLineOptions { DevExpressRoot = "c:\\Program Files (x86)\\DevExpress\\DXperience 12.2" });
            }
        }
    }

}
