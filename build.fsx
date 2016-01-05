#r @"tools/FAKE.Core/tools/FakeLib.dll"
#load "tools/SourceLink.Fake/tools/SourceLink.fsx"
open Fake 
open System
open SourceLink

let authors = ["Geoffrey Huntley"]

// project name and description
let projectName = "GeoCoordinate"
let projectDescription = "GeoCoordinate is a Portable Class Library compatible implementation of System.Device.Location.GeoCoordinate. It is an exact 1:1 API compliant implementation and will be supported until MSFT sees it fit to embed the type. Which at that point this implementation will cease development/support and you will be able to simply remove this package and everything will still work."
let projectSummary = projectDescription

// directories
let buildDir = "./src/GeoCoordinate/bin"
let testResultsDir = "./testresults"
let packagingRoot = "./packaging/"
let packagingDir = packagingRoot @@ "GeoCoordinate"

let releaseNotes = 
    ReadFile "RELEASENOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

let buildMode = getBuildParamOrDefault "buildMode" "Release"

MSBuildDefaults <- { 
    MSBuildDefaults with 
        ToolsVersion = Some "14.0"
        Verbosity = Some MSBuildVerbosity.Minimal }

Target "Clean" (fun _ ->
    CleanDirs [buildDir; testResultsDir; packagingRoot; packagingDir]
)

open Fake.AssemblyInfoFile
open Fake.Testing

Target "AssemblyInfo" (fun _ ->
    CreateCSharpAssemblyInfo "./SolutionInfo.cs"
      [ Attribute.Product projectName
        Attribute.Version releaseNotes.AssemblyVersion
        Attribute.FileVersion releaseNotes.AssemblyVersion
        Attribute.ComVisible false ]
)

Target "CheckProjects" (fun _ ->
    !! "./src/GeoCoordinate/GeoCoordinate*.csproj"
    |> Fake.MSBuild.ProjectSystem.CompareProjectsTo "./src/GeoCoordinate/GeoCoordinate.csproj"
)


Target "FixProjects" (fun _ ->
    !! "./src/GeoCoordinate/GeoCoordinate*.csproj"
    |> Fake.MSBuild.ProjectSystem.FixProjectFiles "./src/GeoCoordinate/GeoCoordinate.csproj"
)

let setParams defaults = {
    defaults with
        ToolsVersion = Some("14.0")
        Targets = ["Build"]
        Properties =
            [
                "Configuration", buildMode
            ]
    }

let Exec command args =
    let result = Shell.Exec(command, args)
    if result <> 0 then failwithf "%s exited with error %d" command result

Target "BuildApp" (fun _ ->
    build setParams "./src/GeoCoordinate.sln"
        |> DoNothing
)

Target "BuildMono" (fun _ ->
    // xbuild does not support msbuild  tools version 14.0 and that is the reason
    // for using the xbuild command directly instead of using msbuild
    Exec "xbuild" "./src/GeoCoordinate.sln /t:Build /tv:12.0 /v:m  /p:RestorePackages='False' /p:Configuration='Release' /logger:Fake.MsBuildLogger+ErrorLogger,'../src/GeoCoordinate.net/tools/FAKE.Core/tools/FakeLib.dll'"

)

Target "UnitTests" (fun _ ->
    !! (sprintf "./src/GeoCoordinate.Tests/bin/%s/**/GeoCoordinate.Tests*.dll" buildMode)
    |> xUnit2 (fun p -> 
            {p with
                HtmlOutputPath = Some (testResultsDir @@ "xunit.html") })
)

Target "SourceLink" (fun _ ->
    [ "./src/GeoCoordinate/GeoCoordinate.csproj" ]
    |> Seq.iter (fun pf ->
        let proj = VsProj.LoadRelease pf
        let url = "https://raw.githubusercontent.com/ghuntley/GeoCoordinate/{0}/%var2%"
        SourceLink.Index proj.Compiles proj.OutputFilePdb __SOURCE_DIRECTORY__ url
    )
)

Target "CreateGeoCoordinatePackage" (fun _ ->
    let net45Dir = packagingDir @@ "lib/net45/"
    let netcore45Dir = packagingDir @@ "lib/netcore451/"
    let portableDir = packagingDir @@ "lib/portable-net45+wp80+win+wpa81/"
    CleanDirs [net45Dir; netcore45Dir; portableDir]

    CopyFile net45Dir (buildDir @@ "Release/Net45/GeoCoordinate.dll")
    CopyFile net45Dir (buildDir @@ "Release/Net45/GeoCoordinate.XML")
    CopyFile net45Dir (buildDir @@ "Release/Net45/GeoCoordinate.pdb")
    CopyFile netcore45Dir (buildDir @@ "Release/NetCore45/GeoCoordinate.dll")
    CopyFile netcore45Dir (buildDir @@ "Release/NetCore45/GeoCoordinate.XML")
    CopyFile netcore45Dir (buildDir @@ "Release/NetCore45/GeoCoordinate.pdb")
    CopyFile portableDir (buildDir @@ "Release/Portable/GeoCoordinate.dll")
    CopyFile portableDir (buildDir @@ "Release/Portable/GeoCoordinate.XML")
    CopyFile portableDir (buildDir @@ "Release/Portable/GeoCoordinate.pdb")
    CopyFiles packagingDir ["LICENSE.md"; "README.md"; "RELEASENOTES.md"]

    NuGet (fun p -> 
        {p with
            Authors = authors
            Project = projectName
            Description = projectDescription
            OutputPath = packagingRoot
            Summary = projectSummary
            WorkingDir = packagingDir
            Version = releaseNotes.AssemblyVersion
            ReleaseNotes = toLines releaseNotes.Notes
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" }) "GeoCoordinate.nuspec"
)

Target "Default" DoNothing

Target "CreatePackages" DoNothing

"Clean"
   ==> "AssemblyInfo"
   ==> "CheckProjects"
   ==> "BuildApp"

"Clean"
   ==> "AssemblyInfo"
   ==> "CheckProjects"
   ==> "BuildMono"

"UnitTests"
   ==> "Default"

"SourceLink"
   ==> "CreatePackages"

"CreateGeoCoordinatePackage"
   ==> "CreatePackages"

RunTargetOrDefault "Default"