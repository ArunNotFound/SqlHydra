module SqlHydra.Extensions

open System
open System.IO
open System.Runtime.Loader
open SqlHydra.Domain
open Microsoft.Build.Construction

/// Resolves an extension assembly from the target project's references and loads IExtendTypeMapping implementations.
let load (project: FileInfo) (extensionNames: string list) : IExtendTypeMapping list =
    if extensionNames.IsEmpty then [] else

    let root = ProjectRootElement.Open(project.FullName)

    let allRefs =
        root.ItemGroups
        |> Seq.collect _.Items
        |> Seq.filter (fun item -> item.ItemType = "PackageReference" || item.ItemType = "ProjectReference")
        |> Seq.toList

    let interfaceType = typeof<IExtendTypeMapping>

    extensionNames
    |> List.collect (fun extName ->
        let ref =
            allRefs
            |> List.tryFind (fun item ->
                // PackageReference: Include matches the name directly
                // ProjectReference: Include is a path; match on the project file name without extension
                if item.ItemType = "PackageReference" then
                    item.Include = extName
                else
                    Path.GetFileNameWithoutExtension(item.Include) = extName
            )

        match ref with
        | None ->
            failwith $"Extension '{extName}' was not found as a PackageReference or ProjectReference in '{project.Name}'."
        | Some _ ->
            // Search the project's output directories for the extension assembly
            let projectDir = project.Directory.FullName
            let dllName = $"{extName}.dll"

            let dllPath =
                Directory.EnumerateFiles(Path.Combine(projectDir, "bin"), dllName, SearchOption.AllDirectories)
                |> Seq.tryHead

            match dllPath with
            | None ->
                failwith $"Could not find '{dllName}' in the build output of '{project.Name}'. Ensure the project has been built."
            | Some path ->
                let asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path))

                asm.GetTypes()
                |> Array.filter (fun t ->
                    not t.IsAbstract && not t.IsInterface &&
                    interfaceType.IsAssignableFrom(t))
                |> Array.map (fun t -> Activator.CreateInstance(t) :?> IExtendTypeMapping)
                |> Array.toList
    )
