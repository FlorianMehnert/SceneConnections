using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ClassParser
{
    
    public static Dictionary<string, List<string>> GetAllClassReferencesParallel(IEnumerable<string> scriptPaths)
    {
        var resultDictionary = new ConcurrentDictionary<string, List<string>>();

        Parallel.ForEach(
            scriptPaths,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            scriptPath =>
            {
                try
                {
                    var references = GetClassReferences(scriptPath);
                    var scriptName = Path.GetFileNameWithoutExtension(scriptPath);
                    resultDictionary.TryAdd(scriptName, references);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing {scriptPath}: {ex.Message}");
                }
            }
        );

        return new Dictionary<string, List<string>>(resultDictionary);
    }
    private static void CollectMethodParameters(string content, HashSet<string> references)
    {
        // Match method parameters
        var methodRegex = new Regex(@"(?:private|public|protected|internal)\s+(?:static\s+)?(?:<[^>]+>\s+)?([A-Za-z0-9_.<>]+)\s+\w+\s*\(([^)]*)\)");
        foreach (Match match in methodRegex.Matches(content))
        {
            // Add return type
            AddReference(match.Groups[1].Value, references);

            // Add parameter types
            if (string.IsNullOrEmpty(match.Groups[2].Value)) continue;
            var parameters = match.Groups[2].Value.Split(',');
            foreach (var param in parameters)
            {
                var paramType = param.Trim().Split(' ')[0];
                AddReference(paramType, references);
            }
        }
    }

    private static void CollectGenericTypes(string content, HashSet<string> references)
    {
        // Match generic type arguments
        var genericRegex = new Regex("<([^<>]+)>");
        foreach (Match match in genericRegex.Matches(content))
        {
            var genericTypes = match.Groups[1].Value.Split(',');
            foreach (var type in genericTypes)
            {
                AddReference(type.Trim(), references);
            }
        }
    }

    private static void CollectInheritanceReferences(string content, HashSet<string> references)
    {
        // Match class inheritance and interface implementations
        var inheritanceRegex = new Regex(@"class\s+\w+\s*:\s*([^{]+)");
        foreach (Match match in inheritanceRegex.Matches(content))
        {
            var inheritedTypes = match.Groups[1].Value.Split(',');
            foreach (var type in inheritedTypes)
            {
                AddReference(type.Trim(), references);
            }
        }
    }

    private static void CollectVariableDeclarations(string content, HashSet<string> references)
    {
        // Match local variable declarations
        var varRegex = new Regex(@"(?:var|[A-Za-z0-9_.<>]+(?:\[\])?)\s+\w+\s*=\s*new\s+([A-Za-z0-9_.<>]+)");
        foreach (Match match in varRegex.Matches(content))
        {
            AddReference(match.Groups[1].Value, references);
        }
    }

    private static void CollectAttributeReferences(string content, HashSet<string> references)
    {
        // Match attributes
        var attributeRegex = new Regex(@"\[([A-Za-z0-9_]+)(?:\(.*?\))?\]");
        foreach (Match match in attributeRegex.Matches(content))
        {
            var attributeName = match.Groups[1].Value;
            if (!attributeName.EndsWith("Attribute"))
                attributeName += "Attribute";
            AddReference(attributeName, references);
        }
    }
    
    private static readonly ThreadLocal<HashSet<string>> UsingStatements = new(() => new HashSet<string>());

    // Original GetClassReferences method remains mostly the same, but with thread-safety improvements
    private static List<string> GetClassReferences(string scriptPath)
    {
        var references = new HashSet<string>();
        lock (references)
        {
            UsingStatements.Value.Clear();
        }

        // Read the script file - using blocks ensure proper resource disposal
        string content;
        using (var reader = new StreamReader(scriptPath))
        {
            content = reader.ReadToEnd();
        }

        // Process the content in parallel where possible
        var tasks = new[]
        {
            Task.Run(() => CollectUsingStatements(content)),
            Task.Run(() => CollectFieldReferences(content, references)),
            Task.Run(() => CollectMethodParameters(content, references)),
            Task.Run(() => CollectGenericTypes(content, references)),
            Task.Run(() => CollectInheritanceReferences(content, references)),
            Task.Run(() => CollectVariableDeclarations(content, references)),
            Task.Run(() => CollectAttributeReferences(content, references))
        };

        Task.WaitAll(tasks);

        FilterCommonTypes(references);
        return references.ToList();
    }

    private static void CollectUsingStatements(string content)
    {
        var usingRegex = new Regex(@"using\s+(?!static|System)([^;]+);", RegexOptions.Compiled);
        foreach (Match match in usingRegex.Matches(content))
        {
            lock (UsingStatements)
            {
                UsingStatements.Value.Add(match.Groups[1].Value.Trim());
            }
        }
    }

    // Other collection methods modified to use ConcurrentHashSet or locks
    private static void CollectFieldReferences(string content, HashSet<string> references)
    {
        var fieldRegex = new Regex(@"(?:private|public|protected|internal)\s+(?:readonly\s+)?([A-Za-z0-9_.<>]+(?:\[\])?)\s+\w+\s*[;={]", RegexOptions.Compiled);
        foreach (Match match in fieldRegex.Matches(content))
        {
            lock (references)
            {
                AddReference(match.Groups[1].Value, references);
            }
        }
    }

    // Similar modifications for other Collect* methods...

    private static void AddReference(string type, HashSet<string> references)
    {
        type = type.Trim().Replace("[]", "");
        
        if (type.Contains("<"))
        {
            type = type.Substring(0, type.IndexOf("<", StringComparison.Ordinal));
        }

        if (IsCSharpKeyword(type))
            return;

        lock (references)
        {
            references.Add(type);
            foreach (var usingStatement in UsingStatements.Value.Where(usingStatement => usingStatement.EndsWith("." + type)))
            {
                references.Add(usingStatement + "." + type);
            }
        }
    }

    // Static sets for improved performance
    private static readonly HashSet<string> CommonTypes = new()
    {
        "void", "string", "int", "float", "double", "bool", "decimal",
        "object", "dynamic", "var", "byte", "char", "long", "short",
        "uint", "ulong", "ushort", "sbyte", "DateTime", "TimeSpan",
        "IEnumerable", "IEnumerator", "IList", "List", "Dictionary",
        "HashSet", "Queue", "Stack", "Array", "ICollection",
        "GameObject", "Transform", "Vector2", "Vector3", "Vector4",
        "Quaternion", "Mathf", "Debug", "MonoBehaviour", "Component",
        "Rigidbody", "Rigidbody2D", "Collider", "Collider2D"
    };

    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct",
        "switch", "this", "throw", "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    private static void FilterCommonTypes(HashSet<string> references)
    {
        references.RemoveWhere(r => CommonTypes.Contains(r));
    }

    private static bool IsCSharpKeyword(string word)
    {
        return CSharpKeywords.Contains(word.ToLower());
    }
}
}