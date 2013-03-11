// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace DXBuildGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.Evaluation;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Extensions
    {
        public static string GetAssemblyName(this Project project)
        {
            return project.GetPropertyValue("AssemblyName");
        }

        public static bool IsFramework4(this Project project)
        {
            return project.GetPropertyValue("TargetFrameworkVersion") == "v4.0";
        }
    }
}
