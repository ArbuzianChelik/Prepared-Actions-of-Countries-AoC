using PAoC_Mod.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PAoC_Mod.Parser
{
    public class EventDefinitionParser
    {
        private static readonly Regex FieldRegex = new Regex(
            "(?<key>\\w+)\\s*=\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.Compiled);

        private static readonly Regex EffectBlockRegex = new Regex(
            @"effect\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WhenActivatedBlockRegex = new Regex(
            @"when_activated\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionsBlockRegex = new Regex(
            @"^\s*options\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex AvailableHeaderRegex = new Regex(
            @"^\s*available\s*=\s*(?<op>[^{]*?)\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public SuperEventData ParseSuperEvent(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = PaocParser.StripComments(content);
            var data = new SuperEventData();

            foreach (Match m in FieldRegex.Matches(content))
            {
                string key = m.Groups["key"].Value.ToUpperInvariant();
                string value = m.Groups["value"].Value;
                switch (key)
                {
                    case "IMAGE": data.ImageFileName = value; break;
                    case "TITLE": data.Title = value; break;
                    case "QUOTE": data.Quote = value; break;
                    case "QUOTE_AUTHOR": data.QuoteAuthor = value; break;
                    case "BUTTON": data.ButtonText = value; break;
                    case "SOUND": data.SoundFileName = value; break;
                }
            }

            Match whenMatch = WhenActivatedBlockRegex.Match(content);
            if (whenMatch.Success)
            {
                int openBrace = whenMatch.Index + whenMatch.Length - 1;
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
                    string whenBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    data.WhenActivated = new PaocParser().ParseEffect(whenBody);
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'when_activated' block in {filePath}.");
                }
            }

            return data;
        }

        public NewsData ParseNews(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = PaocParser.StripComments(content);
            var data = new NewsData();

            foreach (Match m in FieldRegex.Matches(content))
            {
                string key = m.Groups["key"].Value.ToUpperInvariant();
                string value = m.Groups["value"].Value;
                switch (key)
                {
                    case "IMAGE": data.ImageFileName = value; break;
                    case "TITLE": data.Title = value; break;
                    case "DESCRIPTION": data.Description = value; break;
                    case "BUTTON": data.ButtonText = value; break;
                }
            }

            Match effectMatch = EffectBlockRegex.Match(content);
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

            Match whenMatch = WhenActivatedBlockRegex.Match(content);
            if (whenMatch.Success)
            {
                int openBrace = whenMatch.Index + whenMatch.Length - 1;
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
                    string whenBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    data.WhenActivated = new PaocParser().ParseEffect(whenBody);
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'when_activated' block in {filePath}.");
                }
            }

            return data;
        }

        public EventData ParseEvent(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = PaocParser.StripComments(content);
            var data = new EventData();

            foreach (Match m in FieldRegex.Matches(content))
            {
                string key = m.Groups["key"].Value.ToUpperInvariant();
                string value = m.Groups["value"].Value;
                switch (key)
                {
                    case "IMAGE": data.ImageFileName = value; break;
                    case "TITLE": data.Title = value; break;
                    case "DESCRIPTION": data.Description = value; break;
                    case "CLOSE_BUTTON":
                        data.CloseButtonEnabled = value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                                                   value.Equals("1") ||
                                                   value.Equals("enable", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            int optionsStart = OptionsBlockRegex.Match(content).Index;
            if (optionsStart >= 0)
            {
                int openBrace = content.IndexOf('{', optionsStart);
                if (openBrace >= 0)
                {
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
                        string optionsBody = content.Substring(openBrace + 1, i - openBrace - 2);
                        ParseOptions(optionsBody, data);
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Missing closing brace for 'options' block in {filePath}.");
                    }
                }
            }

            return data;
        }

        private void ParseOptions(string body, EventData data)
        {
            var (blocks, leftover) = PaocParser.ExtractBlocksWithLeftover(body);
            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();
                if (trimmedKey.StartsWith("option_", StringComparison.OrdinalIgnoreCase))
                {
                    string optionName = trimmedKey.Substring(7).Trim();
                    if (data.Options.Count >= 5)
                    {
                        Plugin.Logger.LogWarning($"Too many options in event, max 5. Skipping '{optionName}'.");
                        continue;
                    }
                    var option = new EventOption();

                    var (subBlocks, subLeftover) = PaocParser.ExtractBlocksWithLeftover(blockBody);
                    foreach (Match match in FieldRegex.Matches(subLeftover))
                    {
                        string subKey = match.Groups["key"].Value.ToUpperInvariant();
                        if (subKey == "NAME")
                        {
                            option.Name = match.Groups["value"].Value;
                            break;
                        }
                    }

                    foreach (var (subKey, subBlockBody) in subBlocks)
                    {
                        string subTrimmed = subKey.Trim();
                        if (subTrimmed.Equals("effect", StringComparison.OrdinalIgnoreCase))
                        {
                            option.Effect = new PaocParser().ParseEffect(subBlockBody);
                        }
                    }

                    Match availableMatch = AvailableHeaderRegex.Match(blockBody);
                    if (availableMatch.Success)
                    {
                        string opToken = availableMatch.Groups["op"].Value.Trim();
                        int availableStart = availableMatch.Index;
                        int openBrace = availableMatch.Index + availableMatch.Length - 1;
                        int depth = 1;
                        int i = openBrace + 1;
                        while (i < blockBody.Length && depth > 0)
                        {
                            if (blockBody[i] == '{') depth++;
                            else if (blockBody[i] == '}') depth--;
                            i++;
                        }
                        if (depth == 0)
                        {
                            string availableBody = blockBody.Substring(openBrace + 1, i - openBrace - 2);
                            option.AvailableConditions = new PaocParser().ParseConditionList(availableBody);

                            if (!string.IsNullOrEmpty(opToken))
                            {
                                if (opToken.Equals("all", StringComparison.OrdinalIgnoreCase))
                                {
                                    option.AvailableOp = ConditionOperator.All;
                                }
                                else if (opToken.EndsWith(">"))
                                {
                                    if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                                    {
                                        option.AvailableOp = ConditionOperator.GreaterOrEqual;
                                        option.AvailableValue = val;
                                    }
                                    else
                                    {
                                        option.AvailableOp = ConditionOperator.All;
                                    }
                                }
                                else if (opToken.EndsWith("<"))
                                {
                                    if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                                    {
                                        option.AvailableOp = ConditionOperator.LessOrEqual;
                                        option.AvailableValue = val;
                                    }
                                    else
                                    {
                                        option.AvailableOp = ConditionOperator.All;
                                    }
                                }
                                else if (int.TryParse(opToken, out int exact))
                                {
                                    option.AvailableOp = ConditionOperator.Exact;
                                    option.AvailableValue = exact;
                                }
                                else
                                {
                                    Plugin.Logger.LogWarning($"Unknown operator '{opToken}' for available block, using 'all'.");
                                    option.AvailableOp = ConditionOperator.All;
                                }
                            }
                            else
                            {
                                option.AvailableOp = ConditionOperator.All;
                            }
                            Plugin.Logger.LogInfo($"Parsed available block for option '{optionName}' with {option.AvailableConditions.Count} conditions, op={option.AvailableOp}, value={option.AvailableValue}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"Missing closing brace for available block in option '{optionName}'.");
                        }
                    }

                    if (!string.IsNullOrEmpty(option.Name))
                    {
                        data.Options.Add(option);
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Option '{optionName}' missing 'name' field, skipping.");
                    }
                }
            }
        }
    }
}