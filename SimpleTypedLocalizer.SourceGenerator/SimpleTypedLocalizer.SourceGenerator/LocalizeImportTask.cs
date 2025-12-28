namespace SimpleTypedLocalizer.SourceGenerator;

public record LocalizeImportTask(string Path, string? NameSpace, int ImportType)
{
    public string Path { get; } = Path;
    public string? NameSpace { get; } = NameSpace;
    public int ImportType { get; } = ImportType;
}