using System;
using System.Collections.Generic;
using System.Linq;

namespace DXBuildGenerator {
    class Program {

        static void Main(string[] args) {
                BuildGenerator generator = new BuildGenerator();
                if (CommandLine.Parser.Default.ParseArguments(args, generator)) {

                    if ((string.IsNullOrWhiteSpace(generator.ReferencesPath) || string.IsNullOrWhiteSpace(generator.SourceCodeDir))
                        && string.IsNullOrEmpty(generator.DevExpressRoot)) {
                        Console.WriteLine(@"Either -x or -s and -r options should be specified.\r\n");
                    }
                    generator.ResolvePaths();
                    if (!string.IsNullOrWhiteSpace(generator.DevExpressRoot)) {
                        Console.WriteLine("WARNING: The generated script will replace original DevExpress Assemblies!");
                    }
                    generator.Generate();
                }
        }


    }

}
