using PAoC_Mod.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PAoC_Mod.Parser
{
    public class ConditionalEventParser
    {
        private static readonly Regex EffectBlockRegex = new Regex(
            @"effect\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ConditionsBlockRegex = new Regex(
            @"conditions\s*=\s*(?<op>[^{]*?)\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FieldRegex = new Regex(
            @"(?<key>\w+)\s*=\s*(?:(?<str>""[^""]*"")|(?<num>-?\d+))",
            RegexOptions.Compiled);

        public ConditionalEventData Parse(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = PaocParser.StripComments(content);
            var data = new ConditionalEventData();
            data.Name = Path.GetFileNameWithoutExtension(filePath);

            foreach (Match m in FieldRegex.Matches(content))
            {
                string key = m.Groups["key"].Value.ToLowerInvariant();
                string strValue = m.Groups["str"].Success ? m.Groups["str"].Value.Trim('"') : null;
                string numValue = m.Groups["num"].Success ? m.Groups["num"].Value : null;

                switch (key)
                {
                    case "country":
                        if (int.TryParse(numValue, out int cid))
                            data.CountryId = cid;
                        break;
                }
            }

            Match condMatch = ConditionsBlockRegex.Match(content);
            if (condMatch.Success)
            {
                string opToken = condMatch.Groups["op"].Value.Trim();
                if (!string.IsNullOrEmpty(opToken))
                {
                    if (opToken.Equals("all", StringComparison.OrdinalIgnoreCase))
                        data.Op = ConditionOperator.All;
                    else if (opToken.EndsWith(">"))
                    {
                        if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                        {
                            data.Op = ConditionOperator.GreaterOrEqual;
                            data.Value = val;
                        }
                    }
                    else if (opToken.EndsWith("<"))
                    {
                        if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                        {
                            data.Op = ConditionOperator.LessOrEqual;
                            data.Value = val;
                        }
                    }
                    else if (int.TryParse(opToken, out int exact))
                    {
                        data.Op = ConditionOperator.Exact;
                        data.Value = exact;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Unknown condition operator: '{opToken}', using 'all'.");
                        data.Op = ConditionOperator.All;
                    }
                }
                else
                {
                    data.Op = ConditionOperator.All;
                }

                int openBrace = condMatch.Index + condMatch.Length - 1;
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
                    string condBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    ParseConditionsWithCountryBlocks(condBody, data);
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'conditions' block in {filePath}.");
                }
            }

            Match effMatch = EffectBlockRegex.Match(content);
            if (effMatch.Success)
            {
                int openBrace = effMatch.Index + effMatch.Length - 1;
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
                    var parser = new PaocParser();
                    data.Effect = parser.ParseEffect(effectBody);
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'effect' block in {filePath}.");
                }
            }

            return data;
        }

        private void ParseConditionsWithCountryBlocks(string body, ConditionalEventData data)
        {
            body = PaocParser.StripComments(body);

            var (blocks, leftover) = PaocParser.ExtractBlocksWithLeftover(body);

            if (!string.IsNullOrWhiteSpace(leftover))
            {
                var parser = new PaocParser();
                data.Conditions = parser.ParseConditionList(leftover);
            }

            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();
                if (int.TryParse(trimmedKey, out int countryId))
                {
                    var parser = new PaocParser();
                    var conditions = parser.ParseConditionList(blockBody);
                    if (conditions.Count > 0)
                    {
                        data.CountryConditions[countryId] = conditions;
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unexpected block inside conditions: '{trimmedKey}' (expected country ID)");
                }
            }
        }
    }
}