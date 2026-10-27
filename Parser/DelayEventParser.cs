using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PAoC_Mod.Models;

namespace PAoC_Mod.Parser
{
    public class DelayEventParser
    {
        private static readonly Regex DelayBlockRegex = new Regex(
            @"^\s*delay\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex RandomBlockRegex = new Regex(
            @"^\s*random\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex MinBlockRegex = new Regex(
            @"^\s*min\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex MaxBlockRegex = new Regex(
            @"^\s*max\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex EffectBlockRegex = new Regex(
            @"^\s*effect\s*=\s*\{",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex FieldRegex = new Regex(
            @"(?<key>\w+)\s*=\s*(?<value>\d+)",
            RegexOptions.Compiled);

        public DelayEventData Parse(string filePath)
        {
            string content = File.ReadAllText(filePath);
            content = PaocParser.StripComments(content);
            var data = new DelayEventData();
            data.Name = Path.GetFileNameWithoutExtension(filePath);

            Match delayMatch = DelayBlockRegex.Match(content);
            if (delayMatch.Success)
            {
                int openBrace = delayMatch.Index + delayMatch.Length - 1;
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
                    string delayBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    foreach (Match m in FieldRegex.Matches(delayBody))
                    {
                        string key = m.Groups["key"].Value.ToLowerInvariant();
                        int value = int.Parse(m.Groups["value"].Value);
                        switch (key)
                        {
                            case "day":
                            case "days":
                                data.Days = value;
                                break;
                            case "month":
                            case "months":
                                data.Months = value;
                                break;
                            case "year":
                            case "years":
                                data.Years = value;
                                break;
                        }
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'delay' block in {filePath}.");
                }
            }

            Match randomMatch = RandomBlockRegex.Match(content);
            if (randomMatch.Success)
            {
                int openBrace = randomMatch.Index + randomMatch.Length - 1;
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
                    string randomBody = content.Substring(openBrace + 1, i - openBrace - 2);
                    bool hasMinMax = false;

                    Match minMatch = MinBlockRegex.Match(randomBody);
                    if (minMatch.Success)
                    {
                        hasMinMax = true;
                        int minOpen = minMatch.Index + minMatch.Length - 1;
                        int minDepth = 1;
                        int minIdx = minOpen + 1;
                        while (minIdx < randomBody.Length && minDepth > 0)
                        {
                            if (randomBody[minIdx] == '{') minDepth++;
                            else if (randomBody[minIdx] == '}') minDepth--;
                            minIdx++;
                        }
                        if (minDepth == 0)
                        {
                            string minBody = randomBody.Substring(minOpen + 1, minIdx - minOpen - 2);
                            foreach (Match m in FieldRegex.Matches(minBody))
                            {
                                string key = m.Groups["key"].Value.ToLowerInvariant();
                                int value = int.Parse(m.Groups["value"].Value);
                                switch (key)
                                {
                                    case "day":
                                    case "days":
                                        data.RandomMinDays = value;
                                        break;
                                    case "month":
                                    case "months":
                                        data.RandomMinMonths = value;
                                        break;
                                    case "year":
                                    case "years":
                                        data.RandomMinYears = value;
                                        break;
                                }
                            }
                        }
                    }

                    Match maxMatch = MaxBlockRegex.Match(randomBody);
                    if (maxMatch.Success)
                    {
                        hasMinMax = true;
                        int maxOpen = maxMatch.Index + maxMatch.Length - 1;
                        int maxDepth = 1;
                        int maxIdx = maxOpen + 1;
                        while (maxIdx < randomBody.Length && maxDepth > 0)
                        {
                            if (randomBody[maxIdx] == '{') maxDepth++;
                            else if (randomBody[maxIdx] == '}') maxDepth--;
                            maxIdx++;
                        }
                        if (maxDepth == 0)
                        {
                            string maxBody = randomBody.Substring(maxOpen + 1, maxIdx - maxOpen - 2);
                            foreach (Match m in FieldRegex.Matches(maxBody))
                            {
                                string key = m.Groups["key"].Value.ToLowerInvariant();
                                int value = int.Parse(m.Groups["value"].Value);
                                switch (key)
                                {
                                    case "day":
                                    case "days":
                                        data.RandomMaxDays = value;
                                        break;
                                    case "month":
                                    case "months":
                                        data.RandomMaxMonths = value;
                                        break;
                                    case "year":
                                    case "years":
                                        data.RandomMaxYears = value;
                                        break;
                                }
                            }
                        }
                    }

                    if (!hasMinMax)
                    {
                        foreach (Match m in FieldRegex.Matches(randomBody))
                        {
                            string key = m.Groups["key"].Value.ToLowerInvariant();
                            int value = int.Parse(m.Groups["value"].Value);
                            switch (key)
                            {
                                case "day":
                                case "days":
                                    data.RandomMaxDays = value;
                                    break;
                                case "month":
                                case "months":
                                    data.RandomMaxMonths = value;
                                    break;
                                case "year":
                                case "years":
                                    data.RandomMaxYears = value;
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for 'random' block in {filePath}.");
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

            return data;
        }
    }
}