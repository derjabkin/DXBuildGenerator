using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.EnterpriseServices.Internal;
using System.Linq;
using System.Text;

namespace CmdToMSBuild.Tasks
{
    public abstract class GacTaskBase : Task
    {
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        public override bool Execute()
        {
            var publish = new Publish();
            foreach (var item in Assemblies)
            {
                ExecutePublish(publish, item.ItemSpec);
            }

            return true;
        }

        protected abstract void ExecutePublish(Publish publish, string path);
    }
}
