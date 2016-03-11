#r @"packages/FAKE/tools/FakeLib.dll"
open Fake

let buildReleaseDir = "./bin/Release"
let objDirs = !! "**/obj/"
let version = "0.0.1"

// ------ Targets

Target "Clean" (fun _ ->
    CleanDirs <| objDirs ++ buildReleaseDir
)

Target "Build" (fun _ ->
   !! "TomlProvider/*.fsproj"
     |> MSBuildRelease buildReleaseDir "Build"
     |> Log "Build-Output: "
)

Target "Rebuild" (fun _ -> trace "All rebuilt.")

// ------ Dependencies

"Build"
    <=? "Clean"

"Rebuild" <==
    ["Build"; "Clean"]

// ------ Execute

RunTargetOrDefault "Build"
