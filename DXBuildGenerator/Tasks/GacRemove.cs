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
            if (File.Exists(path))
                publish.GacRemove(path);
        }
    }
}
