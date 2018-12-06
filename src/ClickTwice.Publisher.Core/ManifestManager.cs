using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using ClickTwice.Publisher.Core.Manifests;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ClickTwice.Publisher.Core
{
    public class ManifestManager : Manager
    {
        private ManifestManager(string projectFilePath, InformationSource source = InformationSource.AssemblyInfo) : base(projectFilePath)
        {
            Source = source;
        }

        public ManifestManager(string projectFilePath, string applicationManifestFilePath, InformationSource source = InformationSource.AppManifest) : base(projectFilePath)
        {
            Source = source;
            if (applicationManifestFilePath.EndsWith(".application"))
            {
                ApplicationManifestLocation = applicationManifestFilePath;
            }
            else
            {
                var di = new DirectoryInfo(applicationManifestFilePath);
                ApplicationManifestLocation = di.GetFiles("*.application").First().FullName;
            }
        }

        private string ApplicationManifestLocation { get; set; }

        private InformationSource Source { get; set; }

        public AppManifest CreateAppManifest()
        {
            AppManifest manifest;
            switch (Source)
            {
                case InformationSource.AssemblyInfo:
                    manifest = CreateFromAssemblyInfo();
                    break;
                case InformationSource.AppManifest:
                    manifest = CreateFromDeployManifest();
                    break;
                case InformationSource.None:
                    manifest = new AppManifest();
                    break;
                case InformationSource.Both:
                    manifest = CreateFromAssemblyInfo();
                    manifest = CreateFromDeployManifest(manifest);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return manifest;
        }

        private AppManifest CreateFromDeployManifest(AppManifest manifest = null)
        {
            manifest = manifest ?? new AppManifest();
            var xdoc = XDocument.Load(ApplicationManifestLocation);
            var descEl = xdoc.XPathSelectElement("//*[local-name()='description']");
            manifest.ApplicationName = descEl.FindAttribute("product");
            manifest.PublisherName = descEl.FindAttribute("publisher");
            manifest.SuiteName = descEl.FindAttribute("suiteName");
            var identEl = xdoc.XPathSelectElement("//*[local-name()='assemblyIdentity']");
            manifest.AppVersion = new Version(identEl.FindAttribute("version"));
            manifest.ShortName = identEl.FindAttribute("name").Split('.').First();
            var frameworksRoot = xdoc.XPathSelectElement("//*[local-name()='compatibleFrameworks']");
            manifest.FrameworkVersion = new Version(frameworksRoot.Elements().OrderByDescending(x => x.FindAttribute("targetVersion")).First().FindAttribute("targetVersion"));
            return manifest;
        }

        private AppManifest CreateFromAssemblyInfo(AppManifest manifest = null)
        {
            manifest = manifest ?? new AppManifest();
            // In real-life projects AssemblyInfo is often split into several files,
            // e.g. GlobalAssemblyInfo (linked to project) and AssemblyInfo with data
            // specific to the project. The current solution parses csproj and takes into
            // consideration.
            // TODO: The solution doesn't take into consideration new project types
            // which either don't use AssemblyInfo or
            // Compile AssemblyInfo element is not present in csproj
            var combinedAssemblyInfo = GetCombinedAssemblyInfo();
            if (combinedAssemblyInfo.Any())
            {
                var props = combinedAssemblyInfo.Where(l => l.StartsWith("[assembly: ")).ToList();
                manifest.ApplicationName = props.Property("AssemblyTitle");
                manifest.Description = props.Property("AssemblyDescription");
                manifest.PublisherName = props.Property("AssemblyCompany");
                manifest.SuiteName = props.Property("AssemblyProduct");
                manifest.Copyright = props.Property("Copyright");
                var versionProperty = props.Property("Version");
                if (!string.IsNullOrEmpty(versionProperty))
                {
                    manifest.AppVersion = new Version(versionProperty);
                }
                return manifest;
            }
            return null;
        }

        private List<string> GetCombinedAssemblyInfo()
        {
            XDocument document;
            using (var stream = File.OpenRead(ProjectFilePath))
            {
                document = XDocument.Load(stream);
            }

            var projectDir = Path.GetDirectoryName(ProjectFilePath);
            var assemblyInfoPaths = (from compile in document.Descendants("{http://schemas.microsoft.com/developer/msbuild/2003}Compile")
                                     from include in compile.Attributes("Include")
                                     let value = include.Value
                                     where value?.EndsWith("AssemblyInfo.cs") == true
                                     select Path.Combine(projectDir, value))
                                    .ToArray();

            var assemblyInfoLines = new List<string>();
            foreach (var assemblyInfoPath in assemblyInfoPaths)
            {
                var lines = File.ReadAllLines(assemblyInfoPath);
                assemblyInfoLines.AddRange(lines);
            }

            return assemblyInfoLines;
        }

        public FileInfo DeployManifest(AppManifest manifest)
        {
            var j = JsonConvert.SerializeObject(manifest, Formatting.Indented, new Newtonsoft.Json.Converters.VersionConverter());
            File.WriteAllText(GetPublishLocation(), j);
            return new FileInfo(GetPublishLocation());
        }

        private string GetPublishLocation()
        {
            var fi = new FileInfo(ApplicationManifestLocation);
            return Path.Combine(fi.Directory?.FullName ?? new FileInfo(ProjectFilePath).Directory?.FullName ?? string.Empty, fi.Name.Replace(fi.Extension, ".cltw"));
        }

        public static AppManifest ReadFromFile(string manifestFilePath)
        {
            return JsonConvert.DeserializeObject<AppManifest>(File.ReadAllText(manifestFilePath), new VersionConverter());
        }
    }

    public enum InformationSource
    {
        AssemblyInfo,
        AppManifest,
        Both,
        None
    }
}
