using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.Build.Evaluation;
using System.Globalization;
using Microsoft.Build.Execution;
using Microsoft.Build.Construction;
using System.Xml.Linq;
using CommandLine;
using CommandLine.Text;

namespace DXBuildGenerator
{
    class BuildGenerator
    {
        private readonly List<string> referenceFiles = new List<string>();


        internal void Generate()
        {
            XDocument project = XDocument.Load(TemplateFileName);

            Project[] allProjects = (
                from fn in Directory.GetFiles(SourceCodeDir, "*.csproj", SearchOption.AllDirectories)
                select new Project(fn)).ToArray();


            var silverlightProjects = new SortedProjects();
            var windowsProjects = new SortedProjects();
            List<Project> excludedProjects = new List<Project>();

            foreach (var p in allProjects)
            {
                bool added = false;
                if (!p.GetAssemblyName().Contains("SharePoint") && !p.FullPath.Contains("DevExpress.Xpo.Extensions.csproj"))
                {
                    if (IsSilverlightProject(p))
                    {
                        if (!p.FullPath.Contains(".DemoBase."))
                        {
                            silverlightProjects.Add(p);
                            added = true;
                        }
                    }
                    else
                    {
                        windowsProjects.Add(p);
                        added = true;
                    }
                }
                if (!added)
                    excludedProjects.Add(p);

            }

            silverlightProjects.Sort();
            windowsProjects.Sort();

            referenceFiles.Clear();
            PopulateReferenceFiles(silverlightProjects.UnknownReferences);
            PopulateReferenceFiles(windowsProjects.UnknownReferences);
            referenceFiles.Add(typeof(System.Data.Entity.DbContext).Assembly.Location);
            CreateReferenceFilesGroup(project);
            ConvertPatchInternals(project);

            ConvertProjectsToBuild(project, silverlightProjects, null, "4.0", true);
            ConvertProjectsToBuild(project, windowsProjects, null, "4.0", false);

            ConvertTargetFiles(
                @"$(BuildTasksPath)\DevExpress.Build.XamlResourceProcessing.v12.1.dll");
            File.WriteAllText(OutputFileName, project.ToString().Replace("<ItemGroup xmlns=\"\">", "<ItemGroup>"));
        }

        private void CreateReferenceFilesGroup(XDocument project)
        {
            XElement itemGroup = CreateItemGroup();
            foreach (var referenceFile in referenceFiles)
            {
                itemGroup.Add(CreateItem("ReferenceFile", referenceFile));
            }
            project.Root.Add(itemGroup);
        }
        private static bool IsSilverlightProject(Project p)
        {
            return p.GetPropertyValue("TargetFrameworkIdentifier") == "Silverlight" || p.GetPropertyValue("BaseIntermediateOutputPath").Contains("obj.SL") ||
                                    p.FullPath.Contains(".SL");
        }


        private void PopulateReferenceFiles(IEnumerable<string> references)
        {
            string[] fileNames = references.Select(r => r += ".dll").ToArray();
            foreach (string filePath in Directory.EnumerateFiles(ReferencesPath, "*.dll", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) && !referenceFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    referenceFiles.Add(filePath);
                }
            }
        }

        [Option('t', HelpText="Template file name", DefaultValue="Template.proj")]
        public string TemplateFileName { get; set; }

        [Option('o', HelpText="Output file name", DefaultValue = "build.proj")]
        public string OutputFileName { get; set; }
        
        [Option('s', HelpText="Source code directory", Required = true)]
        public string SourceCodeDir { get; set; }

        [Option('r', HelpText="Reference files root directory", Required = true)]
        public string ReferencesPath { get; set; }


        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("DXBuildGenerator", GetType().Assembly.GetName().Version.ToString()),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: DXBuildGenerator -s <source directory> -r <references directory>");
            help.AddOptions(this);
            return help;
        }
        private string GetFullProjectFileName(string projectFileName)
        {
            if (!Path.IsPathRooted(projectFileName))
                projectFileName = Path.Combine(SourceCodeDir, projectFileName);

            return projectFileName;
        }

        private void ConvertTargetFiles(string taskDllPath)
        {
            string[] files = Directory.GetFiles(Path.Combine(SourceCodeDir, @"DevExpress.Xpf.Themes.SL"), "*.targets", SearchOption.AllDirectories);
            foreach (string fileName in files)
                ReplaceUsingTask(fileName, taskDllPath);
        }

        private static void ReplaceUsingTask(string xmlFileName, string taskDllPath)
        {

            var doc = XDocument.Load(xmlFileName);

            var tasks = doc.Descendants(String.Format("{{{0}}}UsingTask", doc.Root.Name.NamespaceName));
            foreach (var t in tasks)
            {
                var attr = t.Attribute("AssemblyName");
                if (attr != null) attr.Remove();
                t.SetAttributeValue("AssemblyFile", taskDllPath);
            }
            doc.Save(xmlFileName);
        }

        private static XElement CreateItem(string itemName, string include)
        {
            XElement item = new XElement(itemName);
            item.Add(new XAttribute("Include", include));
            return item;
        }
        private void ConvertProjectsToBuild(XDocument project, SortedProjects projects, string frameworkVersion, string toolsVersion, bool silverlight)
        {

            XElement itemGroup = CreateItemGroup();

            foreach (var p in projects.SortedList)
            {

                XElement projectToBuild = CreateItem("ProjectToBuild",
                    string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", RemoveBasePath(p.FullPath, SourceCodeDir)));

                if (silverlight)
                    projectToBuild.Add(new XElement("SL", "True"));
                else
                {
                    string fw = frameworkVersion;
                    if (string.IsNullOrEmpty(fw))
                        fw = GetFrameworkVersion(p);

                    projectToBuild.Add(new XElement("FrameworkVersion", fw));
                }

                if (!string.IsNullOrWhiteSpace(toolsVersion))
                    projectToBuild.Add(new XElement("ToolsVersion", toolsVersion));

                itemGroup.Add(projectToBuild);
            }


            project.Root.Add(itemGroup);

        }

        private static string GetFrameworkVersion(Project project)
        {
            if (project.IsFramework4())
                return "v4.0";
            else
                return "v3.5";
        }

        private static XElement CreateItemGroup()
        {
            XName name = XNamespace.None.GetName("ItemGroup");
            XElement itemGroup = new XElement(name);
            return itemGroup;
        }

        private static string RemoveBasePath(string path, string basePath)
        {
            if (path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return path.Substring(basePath.Length);
            else
                return path;
        }
        private void ConvertPatchInternals(XDocument project)
        {
            var infoFiles = (from fn in Directory.GetFiles(SourceCodeDir, "AssemblyInfo.cs", SearchOption.AllDirectories)
                             where File.ReadAllText(fn).Contains("InternalsVisibleTo")
                             select fn);

            XElement itemGroup = CreateItemGroup();
            foreach (string fileFullPath in infoFiles)
            {
                string fileName = RemoveBasePath(fileFullPath, SourceCodeDir);
                XElement projectToBuild = new XElement("PatchInternalsVisibleTo");
                projectToBuild.Add(new XAttribute("Include", string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", fileName)));
                itemGroup.Add(projectToBuild);
            }
            project.Root.Add(itemGroup);
        }


    }
}
