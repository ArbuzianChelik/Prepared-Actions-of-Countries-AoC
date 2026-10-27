using System.IO;
using System.Text.RegularExpressions;
using PAoC_Mod.Models;

namespace PAoC_Mod.Parser
{
    public class KeyEventParser
    {
        private static readonly Regex KeyRegex = new Regex(@"^\s*key\s*=\s*""(?<key>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EffectBlockRegex = new Regex(@"effect\s*=\s*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public KeyEventData Parse(string filePath)
        {
            string content = File.ReadAllText(filePath);
            var data = new KeyEventData();
            data.Name = Path.GetFileNameWithoutExtension(filePath);

            var keyMatch = KeyRegex.Match(content);
            if (keyMatch.Success)
                data.Key = keyMatch.Groups["key"].Value;

            var effectMatch = EffectBlockRegex.Match(content);
            if (effectMatch.Success)
            {
                int openBrace = effectMatch.Index + effectMatch.Length - 1;
                int depth = 1;
                int i = openBrace + 1;
                while (i < content.Length && depth > 0)
                {
                    if (content[i] == '{') depth++;
                    else if (content[i] == '}') depth--;
                    i++;
                }
                if (depth == 0)
                {
                    string effectBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    data.Effect = new PaocParser().ParseEffect(effectBody);
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'effect' block in {filePath}.");
                }
            }

            return data;
        }
    }
}