var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

#Tool "xunit.runner.console"
#Tool "GitVersion.CommandLine"

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var solutions = GetFiles("./**/*.sln");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());

var srcDir = Directory("./src");

var artifactsDir = Directory("./artifacts");
var testResultsDir = artifactsDir + Directory("test-results");
var nupkgDestDir = artifactsDir + Directory("nuget-package");

GitVersion versionInfo;

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories(new DirectoryPath[] {
        artifactsDir,
        testResultsDir,
        nupkgDestDir
  	});

    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }
});

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    foreach(var solution in solutions)
    {
        Information("Restoring NuGet Packages for {0}", solution);
        NuGetRestore(solution);
    }
});

Task("Version")
    .Does(() =>
{
    versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
    Information("AssemblyVersion -> {0}", versionInfo.AssemblySemVer);
    Information("InformationalVersion -> {0}", versionInfo.InformationalVersion);
    Information("NuGetVersion -> {0}", versionInfo.NuGetVersion);
    Information("FullSemVer -> {0}", versionInfo.FullSemVer);

    var file = "./src/GlobalAssemblyInfo.cs";
    CreateAssemblyInfo(file, new AssemblyInfoSettings {
            Product = "Dropcraft",
            Copyright = "Copyright \xa9 Andrei Marukovich",
            Version = versionInfo.AssemblySemVer,
            FileVersion = versionInfo.AssemblySemVer,
            InformationalVersion = versionInfo.InformationalVersion
});
});

Task("Update-AppVeyor-Build-Number")
    .WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
{
    var fullSemVer = versionInfo.FullSemVer.ToString();
    AppVeyor.UpdateBuildVersion(fullSemVer);
});

Task("Build-Solutions")
    .Does(() =>
{
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);

        MSBuild(solution, settings =>
            settings
                .SetConfiguration(configuration)
                .WithProperty("TreatWarningsAsErrors", "false")
                .UseToolVersion(MSBuildToolVersion.NET46)
                .SetVerbosity(Verbosity.Minimal)
                .SetNodeReuse(false));
}});

Task("Run-Tests")
    .Does(() =>
{
    XUnit2("./tests/**/bin/" + configuration + "/Tests.*.dll", new XUnit2Settings {
        OutputDirectory = testResultsDir,
        XmlReportV1 = true
    });
});

Task("Package")
    .Does(() =>
{
    List<FilePath> nuspecs = new List<FilePath>(GetFiles("./src/nuspecs/*.nuspec"));
    foreach (var nuspec in nuspecs)
    {
        NuGetPack(nuspec, new NuGetPackSettings
        {
            Version = nuspec.FullPath.Contains("Deployment") ? versionInfo.NuGetVersion+"-pre"+versionInfo.CommitsSinceVersionSource.ToString() : versionInfo.NuGetVersion,
            BasePath = nuspec.GetDirectory(),
            OutputDirectory = nupkgDestDir,
            Symbols = false
        });
    }    
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Version")
    .IsDependentOn("Update-AppVeyor-Build-Number")
    .IsDependentOn("Build-Solutions")
    .IsDependentOn("Run-Tests")
    .IsDependentOn("Package");

Task("Default")
    .IsDependentOn("Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);