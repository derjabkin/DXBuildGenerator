using System;
using System.Collections.Generic;
using System.Linq;

namespace DXBuildGenerator {
    class Program {

        static void Main(string[] args) {
            BuildGenerator generator = new BuildGenerator();
            if (CommandLine.Parser.Default.ParseArguments(args, generator)) {
                generator.Generate();
            }
        }


    }

}
