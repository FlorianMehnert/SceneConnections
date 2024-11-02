using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ClassParser
{
    private static readonly HashSet<string> UsingStatements = new();
    
    public static List<string> GetClassReferences(string scriptPath)
    {
        var references = new HashSet<string>();
        UsingStatements.Clear();

        // Read the script file
        var content = File.ReadAllText(scriptPath);
        
        // First collect using statements
        CollectUsingStatements(content);

        // Match various patterns where class references might appear
        CollectFieldReferences(content, references);
        CollectMethodParameters(content, references);
        CollectGenericTypes(content, references);
        CollectInheritanceReferences(content, references);
        CollectVariableDeclarations(content, references);
        CollectAttributeReferences(content, references);

        // Remove basic C# types and common Unity types that we don't want to track
        FilterCommonTypes(references);

        return references.ToList();
    }

    private static void CollectUsingStatements(string content)
    {
        var usingRegex = new Regex(@"using\s+(?!static|System)([^;]+);");
        foreach (Match match in usingRegex.Matches(content))
        {
            UsingStatements.Add(match.Groups[1].Value.Trim());
        }
    }

    private static void CollectFieldReferences(string content, HashSet<string> references)
    {
        // Match field declarations including arrays and lists
        var fieldRegex = new Regex(@"(?:private|public|protected|internal)\s+(?:readonly\s+)?([A-Za-z0-9_.<>]+(?:\[\])?)\s+\w+\s*[;={]");
        foreach (Match match in fieldRegex.Matches(content))
        {
            AddReference(match.Groups[1].Value, references);
        }
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
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
            {
                var parameters = match.Groups[2].Value.Split(',');
                foreach (var param in parameters)
                {
                    var paramType = param.Trim().Split(' ')[0];
                    AddReference(paramType, references);
                }
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

    private static void AddReference(string type, HashSet<string> references)
    {
        // Clean up the type name
        type = type.Trim();
        
        // Remove array brackets if present
        type = type.Replace("[]", "");
        
        // Handle generic types
        if (type.Contains("<"))
        {
            type = type.Substring(0, type.IndexOf("<", StringComparison.Ordinal));
        }

        // Skip if it's a C# keyword
        if (IsCSharpKeyword(type))
            return;

        // Add both the simple type name and the fully qualified name if it matches a using statement
        references.Add(type);
        foreach (var usingStatement in UsingStatements)
        {
            if (usingStatement.EndsWith("." + type))
            {
                references.Add(usingStatement + "." + type);
            }
        }
    }

    private static void FilterCommonTypes(HashSet<string> references)
    {
        var commonTypes = new HashSet<string>
        {
            "void", "string", "int", "float", "double", "bool", "decimal",
            "object", "dynamic", "var", "byte", "char", "long", "short",
            "uint", "ulong", "ushort", "sbyte", "DateTime", "TimeSpan",
            "IEnumerable", "IEnumerator", "IList", "List", "Dictionary",
            "HashSet", "Queue", "Stack", "Array", "ICollection",
            // Common Unity types
            "GameObject", "Transform", "Vector2", "Vector3", "Vector4",
            "Quaternion", "Mathf", "Debug", "MonoBehaviour", "Component",
            "Rigidbody", "Rigidbody2D", "Collider", "Collider2D"
        };

        references.RemoveWhere(r => commonTypes.Contains(r));
    }

    private static bool IsCSharpKeyword(string word)
    {
        var keywords = new HashSet<string>
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
        return keywords.Contains(word.ToLower());
    }
}
}