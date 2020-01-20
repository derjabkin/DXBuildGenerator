using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DXBuildGenerator
{
    class ProjectInfo
    {
        public bool IsSilverlight { get; set; }
        public bool IsTest { get; set; }
        public bool IsMvc { get; set; }
        public bool IsWinRT { get; set; }
        public string AssemblyName { get; set; }
        public bool IsUwp => Platform == ProjectPlatform.UWP;

        public bool IsCodedUITests { get; set; }

        public ProjectPlatform Platform { get; set; }

        public Project MSBuildProject { get; set; }
    }

}
