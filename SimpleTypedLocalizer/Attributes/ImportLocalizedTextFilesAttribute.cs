using System;

namespace SimpleTypedLocalizer.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ImportLocalizedTextFilesAttribute : Attribute
{
    public ImportLocalizedTextFilesAttribute(string path, ImportType importType)
    {
        
    }
    
    public string NameSpace { get; set; }
}