using CommandLine;
using DXBuildGenerator.Properties;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace DXBuildGenerator
{
    partial class BuildGenerator
    {


        private readonly List<string> referenceFiles = new List<string>();
        private readonly TextWriter helpWriter;
        private readonly Dictionary<string, string> assemblyProjectFolders = new();

        private BuildGenerator(CommandLineOptions options, TextWriter helpWriter)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            this.helpWriter = helpWriter;
        }

        public CommandLineOptions Options { get; }

        internal static BuildGenerator Create(CommandLineOptions options, TextWriter helpWriter)
        {
            if ((string.IsNullOrWhiteSpace(options.ReferencesPath) || string.IsNullOrWhiteSpace(options.SourceCodeDir))
                && string.IsNullOrEmpty(options.DevExpressRoot))
            {
                helpWriter.WriteLine(Resources.NoPathSpecifiedMessage);
                helpWriter.WriteLine(Properties.Resources.UseHelpOptionForUsage);
                return null;
            }


            if (!string.IsNullOrWhiteSpace(options.DevExpressRoot))
            {

                if (!CheckDirectoryExists(helpWriter, options.DevExpressRoot)) return null;

                options.OutputPath = options.ReferencesPath = Path.Combine(options.DevExpressRoot, "Bin", "Framework");
                options.SourceCodeDir = Path.Combine(options.DevExpressRoot, "Sources");

                helpWriter.WriteLine(Resources.OriginalFilesReplacementWarning);
            }


            if (CheckDirectoryExists(helpWriter, options.SourceCodeDir) && CheckDirectoryExists(helpWriter, options.ReferencesPath)
                && CheckDirectoryExistsOrEmpty(helpWriter, options.CopyReferencesDirecotry))
            {
                return new BuildGenerator(options, helpWriter);
            }

            return null;
        }
        internal static BuildGenerator Create(Parser commandLineParser, string[] args)
        {

            if (commandLineParser == null)
                throw new ArgumentNullException(nameof(commandLineParser));

            var result = commandLineParser.ParseArguments<CommandLineOptions>(args);
            var helpWriter = commandLineParser.Settings.HelpWriter;
            BuildGenerator generator = null;
            result.WithParsed(options => generator = Create(options, helpWriter));
            return generator;

        }


        internal static BuildGenerator Create(string[] args)
        {

            return Create(Parser.Default, args);
        }

        private static bool CheckDirectoryExistsOrEmpty(TextWriter helpWriter, string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;
            else
                return CheckDirectoryExists(helpWriter, path);
        }
        private static bool CheckDirectoryExists(TextWriter helpWriter, string path)
        {
            if (!Directory.Exists(path))
            {
                helpWriter.WriteLine(Resources.DirectoryNotFound, path);
                return false;
            }
            else
                return true;
        }


        internal void Generate()
        {
            XDocument project = XDocument.Load(Options.TemplateFileName);
            string[] projectFiles = Directory.GetFiles(Options.SourceCodeDir, "*.csproj", SearchOption.AllDirectories);

            var projects = new SortedDictionary<ProjectPlatform, SortedProjects>();
            if (!string.IsNullOrWhiteSpace(Options.OutputPath))
                SetPropertyValue(project, "OutputPath", Options.OutputPath);

            SetPropertyValue(project, "DevExpressSourceDir", Options.SourceCodeDir);
            SetPropertyValue(project, "TasksAssembly", Utils.MakeRelativePath(Path.GetDirectoryName(Path.GetFullPath(Options.OutputFileName)), GetType().Assembly.Location));

            foreach (var projectFile in projectFiles)
            {
                ProjectInfo pi = GetProjectInfo(projectFile);
                if (!projects.TryGetValue(pi.Platform, out var platformProjects))
                {
                    platformProjects = projects[pi.Platform] = new SortedProjects();
                }

                if (IsNotExcluded(pi))
                {

                    Project p = TryOpenProject(projectFile);
                    pi.MSBuildProject = p;
                    //TODO: Get rid of hard-coded exclusions
                    if (p != null && !p.GetAssemblyName().Contains("SharePoint"))
                    {
                        platformProjects.Add(pi);
                        if (!string.IsNullOrEmpty(pi.AssemblyName))
                            assemblyProjectFolders[pi.AssemblyName] = Path.GetDirectoryName(projectFile);
                    }
                }
            }


            referenceFiles.Clear();

            foreach (var sortedProjects in projects.Values)
            {
                sortedProjects.Sort();
                foreach (var prj in sortedProjects.SortedList)
                {
                    UpdateProjectReferences(prj.MSBuildProject);
                }

                PopulateReferenceFiles(sortedProjects.UnknownReferences);
                ConvertProjectsToBuild(project, sortedProjects);
            }

            string entityFrameworkFileName = Path.Combine(Options.ReferencesPath, "EntityFramework.dll");
            if (File.Exists(entityFrameworkFileName))
                referenceFiles.Add(entityFrameworkFileName);

            string xamlResProcDll = Directory.GetFiles(Options.ReferencesPath, "DevExpress.Build.XamlResourceProcessing*.dll", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(xamlResProcDll))
            {
                referenceFiles.Add(xamlResProcDll);
                ConvertTargetFiles(
                    Path.Combine(@"$(BuildTasksPath)", Path.GetFileName(xamlResProcDll)));
            }
            else
                Console.WriteLine("WARNING: DevExpress.Build.XamlResourceProcessing not found.");

            CreateReferenceFilesGroup(project);
            ConvertPatchInternals(project);


            File.WriteAllText(Options.OutputFileName, project.ToString().Replace("<ItemGroup xmlns=\"\">", "<ItemGroup>"));

            if (!string.IsNullOrEmpty(Options.AssemblyFoldersFile))
            {
                File.WriteAllLines(Options.AssemblyFoldersFile, assemblyProjectFolders.Select(kv => $"{kv.Key}:{kv.Value}").ToArray());
            }
        }

        private bool IsNotExcluded(ProjectInfo pi)
        {
            return !(pi.IsTest && Options.SkipTestProjects) &&
                                !(pi.IsMvc && Options.SkipMvcProjects) &&
                                !(pi.IsWinRT && Options.SkipWinRTProjects) &&
                                !pi.IsCodedUITests;
        }

        private Project TryOpenProject(string projectFileName)
        {
            try
            {
                helpWriter.WriteLine($"Loading {projectFileName}");
                return new Project(projectFileName, new Dictionary<string, string>(), "Current", new ProjectCollection(), ProjectLoadSettings.IgnoreMissingImports);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }

        }
        private void ResolvePaths()
        {
            if (!string.IsNullOrWhiteSpace(Options.DevExpressRoot))
            {

            }
        }

        private static void SetPropertyValue(XDocument project, string propertyName, string value)
        {
            var propertyElement = GetPropertyElement(project, propertyName);
            if (propertyElement == null)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' not found.", propertyName), "propertyName");

            propertyElement.SetValue(value);
        }

        private static string GetPropertyValue(XDocument project, string propertyName)
        {

            var propertyElement = GetPropertyElement(project, propertyName);
            if (propertyElement != null)
                return propertyElement.Value;
            else
                return string.Empty;
        }

        private static XElement GetPropertyElement(XDocument project, string propertyName)
        {
            var property = (from e in project.Root.Descendants()
                            where e.Name.LocalName == propertyName &&
                            e.Parent.Name.LocalName == "PropertyGroup"
                            select e).FirstOrDefault();
            return property;
        }

        private void CreateReferenceFilesGroup(XDocument project)
        {
            XElement itemGroup = CreateItemGroup();
            foreach (var referenceFile in referenceFiles)
            {
                itemGroup.Add(CreateItem("ReferenceFile", MakeRelativeReferenceFilePath(referenceFile)));
            }
            project.Root.Add(itemGroup);
        }

        private static bool ContainsProjectType(string projectTypes, string projectTypeGuid)
        {
            return projectTypes != null && projectTypes.IndexOf(projectTypeGuid, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static ProjectInfo GetProjectInfo(string path)
        {

            ProjectInfo result = new ProjectInfo();

            XDocument projectDoc = XDocument.Load(path);
            string projectName = Path.GetFileNameWithoutExtension(path);
            string projectTypes = GetPropertyValue(projectDoc, "ProjectTypeGuids");

            result.IsTest = ContainsProjectType(projectTypes, "3AC096D0-A1C2-E12C-1390-A8335801FDAB");
            result.IsWinRT = ContainsProjectType(projectTypes, "BC8A1FFA-BEE3-4634-8014-F334798102B3") ||
                string.Equals(GetPropertyValue(projectDoc, "BaseIntermediateOutputPath"), "obj.RT", StringComparison.OrdinalIgnoreCase);

            result.IsMvc = projectDoc.Root.Descendants().Any(e => e.Name.LocalName == "Reference" &&
                                                                  e.Parent.Name.LocalName == "ItemGroup" &&
                                                                  e.Attributes().Any(a => a.Name.LocalName == "Include" &&
                                                                  a.Value.StartsWith("System.Web.Mvc", StringComparison.OrdinalIgnoreCase)));
            result.IsCodedUITests = projectName.StartsWith("CodedUIExtension"); ContainsProjectType(projectTypes, "3AC096D0-A1C2-E12C-1390-A8335801FDAB");

            result.AssemblyName = GetPropertyValue(projectDoc, "AssemblyName");
            string targetPlatfrom = GetPropertyValue(projectDoc, "TargetPlatformIdentifier");
            string targetFramework = GetPropertyValue(projectDoc, "TargetFramework");

            var platformAndFramework = GetPlatformAndFramework(targetFramework);
            result.Platform = platformAndFramework.platform;
            result.FrameworkVersion = platformAndFramework.frameworkVersion;

            if (path.Contains(@"\XPF\", StringComparison.OrdinalIgnoreCase))
                result.IsXpf = true;
            return result;
        }

        public static (ProjectPlatform platform, string frameworkVersion) GetPlatformAndFramework(string targetFramework)
        {
            if (targetFramework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
                return (ProjectPlatform.Standard, null);
            else if (targetFramework.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
            {
                return (ProjectPlatform.NetCore, null);

            }
            else if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Compare(targetFramework, "net5") >= 0)
                    return (ProjectPlatform.NetCore, null);
                else
                    return (ProjectPlatform.Windows, null);

            }
            else
            {
                return (ProjectPlatform.Windows, "v4.6");
            }

        }

        private string MakeRelativeReferenceFilePath(string referenceFileName)
        {
            if (string.IsNullOrEmpty(Options.CopyReferencesDirecotry))
                return referenceFileName;

            string path = Path.GetDirectoryName(referenceFileName);
            string fileName = Path.GetFileName(referenceFileName);
            File.Copy(referenceFileName, Path.Combine(Options.CopyReferencesDirecotry, fileName), true);
            return Path.Combine(Utils.MakeRelativePath(
                Path.GetDirectoryName(Path.GetFullPath(Options.OutputFileName)), Options.CopyReferencesDirecotry),
                fileName);
        }

        private void PopulateReferenceFiles(IEnumerable<string> references)
        {
            string[] fileNames = references.Select(r => r += ".dll").ToArray();
            foreach (string filePath in Directory.EnumerateFiles(Options.ReferencesPath, "*.dll", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) && !referenceFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    referenceFiles.Add(filePath);
                }
            }
        }


        private void ConvertTargetFiles(string taskDllPath)
        {
            string[] files = Directory.GetFiles(Options.SourceCodeDir, "*.targets", SearchOption.AllDirectories);
            foreach (string fileName in files)
                ReplaceUsingTask(fileName, taskDllPath);
        }

        private static void ReplaceUsingTask(string xmlFileName, string taskDllPath)
        {

            var doc = XDocument.Load(xmlFileName);

            bool changed = false;
            var tasks = doc.Descendants().Where(d => d.Name.LocalName == "UsingTask");
            foreach (var t in tasks)
            {
                var attr = t.Attribute("AssemblyName");
                if (attr == null)
                    attr = t.Attribute("AssemblyFile");


                if (attr != null && Path.GetFileName(attr.Value).StartsWith("DevExpress.Build.XamlResourceProcessing", StringComparison.OrdinalIgnoreCase))
                {
                    attr.Remove();
                    t.SetAttributeValue("AssemblyFile", taskDllPath);
                    changed = true;
                }
            }
            if (changed)
                doc.Save(xmlFileName);
        }

        private static XElement CreateItem(string itemName, string include)
        {
            XElement item = new XElement(itemName);
            item.Add(new XAttribute("Include", include));
            return item;
        }
        private void ConvertProjectsToBuild(XDocument project, SortedProjects projects)
        {
            if (projects.SortedList.Count == 0) return;

            XElement itemGroup = CreateItemGroup();

            foreach (var p in projects.SortedList)
            {

                XElement projectToBuild = CreateItem("ProjectToBuild",
                    string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", Utils.MakeRelativePath(Options.SourceCodeDir, p.MSBuildProject.FullPath)));

                projectToBuild.Add(new XElement("Platform", p.Platform.ToString()));
                if (!string.IsNullOrWhiteSpace(p.FrameworkVersion))
                    projectToBuild.Add(new XElement("FrameworkVersion", p.FrameworkVersion));

                if (p.IsXpf)
                    projectToBuild.Add(new XElement("Product", "Xpf"));

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

        private static bool ShouldPatchInternals(string fileName)
        {
            string content = File.ReadAllText(fileName);
            return content.Contains("InternalsVisibleTo") || content.Contains("PublicKeyToken");
        }

        private void ConvertPatchInternals(XDocument project)
        {
            var infoFiles = Directory.GetFiles(Options.SourceCodeDir, "Assembly*.cs", SearchOption.AllDirectories).Where(ShouldPatchInternals);

            XElement itemGroup = CreateItemGroup();
            foreach (string fileFullPath in infoFiles)
            {
                string fileName = Utils.MakeRelativePath(Options.SourceCodeDir, fileFullPath);
                XElement projectToBuild = new XElement("PatchInternalsVisibleTo");
                projectToBuild.Add(new XAttribute("Include", string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", fileName)));
                itemGroup.Add(projectToBuild);
            }
            project.Root.Add(itemGroup);
        }


        private static bool CanSubstituteReference(Project p, string reference)
        {
            string projectFrameworkVersion = GetFrameworkVersion(p);

            AssemblyName name = new AssemblyName(reference);
            try
            {
                var asm = Assembly.LoadWithPartialName(name.Name);
                if (asm != null)
                {
                    return asm.ImageRuntimeVersion.Substring(0, projectFrameworkVersion.Length).CompareTo(projectFrameworkVersion) <= 0;

                }
                else
                    return false;
            }
            catch (IOException)
            {
                return false;
            }

        }


        private static void UpdateProjectReferences(Project p)
        {
            //Making references to VisualStudio assemblies version-neutral.
            bool shouldSaveProject = false;
            foreach (var r in p.GetItems("Reference"))
            {
                if (r.EvaluatedInclude.StartsWith("Microsoft.VisualStudio.", StringComparison.OrdinalIgnoreCase)
                    && !r.EvaluatedInclude.StartsWith("Microsoft.VisualStudio.Shell", StringComparison.OrdinalIgnoreCase)
                    && !CanSubstituteReference(p, r.EvaluatedInclude))
                {

                    r.UnevaluatedInclude = MakeShortReference(r.EvaluatedInclude);
                    shouldSaveProject = true;
                }
            }
            if (shouldSaveProject) p.Save();
        }

        private static string MakeShortReference(string reference) => new AssemblyName(reference).Name;

    }

}
