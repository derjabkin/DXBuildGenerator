using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmdToMSBuild.Tasks
{
    public class GacRemove : GacTaskBase
    {
        protected override void ExecutePublish(System.EnterpriseServices.Internal.Publish publish, string path)
        {
            if (File.Exists(path)) {
                Log.LogMessage(MessageImportance.Normal, "GacRemove: {0}", path);
                publish.GacRemove(path);

            }
        }
    }
}
