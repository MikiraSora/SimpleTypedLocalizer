using System.Collections.Immutable;

namespace SimpleTypedLocalizer.SourceGenerator;

public record LocalizeImportTaskContext(
    ImmutableArray<LocalizeImportTask> ImportTasks,
    string TargetNameSpace,
    string TargetClassName,
    string TargetClassAccessibility,
    string ProjectFolder)
{
    public ImmutableArray<LocalizeImportTask> ImportTasks { get; set; } = ImportTasks;
    public string TargetNameSpace { get; set; } = TargetNameSpace;
    public string TargetClassName { get; set; } = TargetClassName;
    public string TargetClassAccessibility { get; set; } = TargetClassAccessibility;
    public string ProjectFolder { get; set; } = ProjectFolder;
}