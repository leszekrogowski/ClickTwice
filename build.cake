#tool "ILRepack"
#tool "GitVersion.CommandLine"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutionPath = File("./src/ClickTwice.sln");
var solution = ParseSolution(solutionPath);
var projects = solution.Projects;
var projectPaths = projects.Select(p => p.Path.GetDirectory());
//var testAssemblies = projects.Where(p => p.Name.Contains("Tests")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
var artifacts = "./artifacts/";
//var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));
GitVersion versionInfo = null;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	versionInfo = GitVersion();
	Information("Building for version {0}", versionInfo.FullSemVer);
});

Teardown(() =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in projectPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }
    Information("Cleaning common files...");
    CleanDirectory(artifacts);
});

Task("Restore")
    .Does(() =>
{
    // Restore all NuGet packages.
    Information("Restoring solution...");
    NuGetRestore(solutionPath);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
	Information("Building solution...");
	MSBuild(solutionPath, settings =>
		settings.SetPlatformTarget(PlatformTarget.MSIL)
			//.WithProperty("TreatWarningsAsErrors","true")
			.SetVerbosity(Verbosity.Quiet)
			.WithTarget("Build")
			.SetConfiguration(configuration));
});

Task("Copy-Files")
    .IsDependentOn("Build")
    .Does(() =>
{
    CreateDirectory(artifacts + "build");
	foreach (var project in projects) {
		CreateDirectory(artifacts + "build/" + project.Name);
		var files = GetFiles(project.Path.GetDirectory() +"/bin/" +configuration +"/" +project.Name +".*");
		CopyFiles(files, artifacts + "build/" + project.Name);
	}
});

Task("Publish")
	.IsDependentOn("Build")
	.IsDependentOn("Copy-Files")
	.Does(() => {
        var mergeProjects = new[] {"ClickTwice.Handlers.LaunchPage", "ClickTwice.Handlers.AppDetailsPage"};
        foreach (var project in mergeProjects) {
		Information("Merging libraries");
		var assemblyList = GetFiles("./src/" + project + "/bin/" + configuration + "/**/*.dll");
		Information("Executing ILMerge to merge {0} assemblies", assemblyList.Count);
		ILRepack(
			artifacts + project + ".dll",
			"./src/" + project + "/bin/" + configuration + "/" + project + ".dll",
			assemblyList);
        }
	});
    
    Task("Merge")
    .IsDependentOn("Copy-Files")
    .Does(() => {
        var assemblyList = GetFiles("./src/Cake.ClickTwice/bin/" + configuration + "/**/*.dll").ToList();
        assemblyList.AddRange(GetFiles("./src/ClickTwice.Handlers.LaunchPage/bin/" + configuration + "/**/*.dll"));
        Information("Merging {0} assemblies", assemblyList.Count);
        ILRepack(
            artifacts + "Cake.ClickTwice.dll",
            "./src/Cake.ClickTwice/bin/" + configuration + "/Cake.ClickTwice.dll",
            assemblyList);
    });

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Copy-Files");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
