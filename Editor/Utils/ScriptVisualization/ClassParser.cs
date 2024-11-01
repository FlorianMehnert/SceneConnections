using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ClassParser
    {
        public static List<string> GetClassReferences(string scriptPath)
        {
            List<string> references = new List<string>();

            // Read the script file
            string content = File.ReadAllText(scriptPath);

            // Regex to match private or public fields with other class references
            Regex regex = new Regex(@"private|public\s+(\w+)\s+(\w+);");
            MatchCollection matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                string referencedClass = match.Groups[1].Value;
                references.Add(referencedClass);
            }

            return references;
        }
    }
}