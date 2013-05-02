using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CommandLine;
using CommandLine.Text;
using Microsoft.Build.Evaluation;
using CmdToMSBuild;

namespace DXBuildGenerator {
    class BuildGenerator {
        private readonly List<string> referenceFiles = new List<string>();
        private Assembly vsShellAssembly;
        private List<string> libraryAssemblyNames = new List<string>();

        internal void Generate() {
            XDocument project = XDocument.Load(TemplateFileName);

            string[] projectFiles = Directory.GetFiles(SourceCodeDir, "*.csproj", SearchOption.AllDirectories);

            vsShellAssembly = GetMicrosoftVisualStudioShellAssembly();

            var silverlightProjects = new SortedProjects();
            var windowsProjects = new SortedProjects();
            List<Project> excludedProjects = new List<Project>();

            if (!string.IsNullOrWhiteSpace(OutputPath))
                SetPropertyValue(project, "OutputPath", OutputPath);

            SetPropertyValue(project, "DevExpressSourceDir", SourceCodeDir);
            SetPropertyValue(project, "TasksAssembly", GetType().Assembly.Location);

            foreach (var projectFile in projectFiles) {
                bool added = false;

                ProjectInfo pi = GetProjectInfo(projectFile);
                if (!(pi.IsSilverlight && SkipSilverlightProjects) && !(pi.IsTest && SkipTestProjects) && !(pi.IsMvc && SkipMvcProjects)) {

                    Project p = new Project(projectFile);
                    //TODO: Get rid of hard-coded exclusions
                    if (!p.GetAssemblyName().Contains("SharePoint") && !p.FullPath.Contains("DevExpress.Xpo.Extensions.csproj")) {
                        if (pi.IsSilverlight) {
                            if (!p.FullPath.Contains(".DemoBase.")) {
                                silverlightProjects.Add(p);
                                added = true;
                            }
                        }
                        else {
                            windowsProjects.Add(p);
                            libraryAssemblyNames.Add(p.GetAssemblyName());
                            added = true;
                        }
                    }
                    if (!added)
                        excludedProjects.Add(p);
                }

            }

            silverlightProjects.Sort();
            windowsProjects.Sort();

            foreach (var p in windowsProjects.SortedList)
                UpdateProjectReferences(p);

            referenceFiles.Clear();
            PopulateReferenceFiles(silverlightProjects.UnknownReferences);
            PopulateReferenceFiles(windowsProjects.UnknownReferences);
            referenceFiles.Add(typeof(System.Data.Entity.DbContext).Assembly.Location);
            string xamlResProcDll = Directory.GetFiles(ReferencesPath, "DevExpress.Build.XamlResourceProcessing*.dll", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(xamlResProcDll)) {
                referenceFiles.Add(xamlResProcDll);
                ConvertTargetFiles(
                    Path.Combine(@"$(BuildTasksPath)", Path.GetFileName(xamlResProcDll)));
            }
            else
                Console.WriteLine("WARNING: DevExpress.Build.XamlResourceProcessing not found.");

            CreateReferenceFilesGroup(project);
            ConvertPatchInternals(project);

            ConvertProjectsToBuild(project, silverlightProjects, null, "4.0", true);
            ConvertProjectsToBuild(project, windowsProjects, null, "4.0", false);
            CreateAssemblyNamesItems(project);


            File.WriteAllText(OutputFileName, project.ToString().Replace("<ItemGroup xmlns=\"\">", "<ItemGroup>"));
        }


        internal void ResolvePaths() {
            if (!string.IsNullOrWhiteSpace(DevExpressRoot)) {
                OutputPath = ReferencesPath = Path.Combine(DevExpressRoot, "Bin", "Framework");
                SourceCodeDir = Path.Combine(DevExpressRoot, "Sources");
            }
        }
        private void CreateAssemblyNamesItems(XDocument project) {
            var itemGroup = CreateItemGroup();
            foreach (var name in libraryAssemblyNames) {
                itemGroup.Add(CreateItem("OutputAssembly", string.Format(CultureInfo.InvariantCulture, @"$(OutputPath)\{0}.dll", name)));
            }

            project.Root.Add(itemGroup);
        }

        private static void SetPropertyValue(XDocument project, string propertyName, string value) {
            var propertyElement = GetPropertyElement(project, propertyName);
            if (propertyElement == null)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' not found.", propertyName), "propertyName");

            propertyElement.SetValue(value);
        }

        private static string GetPropertyValue(XDocument project, string propertyName) {

            var propertyElement = GetPropertyElement(project, propertyName);
            if (propertyElement != null)
                return propertyElement.Value;
            else
                return string.Empty;
        }

        private static XElement GetPropertyElement(XDocument project, string propertyName) {
            var property = (from e in project.Root.Descendants()
                            where e.Name.LocalName == propertyName &&
                            e.Parent.Name.LocalName == "PropertyGroup"
                            select e).FirstOrDefault();
            return property;
        }

        private void CreateReferenceFilesGroup(XDocument project) {
            XElement itemGroup = CreateItemGroup();
            foreach (var referenceFile in referenceFiles) {
                itemGroup.Add(CreateItem("ReferenceFile", referenceFile));
            }
            project.Root.Add(itemGroup);
        }

        private static ProjectInfo GetProjectInfo(string path) {

            ProjectInfo result = new ProjectInfo();

            XDocument projectDoc = XDocument.Load(path);
            result.IsSilverlight = Path.GetFileNameWithoutExtension(path).EndsWith(".SL", StringComparison.OrdinalIgnoreCase) ||
                GetPropertyValue(projectDoc, "TargetFrameworkIdentifier") == "Silverlight" ||
                GetPropertyValue(projectDoc, "BaseIntermediateOutputPath").Contains("obj.SL");

            string projectTypes = GetPropertyValue(projectDoc, "ProjectTypeGuids");

            result.IsTest = projectTypes != null && projectTypes.IndexOf("3AC096D0-A1C2-E12C-1390-A8335801FDAB", StringComparison.OrdinalIgnoreCase) >= 0;

            result.IsMvc = projectDoc.Root.Descendants().Where(e => e.Name.LocalName == "Reference" &&
                e.Parent.Name.LocalName == "ItemGroup" &&
                e.Attributes().Where(a => a.Name.LocalName == "Include" && a.Value.StartsWith("System.Web.Mvc", StringComparison.OrdinalIgnoreCase)).Any()).Any();

            return result;
        }


        private void PopulateReferenceFiles(IEnumerable<string> references) {
            string[] fileNames = references.Select(r => r += ".dll").ToArray();
            foreach (string filePath in Directory.EnumerateFiles(ReferencesPath, "*.dll", SearchOption.AllDirectories)) {
                string fileName = Path.GetFileName(filePath);
                if (fileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) && !referenceFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)) {
                    referenceFiles.Add(filePath);
                }
            }
        }

        [Option("dx", HelpText = "Path to the DevExpress installation folder.\r\n" +
            "If this option is specified, Source code directory,  references directory and the output path are determinated automatically.\r\n" +
            "Example DXGenerator -dx \"c:\\Program Files (x86)\\DevExpress\\DXperience 12.2\"")]
        public string DevExpressRoot { get; set; }

        [Option("op", HelpText = "Output path for the compiled assemblies. If the value is not specified, the property value from the template will be used.")]
        public string OutputPath { get; set; }

        [Option('t', HelpText = "Template file name", DefaultValue = "Template.proj")]
        public string TemplateFileName { get; set; }

        [Option('o', HelpText = "Output file name", DefaultValue = "build.proj")]
        public string OutputFileName { get; set; }

        [Option('s', HelpText = "Source code directory", Required = true)]
        public string SourceCodeDir { get; set; }

        [Option('r', HelpText = "Reference files root directory", Required = true)]
        public string ReferencesPath { get; set; }

        [Option("nosl", HelpText = "Skip silverlight projects")]
        public bool SkipSilverlightProjects { get; set; }

        [Option("notest", HelpText = "Skip test projects")]
        public bool SkipTestProjects { get; set; }

        [Option("nomvc", HelpText = "Skip ASP.NET MVC projects")]
        public bool SkipMvcProjects { get; set; }

        [HelpOption]
        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("DXBuildGenerator", GetType().Assembly.GetName().Version.ToString()),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: DXBuildGenerator -s <source directory> -r <references directory>");
            help.AddOptions(this);
            return help;
        }

        private void ConvertTargetFiles(string taskDllPath) {
            string[] files = Directory.GetFiles(Path.Combine(SourceCodeDir, @"DevExpress.Xpf.Themes.SL"), "*.targets", SearchOption.AllDirectories);
            foreach (string fileName in files)
                ReplaceUsingTask(fileName, taskDllPath);
        }

        private static void ReplaceUsingTask(string xmlFileName, string taskDllPath) {

            var doc = XDocument.Load(xmlFileName);

            var tasks = doc.Descendants(String.Format("{{{0}}}UsingTask", doc.Root.Name.NamespaceName));
            foreach (var t in tasks) {
                var attr = t.Attribute("AssemblyName");
                if (attr != null) attr.Remove();
                t.SetAttributeValue("AssemblyFile", taskDllPath);
            }
            doc.Save(xmlFileName);
        }

        private static XElement CreateItem(string itemName, string include) {
            XElement item = new XElement(itemName);
            item.Add(new XAttribute("Include", include));
            return item;
        }
        private void ConvertProjectsToBuild(XDocument project, SortedProjects projects, string frameworkVersion, string toolsVersion, bool silverlight) {
            if (projects.SortedList.Count == 0) return;

            XElement itemGroup = CreateItemGroup();

            foreach (var p in projects.SortedList) {

                XElement projectToBuild = CreateItem("ProjectToBuild",
                    string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", RemoveBasePath(p.FullPath, SourceCodeDir)));

                if (silverlight)
                    projectToBuild.Add(new XElement("SL", "True"));
                else {
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

        private static string GetFrameworkVersion(Project project) {
            if (project.IsFramework4())
                return "v4.0";
            else
                return "v3.5";
        }

        private static XElement CreateItemGroup() {
            XName name = XNamespace.None.GetName("ItemGroup");
            XElement itemGroup = new XElement(name);
            return itemGroup;
        }

        private static string RemoveBasePath(string path, string basePath) {
            if (path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return path.Substring(basePath.Length);
            else
                return path;
        }
        private void ConvertPatchInternals(XDocument project) {
            var infoFiles = (from fn in Directory.GetFiles(SourceCodeDir, "AssemblyInfo.cs", SearchOption.AllDirectories)
                             where File.ReadAllText(fn).Contains("InternalsVisibleTo")
                             select fn);

            XElement itemGroup = CreateItemGroup();
            foreach (string fileFullPath in infoFiles) {
                string fileName = RemoveBasePath(fileFullPath, SourceCodeDir);
                XElement projectToBuild = new XElement("PatchInternalsVisibleTo");
                projectToBuild.Add(new XAttribute("Include", string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", fileName)));
                itemGroup.Add(projectToBuild);
            }
            project.Root.Add(itemGroup);
        }


        private static bool CanSubstituteReference(Project p, string reference) {
            string projectFrameworkVersion = GetFrameworkVersion(p);

            AssemblyName name = new AssemblyName(reference);
            try {
                var asm = Assembly.LoadWithPartialName(name.Name);
                if (asm != null) {
                    return asm.ImageRuntimeVersion.Substring(0, projectFrameworkVersion.Length).CompareTo(projectFrameworkVersion) <= 0;

                }
                else
                    return false;
            }
            catch (IOException) {
                return false;
            }

        }

        private static void UpdateProjectReferences(Project p) {
            //Making references to VisualStudio assemblies version-neutral.
            bool shouldSaveProject = false;
            foreach (var r in p.GetItems("Reference")) {
                if (r.EvaluatedInclude.StartsWith("Microsoft.VisualStudio.", StringComparison.OrdinalIgnoreCase)
                    && !r.EvaluatedInclude.StartsWith("Microsoft.VisualStudio.Shell", StringComparison.OrdinalIgnoreCase)
                    && !CanSubstituteReference(p, r.EvaluatedInclude)) {

                    r.UnevaluatedInclude = MakeShortReference(r.EvaluatedInclude);
                    shouldSaveProject = true;
                }
            }
            if (shouldSaveProject) p.Save();
        }

        private static string MakeShortReference(string reference) {
            AssemblyName an = new AssemblyName(reference);
            return an.Name;
        }

        private static Assembly GetMicrosoftVisualStudioShellAssembly() {
            string dir = Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL\Microsoft.VisualStudio.Shell");
            if (Directory.Exists(dir)) {
                string versionDir = Directory.GetDirectories(dir).OrderBy(d => d).First();
                return Assembly.ReflectionOnlyLoadFrom(Directory.GetFiles(versionDir).Single());
            }
            else
                return null;
        }
    }
}
