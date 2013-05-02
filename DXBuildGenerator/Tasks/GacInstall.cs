using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmdToMSBuild.Tasks
{
    public class GacInstall : GacTaskBase
    {
        protected override void ExecutePublish(System.EnterpriseServices.Internal.Publish publish, string path)
        {
            if (File.Exists(path))
            {
                publish.GacInstall(path);
                Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, "GacInstall: {0}", path);
            }
            else
                Log.LogError("GacInstall: File Not Found: {0}", path);
        }
    }
}
