// -----------------------------------------------------------------------
// <copyright file="SortedProjects.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace DXBuildGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using System.Reflection;

    class SortedProjects
    {
        private readonly Dictionary<string, ProjectInfo> projects = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ProjectInfo> visited = new HashSet<ProjectInfo>();
        private readonly List<ProjectInfo> sortedList = new List<ProjectInfo>();
        private readonly HashSet<string> unknownReferences = new HashSet<string>();
        private readonly HashSet<string> excludedProjects = new HashSet<string>();

        public void Add(ProjectInfo projectInfo)
        {
            projects.Add(projectInfo.MSBuildProject.GetAssemblyName(), projectInfo);
        }

        public void AddExcluded(string assemblyName)
        {
            excludedProjects.Add(assemblyName);
        }


        public void Sort()
        {
            sortedList.Clear();
            visited.Clear();
            unknownReferences.Clear();
            foreach (var project in projects.Values)
            {
                sortedList.AddRange(SortProject(project));
            }
        }

        public IEnumerable<string> UnknownReferences
        {
            get
            {
                return unknownReferences;
            }
        }

        public IList<ProjectInfo> SortedList => sortedList;

        private IEnumerable<ProjectInfo> SortProject(ProjectInfo project)
        {
            if (!visited.Contains(project))
            {
                visited.Add(project);
                foreach (var reference in project.MSBuildProject.GetItems("Reference"))
                {
                    //TODO: Get rid of hard-coded exclusions
                    AssemblyName assemblyName = new AssemblyName(reference.EvaluatedInclude);
                    string shortName = assemblyName.Name;
                    if (projects.TryGetValue(shortName, out var referencedProject))
                    {
                        foreach (var sorted in SortProject(referencedProject))
                            yield return sorted;
                    }
                    else if (excludedProjects.Contains(shortName))
                    {
                        excludedProjects.Add(shortName);
                        yield break;
                    }
                    else if (shortName.StartsWith("DevExpress", StringComparison.OrdinalIgnoreCase))
                        unknownReferences.Add(shortName);
                }
                yield return project;
            }
        }
    }
}
