module SqlHydra.Extensions

open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open SqlHydra.Domain
open Microsoft.Build.Construction

let private interfaceType = typeof<IExtendTypeMapping>

/// An AssemblyLoadContext that resolves shared dependencies (e.g. SqlHydra.Domain, FSharp.Core)
/// back to the host's already-loaded assemblies, ensuring interface type identity is preserved.
type private ExtensionLoadContext(pluginPath: string) =
    inherit AssemblyLoadContext(isCollectible = true)

    let resolver = AssemblyDependencyResolver(pluginPath)

    override this.Load(assemblyName: AssemblyName) =
        // First, check if the host already has this assembly loaded (e.g. SqlHydra.Domain, FSharp.Core).
        // This ensures the extension's IExtendTypeMapping is the same type as the host's.
        let hostAsm =
            AssemblyLoadContext.Default.Assemblies
            |> Seq.tryFind (fun a -> a.GetName().Name = assemblyName.Name)
        match hostAsm with
        | Some asm -> asm
        | None ->
            match resolver.ResolveAssemblyToPath(assemblyName) with
            | null -> null
            | path -> this.LoadFromAssemblyPath(path)

/// Discovers all IExtendTypeMapping implementations in the given assembly.
/// Uses ReflectionTypeLoadException fallback to handle types whose dependencies aren't available.
let private discoverExtensions (asm: Assembly) =
    let types =
        try
            asm.GetTypes()
        with
        | :? ReflectionTypeLoadException as ex ->
            ex.Types |> Array.filter (fun t -> t <> null)

    types
    |> Array.filter (fun t ->
        not t.IsAbstract && not t.IsInterface &&
        interfaceType.IsAssignableFrom(t))
    |> Array.map (fun t -> Activator.CreateInstance(t) :?> IExtendTypeMapping)
    |> Array.toList

/// Resolves extension assemblies and loads IExtendTypeMapping implementations.
let load (project: FileInfo) (extensionNames: string list) : IExtendTypeMapping list =
    if extensionNames.IsEmpty then [] else

    let projectName = Path.GetFileNameWithoutExtension(project.Name)

    extensionNames
    |> List.collect (fun extName ->
        // If the extension name matches the target project itself, scan its output assembly
        let isOwnProject = String.Equals(extName, projectName, StringComparison.OrdinalIgnoreCase)

        if not isOwnProject then
            // Verify the extension is referenced by the project
            let root = ProjectRootElement.Open(project.FullName)
            let hasRef =
                root.ItemGroups
                |> Seq.collect _.Items
                |> Seq.exists (fun item ->
                    match item.ItemType with
                    | "PackageReference" -> item.Include = extName
                    | "ProjectReference" -> Path.GetFileNameWithoutExtension(item.Include) = extName
                    | _ -> false
                )
            if not hasRef then
                failwith $"Extension '{extName}' was not found as a PackageReference or ProjectReference in '{project.Name}'."

        // Search the project's output directories for the extension assembly
        let projectDir = project.Directory.FullName
        let dllName = $"{extName}.dll"
        let binDir = Path.Combine(projectDir, "bin")

        if not (Directory.Exists(binDir)) then
            failwith $"Could not find '{dllName}' in the build output of '{project.Name}'. Ensure the project has been built."

        let dllPath =
            Directory.EnumerateFiles(binDir, dllName, SearchOption.AllDirectories)
            |> Seq.tryHead

        match dllPath with
        | None ->
            failwith $"Could not find '{dllName}' in the build output of '{project.Name}'. Ensure the project has been built."
        | Some path ->
            let fullPath = Path.GetFullPath(path)
            let loadContext = ExtensionLoadContext(fullPath)
            let asm = loadContext.LoadFromAssemblyPath(fullPath)
            discoverExtensions asm
    )
