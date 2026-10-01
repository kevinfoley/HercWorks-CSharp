using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

namespace RenameSymbol;

internal static class Tool
{
    public static async Task<int> RunAsync(string solutionPath, string target, string newName, HashSet<string> flags)
    {
        if (!SyntaxFacts.IsValidIdentifier(newName))
            return Fail($"'{newName}' is not a valid C# identifier", 2);
        if (!File.Exists(solutionPath))
            return Fail($"solution not found: {solutionPath}", 2);

        using var workspace = MSBuildWorkspace.Create();
        var loadFailures = 0;
        using var _ = workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                loadFailures++;
            Console.Error.WriteLine($"workspace {e.Diagnostic.Kind.ToString().ToLowerInvariant()}: {e.Diagnostic.Message}");
        });

        Solution solution;
        try
        {
            solution = await workspace.OpenSolutionAsync(solutionPath);
        }
        catch (Exception ex)
        {
            return Fail($"failed to load {solutionPath}: {ex.Message}");
        }
        // A project that failed to load would have its references silently left unrenamed.
        if (loadFailures > 0)
            return Fail($"{loadFailures} workspace load failure(s); nothing renamed");
        if (!solution.Projects.Any())
            return Fail($"{solutionPath} loaded no projects");

        // An unresolved package or project reference (an unrestored solution, typically) leaves the
        // code that uses the symbol unbound, and Renamer then skips it without complaint.
        var compilations = new List<Compilation>();
        var brokenProjects = 0;
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
                return Fail($"{project.Name} produced no compilation");
            compilations.Add(compilation);
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Count == 0)
                continue;
            brokenProjects++;
            Console.Error.WriteLine($"{project.Name}: {errors.Count} compile error(s), first: {errors[0]}");
        }
        if (brokenProjects > 0 && !flags.Contains("--allow-errors"))
            return Fail("references in code that does not compile would be missed; run `dotnet restore` on the solution, or pass --allow-errors");

        var candidates = Resolve(compilations, target, flags.Contains("--overloads"));
        if (candidates.Count != 1)
        {
            Console.Error.WriteLine(candidates.Count == 0
                ? $"no type or member matches '{target}'"
                : $"'{target}' is ambiguous; qualify it further:");
            foreach (var c in candidates)
                Console.Error.WriteLine($"  {c.ToDisplayString()}  [{c.Kind}, {c.ContainingAssembly?.Name}]");
            return 1;
        }

        var symbol = candidates[0];
        if (symbol.Name == newName)
            return Fail($"{symbol.ToDisplayString()} is already named {newName}");

        var options = new SymbolRenameOptions(
            RenameOverloads: flags.Contains("--overloads"),
            RenameInStrings: flags.Contains("--strings"),
            RenameInComments: flags.Contains("--comments"),
            RenameFile: flags.Contains("--rename-file"));
        var renamed = await Renamer.RenameSymbolAsync(solution, symbol, options, newName);

        var changed = renamed.GetChanges(solution).GetProjectChanges()
            .SelectMany(p => p.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true)
                .Select(id => renamed.GetDocument(id)?.FilePath ?? solution.GetDocument(id)?.FilePath ?? id.ToString()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var renamedFiles = renamed.GetChanges(solution).GetProjectChanges()
            .SelectMany(p => p.GetChangedDocuments())
            .Select(id => (from: solution.GetDocument(id)?.Name, to: renamed.GetDocument(id)?.Name))
            .Where(f => f.from != f.to)
            .ToList();

        Console.WriteLine($"{symbol.ToDisplayString()} -> {newName}: {changed.Count} file(s)");
        foreach (var path in changed)
            Console.WriteLine($"  {Path.GetRelativePath(Environment.CurrentDirectory, path)}");
        foreach (var (from, to) in renamedFiles)
            Console.WriteLine($"  file {from} -> {to}");

        if (flags.Contains("--dry-run"))
            return 0;
        if (!workspace.TryApplyChanges(renamed))
            return Fail("the workspace refused the changes; nothing written");
        return 0;
    }

    /// <summary>
    /// Every source symbol <paramref name="target"/> can name: a type matched whole, or a member
    /// whose last dotted segment is the member name and whose prefix matches its containing type.
    /// </summary>
    private static List<ISymbol> Resolve(IEnumerable<Compilation> compilations, string target, bool collapseOverloads)
    {
        var found = new List<ISymbol>();
        var dot = target.LastIndexOf('.');
        var typePart = dot < 0 ? null : target[..dot];
        var memberPart = dot < 0 ? null : target[(dot + 1)..];

        foreach (var compilation in compilations)
        {
            foreach (var type in AllTypes(compilation.Assembly.GlobalNamespace))
            {
                if (TypeMatches(type, target))
                    found.Add(type);
                if (typePart is not null && TypeMatches(type, typePart))
                    found.AddRange(type.GetMembers(memberPart!).Where(m => !m.IsImplicitlyDeclared));
            }
        }

        // A type reached both as itself and as a nested member of its outer type, or a linked file
        // compiled into two projects, is the same declaration.
        var distinct = found
            .Where(s => s.Locations.Any(l => l.IsInSource))
            .GroupBy(s =>
            {
                var l = s.Locations.First(l => l.IsInSource);
                return (l.SourceTree!.FilePath, l.SourceSpan.Start);
            })
            .Select(g => g.First())
            .ToList();

        // With --overloads any one overload stands for the group.
        if (collapseOverloads && distinct.Count > 1 && distinct.All(s => s is IMethodSymbol)
            && distinct.Select(s => (s.ContainingType.ToDisplayString(), s.Name)).Distinct().Count() == 1)
            return [distinct[0]];
        return distinct;
    }

    private static bool TypeMatches(INamedTypeSymbol type, string name) =>
        type.Name == name
        || type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == name
        || type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) == name
        || MetadataName(type) == name;

    private static string MetadataName(INamedTypeSymbol type) =>
        type.ContainingType is { } outer ? MetadataName(outer) + "+" + type.MetadataName
        : type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + type.MetadataName
        : type.MetadataName;

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol ns)
                foreach (var t in AllTypes(ns)) yield return t;
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var t in AllTypes(type)) yield return t;
            }
        }
    }

    private static int Fail(string message, int code = 1)
    {
        Console.Error.WriteLine(message);
        return code;
    }
}
