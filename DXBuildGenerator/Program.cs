using System;
using System.Collections.Generic;
using System.Linq;

namespace DXBuildGenerator {
    class Program {

        static void Main(string[] args) {
            BuildGenerator generator = BuildGenerator.Create(args);
            if (generator != null)
                generator.Generate();
        }


    }

}
