// -----------------------------------------------------------------------
// <copyright file="SortedProjects.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace DXBuildGenerator {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using System.Reflection;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class SortedProjects {
        private readonly Dictionary<string, Project> projects = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Project> visited = new HashSet<Project>();
        private readonly List<Project> sortedList = new List<Project>();
        private readonly HashSet<string> unknownReferences = new HashSet<string>();
        private readonly HashSet<string> excludedProjects = new HashSet<string>();

        public void Add(Project project) {
            projects.Add(project.GetAssemblyName(), project);
        }

        public void AddExcluded(string assemblyName) {
            excludedProjects.Add(assemblyName);
        }


        public void Sort() {
            sortedList.Clear();
            visited.Clear();
            unknownReferences.Clear();
            foreach (var project in projects.Values) {
                sortedList.AddRange(SortProject(project));
            }
        }

        public IEnumerable<string> UnknownReferences {
            get {
                return unknownReferences;
            }
        }

        public IList<Project> SortedList { get { return sortedList; } }

        private IEnumerable<Project> SortProject(Project project) {
            if (!visited.Contains(project)) {
                visited.Add(project);
                foreach (var reference in project.GetItems("Reference")) {
                    //TODO: Get rid of hard-coded exclusions
                    if (reference.EvaluatedInclude != "DevExpress.XtraRichEdit.v12.2.Extensions, Version=12.2.0.0, Culture=neutral, PublicKeyToken=79868b8147b5eae4, processorArchitecture=MSIL") {
                        AssemblyName assemblyName = new AssemblyName(reference.EvaluatedInclude);
                        string shortName = assemblyName.Name;
                        Project referencedProject;
                        if (projects.TryGetValue(shortName, out referencedProject)) {
                            foreach (var sorted in SortProject(referencedProject))
                                yield return sorted;
                        }
                        else if (excludedProjects.Contains(shortName)) {
                            excludedProjects.Add(shortName);
                            yield break;
                        }
                        else if (shortName.StartsWith("DevExpress", StringComparison.OrdinalIgnoreCase))
                            unknownReferences.Add(shortName);
                    }
                }
                yield return project;
            }
        }
    }
}
