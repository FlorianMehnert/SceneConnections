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
        private static readonly ThreadLocal<HashSet<string>> UsingStatements = new(() => new HashSet<string>());

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
                        var references = GetClassFieldReferences(scriptPath);
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

        private static List<string> GetClassFieldReferences(string scriptPath)
        {
            var references = new HashSet<string>();
            UsingStatements.Value.Clear();

            string content;
            using (var reader = new StreamReader(scriptPath))
            {
                content = reader.ReadToEnd();
            }

            // First collect using statements
            CollectUsingStatements(content);

            // Then collect field references
            CollectFieldReferences(content, references);

            FilterCommonTypes(references);
            return references.ToList();
        }

        private static void CollectUsingStatements(string content)
        {
            var usingRegex = new Regex(@"using\s+(?!static|System)([^;]+);", RegexOptions.Compiled);
            foreach (Match match in usingRegex.Matches(content))
            {
                UsingStatements.Value.Add(match.Groups[1].Value.Trim());
            }
        }

        private static void CollectFieldReferences(string content, HashSet<string> references)
        {
            // Updated regex to better handle various field declarations
            var fieldRegex = new Regex(
                @"(?:private|public|protected|internal)\s+" + // Access modifier
                @"(?:readonly\s+)?" + // Optional readonly
                @"(?:static\s+)?" + // Optional static
                @"([A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*)" + // Type name with possible nested types
                "(?:<[^>]+>)?" + // Optional generic parameters
                @"\s+\w+\s*" + // Field name
                "(?:[;=]|{[^}]*})", // Ending with ; or = or { ... }
                RegexOptions.Compiled
            );

            foreach (Match match in fieldRegex.Matches(content))
            {
                var fullType = match.Groups[1].Value;
                ProcessTypeReference(fullType, references);

                // If there are generic parameters, extract and process them too
                var genericParamsMatch = Regex.Match(match.Value, "<([^>]+)>");
                if (!genericParamsMatch.Success) continue;
                foreach (var genericType in genericParamsMatch.Groups[1].Value.Split(','))
                {
                    ProcessTypeReference(genericType.Trim(), references);
                }
            }
        }

        private static void ProcessTypeReference(string type, HashSet<string> references)
        {
            // Handle nested types (e.g., Constants.ComponentGraphDrawType)
            var typeComponents = type.Split('.');
            foreach (var component in typeComponents)
            {
                if (!string.IsNullOrWhiteSpace(component))
                {
                    AddReference(component, references);
                }
            }

            // Add the full type as well
            AddReference(type, references);
        }

        private static void AddReference(string type, HashSet<string> references)
        {
            type = type.Trim().Replace("[]", "");

            if (type.Contains("<"))
            {
                // Extract the base type from generic types
                type = type.Substring(0, type.IndexOf("<", StringComparison.Ordinal));
            }

            if (IsCSharpKeyword(type))
                return;

            references.Add(type);

            // Add fully qualified names based on using statements
            foreach (var usingStatement in UsingStatements.Value.Where(us => us.EndsWith("." + type)))
            {
                references.Add(usingStatement + "." + type);
            }
        }

        private static readonly HashSet<string> CommonTypes = new()
        {
            "void", "string", "int", "float", "double", "bool", "decimal", "object", "dynamic", "var", "byte", "char", "long", "short", "uint", "ulong", "ushort", "sbyte", "DateTime", "TimeSpan", "IEnumerable", "IEnumerator", "IList", "List", "Dictionary", "HashSet", "Queue", "Stack", "Array",
            "ICollection", "GameObject", "Transform", "Vector2", "Vector3", "Vector4", "Quaternion", "Mathf", "Debug", "MonoBehaviour", "Component", "Rigidbody", "Rigidbody2D", "Collider", "Collider2D", "Label", "TextField", "Color", "Component", "Node", "Group", "GameObject", "Constants", "List",
            "Dictionary"
        };

        private static readonly HashSet<string> CSharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
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