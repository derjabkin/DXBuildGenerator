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
using DXBuildGenerator.Properties;

namespace DXBuildGenerator
{
    class BuildGenerator
    {
        private readonly List<string> referenceFiles = new List<string>();
        private Assembly vsShellAssembly;
        private List<string> libraryAssemblyNames = new List<string>();
        private readonly TextWriter helpWriter;



        private BuildGenerator(TextWriter helpWriter)
        {
            this.helpWriter = helpWriter;
        }
        internal static BuildGenerator Create(Parser commandLineParser, string[] args)
        {

            if (commandLineParser == null)
                throw new ArgumentNullException("commandLineParser");

            BuildGenerator generator = new BuildGenerator(commandLineParser.Settings.HelpWriter);
            var helpWriter = generator.helpWriter;


            if (commandLineParser.ParseArguments(args, generator))
            {

                if ((string.IsNullOrWhiteSpace(generator.ReferencesPath) || string.IsNullOrWhiteSpace(generator.SourceCodeDir))
                    && string.IsNullOrEmpty(generator.DevExpressRoot))
                {
                    helpWriter.WriteLine(Resources.NoPathSpecifiedMessage);
                    helpWriter.WriteLine(Properties.Resources.UseHelpOptionForUsage);

                    return null;
                }


                if (!string.IsNullOrWhiteSpace(generator.DevExpressRoot))
                {

                    if (!CheckDirectoryExists(helpWriter, generator.DevExpressRoot)) return null;

                    generator.OutputPath = generator.ReferencesPath = Path.Combine(generator.DevExpressRoot, "Bin", "Framework");
                    generator.SourceCodeDir = Path.Combine(generator.DevExpressRoot, "Sources");

                    helpWriter.WriteLine(Resources.OriginalFilesReplacementWarning);
                }


                if (CheckDirectoryExists(helpWriter, generator.SourceCodeDir) && CheckDirectoryExists(helpWriter, generator.ReferencesPath)
                    && CheckDirectoryExistsOrEmpty(helpWriter, generator.CopyReferencesDirecotry))
                    return generator;
            }

            return null;

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
            XDocument project = XDocument.Load(TemplateFileName);

            string[] projectFiles = Directory.GetFiles(SourceCodeDir, "*.csproj", SearchOption.AllDirectories);

            vsShellAssembly = GetMicrosoftVisualStudioShellAssembly();

            var silverlightProjects = new SortedProjects();
            var windowsProjects = new SortedProjects();

            if (!string.IsNullOrWhiteSpace(OutputPath))
                SetPropertyValue(project, "OutputPath", OutputPath);

            SetPropertyValue(project, "DevExpressSourceDir", SourceCodeDir);
            SetPropertyValue(project, "TasksAssembly", GetType().Assembly.Location);

            foreach (var projectFile in projectFiles)
            {
                bool added = false;

                ProjectInfo pi = GetProjectInfo(projectFile);
                if (!(pi.IsSilverlight && SkipSilverlightProjects) &&
                    !(pi.IsTest && SkipTestProjects) &&
                    !(pi.IsMvc && SkipMvcProjects) &&
                    !(pi.IsWinRT && SkipWinRTProjects) && 
                    !pi.IsUwp && !pi.IsCodedUITests)
                {

                    Project p = TryOpenProject(projectFile);
                    //TODO: Get rid of hard-coded exclusions
                    if (p != null && !p.GetAssemblyName().Contains("SharePoint"))
                    {
                        if (pi.IsSilverlight)
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
                            if (p.GetPropertyValue("OutputType") == "Library")
                                libraryAssemblyNames.Add(p.GetAssemblyName());
                            added = true;
                        }
                    }
                }

                if (!added)
                {


                    if (!pi.IsSilverlight)
                        windowsProjects.AddExcluded(pi.AssemblyName);
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

            ConvertProjectsToBuild(project, silverlightProjects, "4.0", true);
            ConvertProjectsToBuild(project, windowsProjects, "4.0", false);
            CreateAssemblyNamesItems(project);


            File.WriteAllText(OutputFileName, project.ToString().Replace("<ItemGroup xmlns=\"\">", "<ItemGroup>"));
        }


        private static Project TryOpenProject(string projectFileName)
        {
            try
            {
                Dictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties["VisualStudioVersion"] = "14.0";   
                return new Project(projectFileName, globalProperties, "14.0");
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException ex)
            {
                return null;
            }

        }
        private void ResolvePaths()
        {
            if (!string.IsNullOrWhiteSpace(DevExpressRoot))
            {

            }
        }
        private void CreateAssemblyNamesItems(XDocument project)
        {
            var itemGroup = CreateItemGroup();
            foreach (var name in libraryAssemblyNames)
            {
                itemGroup.Add(CreateItem("OutputAssembly", string.Format(CultureInfo.InvariantCulture, @"$(OutputPath)\{0}.dll", name)));
            }

            project.Root.Add(itemGroup);
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
            result.IsSilverlight = Path.GetFileNameWithoutExtension(path).EndsWith(".SL", StringComparison.OrdinalIgnoreCase) ||
                GetPropertyValue(projectDoc, "TargetFrameworkIdentifier") == "Silverlight" ||
                GetPropertyValue(projectDoc, "BaseIntermediateOutputPath").Contains("obj.SL");

            string projectTypes = GetPropertyValue(projectDoc, "ProjectTypeGuids");

            result.IsTest = ContainsProjectType(projectTypes, "3AC096D0-A1C2-E12C-1390-A8335801FDAB");
            result.IsWinRT = ContainsProjectType(projectTypes, "BC8A1FFA-BEE3-4634-8014-F334798102B3");
            result.IsMvc = projectDoc.Root.Descendants().Any(e => e.Name.LocalName == "Reference" &&
                                                                  e.Parent.Name.LocalName == "ItemGroup" &&
                                                                  e.Attributes().Any(a => a.Name.LocalName == "Include" &&
                                                                  a.Value.StartsWith("System.Web.Mvc", StringComparison.OrdinalIgnoreCase)));
            result.IsCodedUITests = ContainsProjectType(projectTypes, "3AC096D0-A1C2-E12C-1390-A8335801FDAB");

            result.AssemblyName = GetPropertyValue(projectDoc, "AssemblyName");
            string targetPlatfrom = GetPropertyValue(projectDoc, "TargetPlatformIdentifier");
            result.IsUwp = targetPlatfrom == "UAP" || Path.GetFileName(path).Contains(".UWP.");
            return result;
        }


        private string MakeRelativeReferenceFilePath(string referenceFileName)
        {
            if (string.IsNullOrEmpty(CopyReferencesDirecotry))
                return referenceFileName;

            string path = Path.GetDirectoryName(referenceFileName);
            string fileName = Path.GetFileName(referenceFileName);
            File.Copy(referenceFileName, Path.Combine(CopyReferencesDirecotry, fileName), true);
            return Path.Combine(Utils.MakeRelativePath(
                Path.GetDirectoryName(Path.GetFullPath(OutputFileName)), CopyReferencesDirecotry),
                fileName);
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

        [Option('x', HelpText = "Path to the DevExpress installation folder." +
            "If this option is specified, Source code directory,  references directory and the output path are determinated automatically.")]
        public string DevExpressRoot { get; set; }

        [Option("op", HelpText = "Output path for the compiled assemblies. If the value is not specified, the property value from the template will be used.")]
        public string OutputPath { get; set; }

        [Option('t', HelpText = "Template file name", DefaultValue = "Template.proj")]
        public string TemplateFileName { get; set; }

        [Option('o', HelpText = "Output file name", DefaultValue = "build.proj")]
        public string OutputFileName { get; set; }

        [Option('s', HelpText = "Source code directory")]
        public string SourceCodeDir { get; set; }

        [Option('r', HelpText = "Reference files root directory")]
        public string ReferencesPath { get; set; }

        [Option("nosl", HelpText = "Skip silverlight projects")]
        public bool SkipSilverlightProjects { get; set; }

        [Option("notest", HelpText = "Skip test projects")]
        public bool SkipTestProjects { get; set; }

        [Option("nomvc", HelpText = "Skip ASP.NET MVC projects")]
        public bool SkipMvcProjects { get; set; }

        [Option("nowinrt", HelpText = "Skip WinRT (Windows 8) projects")]
        public bool SkipWinRTProjects { get; set; }

        [Option("copyrefdir", HelpText = "Reference files will be copied in the specified directory")]
        public string CopyReferencesDirecotry { get; set; }

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
            help.AddPreOptionsLine("Or: DXBuildGenerator -x <devexpress root directory>");
            help.AddPreOptionsLine("\r\nExample DXGenerator -x \"c:\\Program Files (x86)\\DevExpress\\DXperience 12.2\"");
            help.AddOptions(this);
            return help;
        }

        private void ConvertTargetFiles(string taskDllPath)
        {
            string[] files = Directory.GetFiles(SourceCodeDir, "*.targets", SearchOption.AllDirectories);
            foreach (string fileName in files)
                ReplaceUsingTask(fileName, taskDllPath);
        }

        private static void ReplaceUsingTask(string xmlFileName, string taskDllPath)
        {

            var doc = XDocument.Load(xmlFileName);

            bool changed = false;
            var tasks = doc.Descendants(String.Format("{{{0}}}UsingTask", doc.Root.Name.NamespaceName));
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
        private void ConvertProjectsToBuild(XDocument project, SortedProjects projects, string toolsVersion, bool silverlight)
        {
            if (projects.SortedList.Count == 0) return;

            XElement itemGroup = CreateItemGroup();

            foreach (var p in projects.SortedList)
            {

                XElement projectToBuild = CreateItem("ProjectToBuild",
                    string.Format(CultureInfo.InvariantCulture, "$(DevExpressSourceDir)\\{0}", Utils.MakeRelativePath(SourceCodeDir, p.FullPath)));

                if (silverlight)
                    projectToBuild.Add(new XElement("SL", "True"));
                else
                {
                    projectToBuild.Add(new XElement("FrameworkVersion", GetFrameworkVersion(p)));
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

        private static bool ShouldPatchInternals(string fileName)
        {
            string content = File.ReadAllText(fileName);
            return content.Contains("InternalsVisibleTo") || content.Contains("PublicKeyToken");
        }

        private void ConvertPatchInternals(XDocument project)
        {
            var infoFiles = Directory.GetFiles(SourceCodeDir, "Assembly*.cs", SearchOption.AllDirectories).Where(ShouldPatchInternals);

            XElement itemGroup = CreateItemGroup();
            foreach (string fileFullPath in infoFiles)
            {
                string fileName = Utils.MakeRelativePath(SourceCodeDir, fileFullPath);
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

        private static string MakeShortReference(string reference)
        {
            AssemblyName an = new AssemblyName(reference);
            return an.Name;
        }

        private static Assembly GetMicrosoftVisualStudioShellAssembly()
        {
            string dir = Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL\Microsoft.VisualStudio.Shell");
            if (Directory.Exists(dir))
            {
                string versionDir = Directory.GetDirectories(dir).OrderBy(d => d).First();
                return Assembly.ReflectionOnlyLoadFrom(Directory.GetFiles(versionDir).Single());
            }
            else
                return null;
        }
    }
}
