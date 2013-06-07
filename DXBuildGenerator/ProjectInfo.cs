using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmdToMSBuild
{
    class ProjectInfo
    {
        public bool IsSilverlight { get; set; }
        public bool IsTest { get; set; }
        public bool IsMvc { get; set; }
        public bool IsWinRT { get; set; }
        public string AssemblyName { get; set; }
    }
}
