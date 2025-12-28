using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SimpleTypedLocalizer.SourceGenerator;

[Generator]
public class LocalizeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var taskContexts = context.SyntaxProvider
            .CreateSyntaxProvider(
                SyntacticPredicate,
                SemanticTransform
            )
            .Where(static context => context is
                {
                    ImportTasks.Length: > 0
                }
            )
            .Collect()
            .WithTrackingName("TaskContexts");

        var additionFiles = context.AdditionalTextsProvider
            .Where(x => x.Path.EndsWith(".json") || x.Path.EndsWith(".resx"))
            .Collect()
            .WithTrackingName("AdditionFiles");

        var postprocessTask =
            taskContexts.Combine(additionFiles)
                .Select((pair, cancelToken) =>
                {
                    var files = pair.Right;
                    var resultList = new List<(LocalizeImportTaskContext, RunTaskContextResult?)>();

                    foreach (var taskContext in pair.Left)
                    {
                        var result = ImportTaskRunner.RunTaskContext(taskContext!, files, cancelToken);
                        resultList.Add((taskContext!, result));
                    }

                    return resultList.ToImmutableArray();
                });

        context.RegisterSourceOutput(postprocessTask, ExecuteGeneration);
    }

    private void ExecuteGeneration(SourceProductionContext sourceContext,
        ImmutableArray<(LocalizeImportTaskContext, RunTaskContextResult?)> arg)
    {
        foreach (var pair in arg.Where(x => x.Item2 != null))
        {
            // generate registration method
            var sourceCode = ImportTaskSourceWriter.GenerateExtensionClass(pair.Item1, pair.Item2!);

            // add source file
            sourceContext.AddSource(
                $"{pair.Item1.TargetNameSpace.Replace(".", "_")}_{pair.Item1.TargetClassName}_localized.g.cs",
                SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static bool SyntacticPredicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        return syntaxNode switch
        {
            ClassDeclarationSyntax {AttributeLists.Count: > 0} declaration => CheckClassIsImportable(declaration),
            _ => false
        };
    }

    private static bool CheckClassIsImportable(ClassDeclarationSyntax declaration)
    {
        return !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
               !declaration.Modifiers.Any(SyntaxKind.StaticKeyword) &&
               declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static LocalizeImportTaskContext? SemanticTransform(GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        return context.Node switch
        {
            ClassDeclarationSyntax => SemanticTransformClass(context),
            _ => null
        };
    }

    private static LocalizeImportTaskContext? SemanticTransformClass(GeneratorSyntaxContext context)
    {
        if (context.Node is not (TypeDeclarationSyntax declaration
            and (ClassDeclarationSyntax or RecordDeclarationSyntax)))
            return null;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if (classSymbol is null)
            return null;

        var attributes = classSymbol.GetAttributes();

        //build class info
        var targetClassName = classSymbol.Name;
        var targetNameSpace = GetFullNameSpace(classSymbol);
        var targetClassAccessibility = classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            _ => "private"
        };

        var projectFolder = GetProjectDirectoryViaSyntax(context);

        //build tasks
        var tasks = new List<LocalizeImportTask>();
        foreach (var attribute in attributes)
            if (TryCreateImportTask(classSymbol, attribute) is { } task)
                tasks.Add(task);

        return new LocalizeImportTaskContext(tasks.ToImmutableArray(), targetNameSpace, targetClassName,
            targetClassAccessibility, projectFolder);
    }

    private static string GetProjectDirectoryViaSyntax(GeneratorSyntaxContext context)
    {
        return "";
        /*
        var classFilePath = context.Node.SyntaxTree.FilePath;

        if (string.IsNullOrEmpty(classFilePath))
            return string.Empty;

        var directory = Path.GetDirectoryName(classFilePath);
        while (directory != null)
        {
            if (Directory.GetFiles(directory, "*.csproj").Any())
                return directory;
            directory = Path.GetDirectoryName(directory);
        }

        return Path.GetDirectoryName(classFilePath)!;
        */
    }

    private static string GetFullNameSpace(INamedTypeSymbol symbol)
    {
        if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
            return string.Empty;

        return symbol.ContainingNamespace.ToDisplayString();
    }

    private static LocalizeImportTask? TryCreateImportTask(INamedTypeSymbol classSymbol, AttributeData attribute)
    {
        // check for known attribute
        if (!IsImportAttribute(attribute, out var path, out var nameSpace, out var importType))
            return null;

        return new LocalizeImportTask(path, nameSpace, importType);
    }

    private static bool IsImportAttribute(
        AttributeData attribute,
        out string path,
        out string? nameSpace,
        out int importType)
    {
        path = string.Empty;
        nameSpace = null;
        importType = 0;

        if (attribute.AttributeClass?.Name != "ImportLocalizedTextFilesAttribute" &&
            attribute.AttributeClass?.ToDisplayString() !=
            "SimpleTypedLocalizer.Attributes.ImportLocalizedTextFilesAttribute")
            return false;

        if (attribute.ConstructorArguments.Length >= 2)
        {
            path = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;

            var typeArg = attribute.ConstructorArguments[1].Value;
            if (typeArg is int val)
                importType = val;
        }

        var nameSpaceArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "NameSpace");

        if (!nameSpaceArg.Value.IsNull)
            nameSpace = nameSpaceArg.Value.Value?.ToString();

        return !string.IsNullOrEmpty(path);
    }
}