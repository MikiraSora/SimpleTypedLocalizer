using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SimpleTypedLocalizer.SourceGenerator;

public static class ImportTaskRunner
{
    public static RunTaskContextResult? RunTaskContext(LocalizeImportTaskContext context,
        ImmutableArray<AdditionalText> files,
        CancellationToken cancellationToken)
    {
        var providerDeclareSourceSentences = new HashSet<string>();
        var exportLocalizedTextNames = new Dictionary<string, string>();
        var staticBuildProviders = new HashSet<RunTaskContextResult.StaticBuildProvider>();

        foreach (var task in context.ImportTasks)
        {
            if (cancellationToken.IsCancellationRequested)
                return default;

            var isImportAtCompileTime = task.ImportType == 0;

            //var additionalTexts = EnumerateFiles(files, task.Path, context.ProjectFolder);
            var additionalTexts = FilterFiles(files, task.Path);
            foreach (var additionalText in additionalTexts)
            {
                if (cancellationToken.IsCancellationRequested)
                    return default;
                var filePath = additionalText.Path;

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!TryParseLangCodeFromFileName(fileName, out var langCode))
                    continue;

                if (!TryParseLangFile(additionalText, out var langMap) || langMap is null)
                {
                    //这里直接给注释说明加载失败
                    providerDeclareSourceSentences.Add($"/*error: Can't import lang file: {filePath}*/");
                    continue;
                }

                //更新可本地化的文本名称
                foreach (var pair in langMap)
                    if (!exportLocalizedTextNames.ContainsKey(pair.Key))
                        exportLocalizedTextNames[pair.Key] = pair.Value;

                if (isImportAtCompileTime)
                {
                    var buildProvider = new RunTaskContextResult.StaticBuildProvider(
                        GenerateBuildProviderClassName(fileName), langCode, langMap.ToImmutableDictionary());

                    providerDeclareSourceSentences.Add(
                        $"{buildProvider.ProviderClassName}.Instance /*import from: {filePath}*/");
                    staticBuildProviders.Add(buildProvider);
                }
                //todo 那么就是作为AvaloniaEmebbedResource在运行时加载咯,真有这个必要吗？
                /*
                    var avaloniaResourceUri = BuildAvaloniaResourceUri(task.Path,task.NameSpace);
                    result.ProviderDeclareSourceSentences.Add();
                    */
            }
        }

        return new RunTaskContextResult(
            exportLocalizedTextNames.ToImmutableDictionary(),
            providerDeclareSourceSentences.ToImmutableHashSet(),
            staticBuildProviders.ToImmutableHashSet(),
            exportLocalizedTextNames.Count > 0);
    }

    private static string GenerateBuildProviderClassName(string fileName)
    {
        var classNameBuilder = new StringBuilder();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            classNameBuilder.Append("Unknown");
        }
        else
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            string[] words = Regex.Split(baseName, @"[^a-zA-Z0-9]");


            foreach (var word in words)
            {
                if (word.Length <= 0) continue;

                classNameBuilder.Append(char.ToUpper(word[0]));
                if (word.Length > 1)
                    classNameBuilder.Append(word[1..]);
            }

            if (classNameBuilder.Length > 0 && char.IsDigit(classNameBuilder[0]))
                classNameBuilder.Insert(0, "_");
        }

        classNameBuilder.Append("Provider");
        classNameBuilder.Append("_");
        classNameBuilder.Append(RandomNumberGenerator.GetInt32(0, 1000000));

        return classNameBuilder.ToString();
    }

    private static bool TryParseLangFile(AdditionalText additionalText, out Dictionary<string, string>? langMap)
    {
        langMap = default;
        if (additionalText.GetText() is not {Length: > 0} sourceText)
            return false;
        var content = sourceText.ToString();

        try
        {
            langMap = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
        }
        catch (Exception e)
        {
            //try parse as .resx file
            try
            {
                langMap = ParseResxContent(content);
            }
            catch (Exception e2)
            {
                return false;
            }
        }

        return langMap != null;
    }

    private static Dictionary<string, string> ParseResxContent(string xmlContent)
    {
        var dict = new Dictionary<string, string>();
        var doc = XDocument.Parse(xmlContent);

        foreach (var dataElement in doc.Root.Elements("data"))
        {
            var name = dataElement.Attribute("name")?.Value;
            var value = dataElement.Element("value")?.Value;

            if (name != null && value != null)
                dict[name] = value;
        }

        return dict;
    }

    private static bool TryParseLangCodeFromFileName(string fileName, out string langCode)
    {
        bool IsValidLanguageFormat(string code)
        {
            if (code.Length < 2 || code.Length > 10) return false;

            foreach (var c in code)
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;

            return true;
        }

        langCode = default;
        /* 钦定符合标准的文件名都是这种:
         * myLang.zh-cn
         * daswe.zh
         * qwe
         * sadxxx.jp
         */

        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var lastDotIndex = fileName.LastIndexOf('.');

        if (lastDotIndex == -1)
        {
            langCode = "";
            return true;
        }

        if (lastDotIndex == fileName.Length - 1)
            return false;

        // 2. 提取点之后的部分
        var potentialLangCode = fileName.Substring(lastDotIndex + 1);

        if (IsValidLanguageFormat(potentialLangCode))
        {
            langCode = potentialLangCode;
            return true;
        }

        return false;
    }

    private static IEnumerable<AdditionalText> FilterFiles(ImmutableArray<AdditionalText> files, string pathPattern)
    {
        /* 比如 pathPattern = "Assets/Languages/lang.resx" baseFolder = C:/AA/
         * 那么可以匹配以下 AdditionalText 列表对象:
         *  C:/AA/Assets/Languages/lang.zh-cn.resx
         *  C:/BAA/Assets/Languages/lang.jp.resx
         *  C:/AA/Assets/Languages/lang.resx
         */

        string path(string path)
        {
            return path?.Replace('\\', '/');
        }

        if (string.IsNullOrWhiteSpace(pathPattern)) yield break;

        // 1. 统一分隔符并获取关键信息
        var normalizedPattern = path(pathPattern)?.Trim('/');
        var directoryPart = path(Path.GetDirectoryName(normalizedPattern)) ?? "";
        var fileNameNoExt = Path.GetFileNameWithoutExtension(normalizedPattern);
        var extension = Path.GetExtension(normalizedPattern);

        foreach (var file in files)
        {
            var currentPath = file.Path.Replace('\\', '/');

            if (!currentPath.Contains(directoryPart)) continue;

            var currentFileName = Path.GetFileName(currentPath);

            if (currentFileName.StartsWith(fileNameNoExt, StringComparison.OrdinalIgnoreCase) &&
                currentFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                var middlePart = currentFileName.Substring(
                    fileNameNoExt.Length,
                    currentFileName.Length - fileNameNoExt.Length - extension.Length);

                if (string.IsNullOrEmpty(middlePart) || middlePart.StartsWith("."))
                    yield return file;
            }
        }
    }
}