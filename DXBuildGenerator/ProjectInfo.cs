using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DXBuildGenerator
{
    class ProjectInfo
    {
        public bool IsTest { get; set; }
        public bool IsMvc { get; set; }
        public bool IsWinRT { get; set; }
        public string AssemblyName { get; set; }

        public bool IsCodedUITests { get; set; }

        public ProjectPlatform Platform { get; set; }

        public Project MSBuildProject { get; set; }
        public string FrameworkVersion { get; set; }
        
        public bool IsXpf { get; internal set; }
    }

}
