using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DXBuildGenerator.Tasks
{
    public class Zip : Task
    {
        public override bool Execute()
        {
            using (var stream = File.Create(ZipFileName))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var item in Files)
                {
                    string filePath = item.ItemSpec;
                    var entry = archive.CreateEntry(Utils.MakeRelativePath(WorkingDirectory, filePath));

                    using (var inputStream = File.OpenRead(filePath))
                    using (var outStream = entry.Open())
                    {
                        inputStream.CopyTo(outStream);
                    }
                }
            }

            return true;
        }

        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string ZipFileName { get; set; }

        [Required]
        public string WorkingDirectory { get; set; }
    }
}
