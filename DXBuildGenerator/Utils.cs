using System;
using System.IO;

namespace DXBuildGenerator
{
    class Utils
    {
        /// <summary>
        /// http://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <param name="dontEscape">Boolean indicating whether to add uri safe escapes to the relative path</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(GetFullPath(fromPath) + "\\");
            Uri toUri = new Uri(GetFullPath(toPath));

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string GetFullPath(string fromPath)
        {
            try
            {
                return Path.GetFullPath(fromPath);
            }
            catch(ArgumentException aex)
            {
                throw new ArgumentException($"GetFullPath failed for '{fromPath}'", aex);
            }
        }
    }
}
