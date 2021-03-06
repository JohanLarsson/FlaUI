#tool nuget:?package=NUnit.ConsoleRunner&version=3.7.0
#addin Cake.FileHelpers
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var slnFile = @"src\FlaUI.sln";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    NuGetRestore(slnFile);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    var buildSettings = new MSBuildSettings {
        Verbosity = Verbosity.Minimal,
        ToolVersion = MSBuildToolVersion.VS2017,
        Configuration = configuration,
        PlatformTarget = PlatformTarget.MSIL,
    }.AddFileLogger(new MSBuildFileLogger {
        LogFile = "./BuildLog.txt",
        MSBuildFileLoggerOutput = MSBuildFileLoggerOutput.All
    });
    // Hide informational warnings for now
    buildSettings.Properties.Add("WarningLevel", new[] { "3" });
    // Force restoring
    buildSettings.Properties.Add("RestoreForce", new[] { "true" });

    // First build with default settings
    buildSettings.Targets.Clear();
    buildSettings.WithTarget("Restore");
    MSBuild(slnFile, buildSettings);
    buildSettings.Targets.Clear();
    buildSettings.WithTarget("Build");
    MSBuild(slnFile, buildSettings);

    // Second build with signing enabled
    buildSettings.FileLoggers.First().LogFile = "./BuildLogSigned.txt";
    buildSettings.Properties.Add("EnableSigning", new[] { "true" });
    buildSettings.Targets.Clear();
    buildSettings.WithTarget("Restore");
    MSBuild(slnFile, buildSettings);
    buildSettings.Targets.Clear();
    buildSettings.WithTarget("Build");
    MSBuild(slnFile, buildSettings);
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3(@"src\FlaUI.Core.UnitTests\bin\FlaUI.Core.UnitTests.dll", new NUnit3Settings {
        Results = "UnitTestResult.xml"
    });
    if (AppVeyor.IsRunningOnAppVeyor) {
        AppVeyor.UploadTestResults("UnitTestResult.xml", AppVeyorTestResultsType.NUnit3);
    }
});

Task("Run-UI-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3(@"src\FlaUI.Core.UITests\bin\FlaUI.Core.UITests.dll", new NUnit3Settings {
        Results = "UIA2TestResult.xml",
        ArgumentCustomization = args => args.Append("--params=uia=2")
    });
    if (AppVeyor.IsRunningOnAppVeyor) {
        AppVeyor.UploadTestResults("UIA2TestResult.xml", AppVeyorTestResultsType.NUnit3);
    }

    NUnit3(@"src\FlaUI.Core.UITests\bin\FlaUI.Core.UITests.dll", new NUnit3Settings {
        Results = "UIA3TestResult.xml",
        ArgumentCustomization = args => args.Append("--params=uia=3")
    });
    if (AppVeyor.IsRunningOnAppVeyor) {
        AppVeyor.UploadTestResults("UIA3TestResult.xml", AppVeyorTestResultsType.NUnit3);
    }
});

Task("Run-Tests")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Run-UI-Tests")
    .Does(() =>
{
});

Task("Package")
    .IsDependentOn("Run-Tests")
    .Does(() =>
{
    // Upload the artifacts to appveyor
    if (AppVeyor.IsRunningOnAppVeyor) {
        // Upload the nuget packages
        var files = GetFiles("./nuget/*.nupkg");
        foreach(var file in files)
        {
            AppVeyor.UploadArtifact(file);
        }
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run-Unit-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
