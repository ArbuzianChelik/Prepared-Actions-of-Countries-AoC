using PAoC_Mod.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PAoC_Mod.Parser
{
    public class PaocParser
    {
        private static readonly Regex BlockHeaderRegex = new Regex(@"\s*(?<key>[^{}=\r\n]+?)\s*=\s*\{", RegexOptions.Compiled);
        private static readonly Regex CommandTokenRegex = new Regex(
            @"^(?<cmd>\w+|var:\w+)\s*=\s*
            (?:
                (?<mode>add|set)\s+(?<amount>-?\d+) |
                (?<num1>\d+)\s+(?<num2>\d+) |
                (?<str1>""[^""]*"")\s+(?<str2>""[^""]*"") |
                (?<str_num_str>""[^""]*"")\s+(?<str_num_num>\d+) |
                (?<str_word>""[^""]*"")\s+(?<word>\w+) |
                (?<str>""[^""]*"") |
                (?<num_str_num>\d+)\s+""(?<num_str_str>[^""]*)"" |
                (?<float_num>\d+\.\d+) |
                (?<num_op>-?\d+)\s*(?<op>[><])? |
                (?<str_unquoted>[^""\s=]+)
            )\s*$ |
            ^(?<standalone>\w+)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex VarLineRegex = new Regex(@"^\s*(?<key>\w+)\s*=\s*(?:""(?<value>[^""]*)""|(?<value2>[^\s""=]+))\s*$", RegexOptions.Multiline);
        private static readonly Regex VarConditionRegex = new Regex(@"var:(\w+)\s*([><])\s*(\d+)", RegexOptions.Compiled);

        private static readonly HashSet<string> SupportedCommands = new HashSet<string>
        {
            "attack", "peace", "fight_to_death", "join_faction", "leave_faction",
            "puppet", "release", "rename", "recolor", "die",
            "rename_faction", "recolor_faction", "disband_faction", "power", "money",
            "annex", "revolt", "superevent", "news", "power_lock",
            "rename_city", "ai", "conditional_event", "sound",
            "move_camera_to_country", "move_camera_to_city",
            "set_flag", "set_flag_to_country", "event", "key_event", "delay_event",
            "set_autonomy", "change_rightful_owner", "set_loyalty"
        };

        private static readonly HashSet<string> SupportedConditions = new HashSet<string>
        {
            "in_war", "in_war_with", "name_is", "is_puppet", "is_puppet_of",
            "is_in_faction", "is_in_faction_with",
            "controls_city", "country_controls_city",
            "power_is", "has_gold", "country_exist",
            "destroyed", "destroyed_by", "own_tiles",
            "city_rightful_to", "revolted", "revolted_from", "revolt_able",
            "color_is", "autonomy_is", "country_autonomy_is",
            "loyalty_is", "country_loyalty_is", "country_name_is"
        };

        public static string StripComments(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;
            var sb = new StringBuilder(content.Length);
            bool inString = false;
            int i = 0;
            while (i < content.Length)
            {
                char c = content[i];
                if (c == '"')
                {
                    inString = !inString;
                    sb.Append(c);
                    i++;
                }
                else if (c == '#' && !inString)
                {
                    while (i < content.Length && content[i] != '\n') i++;
                    if (i < content.Length)
                    {
                        sb.Append('\n');
                        i++;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        public Dictionary<DateTime, List<ScheduleNode>> Parse(string filePath)
        {
            var result = new Dictionary<DateTime, List<ScheduleNode>>();
            string content = File.ReadAllText(filePath);
            content = StripComments(content);

            Plugin.Logger.LogInfo($"Parsing file: {filePath}, {content.Length} characters");

            var (dateBlocks, topLeftover) = ExtractBlocksWithLeftover(content);
            if (!string.IsNullOrWhiteSpace(topLeftover))
                Plugin.Logger.LogWarning("Unexpected content outside of date blocks.");

            Plugin.Logger.LogInfo($"Found {dateBlocks.Count} date block(s)");

            foreach (var (dateStr, body) in dateBlocks)
            {
                if (!DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    Plugin.Logger.LogWarning($"Invalid date format: {dateStr}");
                    continue;
                }

                var nodes = ParseNodes(body, null);
                if (nodes.Count > 0)
                {
                    if (result.TryGetValue(date, out var existing))
                        existing.AddRange(nodes);
                    else
                        result[date] = nodes;

                    Plugin.Logger.LogInfo($"Date {dateStr}: {nodes.Count} top-level node(s) scheduled");
                }
                else
                {
                    Plugin.Logger.LogWarning($"Date {dateStr}: no valid content found");
                }
            }

            Plugin.Logger.LogInfo($"Parsing complete, {result.Count} date(s) loaded");
            return result;
        }

        public List<ScheduleNode> ParseEffect(string content)
        {
            content = StripComments(content);
            return ParseNodes(content, null);
        }

        public List<ConditionCheck> ParseConditionList(string body)
        {
            body = StripComments(body);
            var conditions = new List<ConditionCheck>();
            var (blocks, leftover) = ExtractBlocksWithLeftover(body);

            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();
                if (trimmedKey.Equals("not", StringComparison.OrdinalIgnoreCase))
                {
                    var notCondition = ParseNotBlock(trimmedKey, blockBody);
                    if (notCondition != null)
                        conditions.Add(notCondition);
                }
                else if (int.TryParse(trimmedKey, out int countryId))
                {
                    var nestedConditions = ParseConditionList(blockBody);
                    foreach (var cond in nestedConditions)
                    {
                        cond.CountryId = countryId;
                        conditions.Add(cond);
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unexpected block in condition list: '{trimmedKey}' (expected country ID or 'not')");
                }
            }

            var lines = leftover.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                var match = CommandTokenRegex.Match(trimmedLine);
                if (!match.Success)
                {
                    Plugin.Logger.LogWarning($"Could not parse condition line: '{trimmedLine}'");
                    continue;
                }
                var check = BuildConditionCheck(match);
                if (check != null)
                    conditions.Add(check);
            }

            return conditions;
        }

        private ConditionCheck ParseNotBlock(string key, string blockBody)
        {
            string opToken = "";
            int eqIndex = key.IndexOf('=');
            if (eqIndex >= 0)
            {
                opToken = key.Substring(eqIndex + 1).Trim();
            }

            var notCheck = new ConditionCheck
            {
                IsNegatedGroup = true,
                NestedConditions = ParseConditionList(blockBody)
            };

            if (!string.IsNullOrEmpty(opToken))
            {
                if (opToken.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    notCheck.NotOperator = ConditionOperator.All;
                }
                else if (opToken.EndsWith(">"))
                {
                    if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                    {
                        notCheck.NotOperator = ConditionOperator.GreaterOrEqual;
                        notCheck.NotValue = val;
                    }
                    else
                    {
                        notCheck.NotOperator = ConditionOperator.All;
                    }
                }
                else if (opToken.EndsWith("<"))
                {
                    if (int.TryParse(opToken.Substring(0, opToken.Length - 1).Trim(), out int val))
                    {
                        notCheck.NotOperator = ConditionOperator.LessOrEqual;
                        notCheck.NotValue = val;
                    }
                    else
                    {
                        notCheck.NotOperator = ConditionOperator.All;
                    }
                }
                else if (int.TryParse(opToken, out int exact))
                {
                    notCheck.NotOperator = ConditionOperator.Exact;
                    notCheck.NotValue = exact;
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unknown operator '{opToken}' for not block, using 'all'.");
                    notCheck.NotOperator = ConditionOperator.All;
                }
            }
            else
            {
                notCheck.NotOperator = ConditionOperator.All;
            }

            Plugin.Logger.LogInfo($"Parsed not block with {notCheck.NestedConditions.Count} nested conditions, op={notCheck.NotOperator}, value={notCheck.NotValue}");
            return notCheck;
        }

        private List<ScheduleNode> ParseNodes(string body, int? currentCountryId)
        {
            var nodes = new List<ScheduleNode>();
            var (blocks, leftover) = ExtractBlocksWithLeftover(body);

            var lines = leftover.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                var match = CommandTokenRegex.Match(trimmedLine);
                if (!match.Success)
                {
                    Plugin.Logger.LogWarning($"Could not parse line: '{trimmedLine}'");
                    continue;
                }

                string cmdName;
                bool isStandalone = match.Groups["standalone"].Success;
                if (isStandalone)
                {
                    cmdName = match.Groups["standalone"].Value.ToLowerInvariant();
                }
                else
                {
                    cmdName = match.Groups["cmd"].Value.ToLowerInvariant();
                }

                bool isGlobalCommand = cmdName == "superevent" || cmdName == "news" || cmdName == "ai" || cmdName == "rename_city" ||
                                        cmdName == "conditional_event" || cmdName == "sound" ||
                                        cmdName == "move_camera_to_country" || cmdName == "move_camera_to_city" ||
                                        cmdName == "set_flag" || cmdName == "set_flag_to_country" || cmdName == "event" ||
                                        cmdName == "revolt" || cmdName == "key_event" || cmdName == "delay_event" ||
                                        cmdName == "set_autonomy" || cmdName == "change_rightful_owner" || cmdName == "set_loyalty";

                if (!currentCountryId.HasValue && !isGlobalCommand)
                {
                    Plugin.Logger.LogWarning($"Command '{cmdName}' outside country block, ignoring.");
                    continue;
                }

                var entry = BuildActionEntry(match, currentCountryId ?? 0);
                if (entry != null)
                    nodes.Add(new ActionNode { Action = entry });
            }

            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();

                if (int.TryParse(trimmedKey, out int countryId))
                {
                    nodes.Add(new CountryNode
                    {
                        CountryId = countryId,
                        Children = ParseNodes(blockBody, countryId)
                    });
                }
                else if (TryParsePercent(trimmedKey, out int chancePercent))
                {
                    nodes.Add(new ChanceNode
                    {
                        Percent = chancePercent,
                        Children = ParseNodes(blockBody, currentCountryId)
                    });
                }
                else if (trimmedKey.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    nodes.Add(ParseRandomNode(blockBody, currentCountryId));
                }
                else if (trimmedKey.Equals("if", StringComparison.OrdinalIgnoreCase))
                {
                    int contextId = currentCountryId ?? 0;
                    nodes.Add(ParseIfNode(blockBody, contextId));
                }
                else if (trimmedKey.Equals("everyone", StringComparison.OrdinalIgnoreCase))
                {
                    nodes.Add(new EveryoneNode
                    {
                        Children = ParseNodes(blockBody, -1)
                    });
                }
                else if (trimmedKey.Equals("var", StringComparison.OrdinalIgnoreCase) || trimmedKey.Equals("variable", StringComparison.OrdinalIgnoreCase))
                {
                    nodes.Add(ParseVariableNode(blockBody));
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unexpected block '{trimmedKey}'.");
                }
            }

            return nodes;
        }

        private RandomNode ParseRandomNode(string body, int? currentCountryId)
        {
            var (blocks, leftover) = ExtractBlocksWithLeftover(body);
            if (!string.IsNullOrWhiteSpace(leftover))
                Plugin.Logger.LogWarning("Unexpected content directly inside a random block.");

            var node = new RandomNode();

            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();
                if (TryParsePercent(trimmedKey, out int weight))
                {
                    node.Options.Add(new RandomOption
                    {
                        Weight = weight,
                        Children = ParseNodes(blockBody, currentCountryId)
                    });
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unexpected key '{trimmedKey}' inside random block, expected N%.");
                }
            }

            return node;
        }

        private ConditionBlockNode ParseIfNode(string body, int countryId)
        {
            var (blocks, leftover) = ExtractBlocksWithLeftover(body);
            leftover = VarConditionRegex.Replace(leftover, "var:$1 = $3$2");
            var node = new ConditionBlockNode { CountryId = countryId };

            var lines = leftover.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                var match = CommandTokenRegex.Match(trimmedLine);
                if (!match.Success)
                {
                    Plugin.Logger.LogWarning($"Could not parse condition line in if: '{trimmedLine}'");
                    continue;
                }
                var check = BuildConditionCheck(match);
                if (check != null)
                    node.Conditions.Add(check);
            }

            foreach (var (key, blockBody) in blocks)
            {
                string trimmedKey = key.Trim();

                if (trimmedKey.Equals("then", StringComparison.OrdinalIgnoreCase))
                {
                    node.ThenBranches.Add(new ThenBranch
                    {
                        Op = ConditionOperator.All,
                        Children = ParseNodes(blockBody, countryId)
                    });
                }
                else if (trimmedKey.StartsWith("then", StringComparison.OrdinalIgnoreCase))
                {
                    string spec = trimmedKey.Length > 4 ? trimmedKey.Substring(4).Trim() : "";
                    var branch = ParseThenSpec(spec);
                    if (branch != null)
                    {
                        branch.Children = ParseNodes(blockBody, countryId);
                        node.ThenBranches.Add(branch);
                    }
                }
                else if (trimmedKey.Equals("else", StringComparison.OrdinalIgnoreCase))
                {
                    node.ElseBranches.Add(new ElseBranch
                    {
                        Op = ConditionOperator.All,
                        Children = ParseNodes(blockBody, countryId)
                    });
                }
                else if (trimmedKey.StartsWith("else", StringComparison.OrdinalIgnoreCase))
                {
                    string spec = trimmedKey.Length > 4 ? trimmedKey.Substring(4).Trim() : "";
                    var branch = ParseElseSpec(spec);
                    if (branch != null)
                    {
                        branch.Children = ParseNodes(blockBody, countryId);
                        node.ElseBranches.Add(branch);
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Unexpected block '{trimmedKey}' inside an if block.");
                }
            }

            return node;
        }

        private ThenBranch ParseThenSpec(string spec)
        {
            spec = spec.Trim();

            if (spec.Equals("all", StringComparison.OrdinalIgnoreCase))
                return new ThenBranch { Op = ConditionOperator.All };

            if (spec.EndsWith(">"))
            {
                if (int.TryParse(spec.Substring(0, spec.Length - 1).Trim(), out int v))
                    return new ThenBranch { Op = ConditionOperator.GreaterOrEqual, Value = v };
            }
            else if (spec.EndsWith("<"))
            {
                if (int.TryParse(spec.Substring(0, spec.Length - 1).Trim(), out int v))
                    return new ThenBranch { Op = ConditionOperator.LessOrEqual, Value = v };
            }
            else if (int.TryParse(spec, out int exact))
            {
                return new ThenBranch { Op = ConditionOperator.Exact, Value = exact };
            }

            Plugin.Logger.LogWarning($"Invalid then specifier: 'then {spec}'.");
            return null;
        }

        private ElseBranch ParseElseSpec(string spec)
        {
            spec = spec.Trim();

            if (spec.Equals("all", StringComparison.OrdinalIgnoreCase))
                return new ElseBranch { Op = ConditionOperator.All };

            if (spec.EndsWith(">"))
            {
                if (int.TryParse(spec.Substring(0, spec.Length - 1).Trim(), out int v))
                    return new ElseBranch { Op = ConditionOperator.GreaterOrEqual, Value = v };
            }
            else if (spec.EndsWith("<"))
            {
                if (int.TryParse(spec.Substring(0, spec.Length - 1).Trim(), out int v))
                    return new ElseBranch { Op = ConditionOperator.LessOrEqual, Value = v };
            }
            else if (int.TryParse(spec, out int exact))
            {
                return new ElseBranch { Op = ConditionOperator.Exact, Value = exact };
            }

            Plugin.Logger.LogWarning($"Invalid else specifier: 'else {spec}'.");
            return null;
        }

        private static bool TryParsePercent(string key, out int percent)
        {
            percent = 0;
            if (!key.EndsWith("%"))
                return false;
            return int.TryParse(key.Substring(0, key.Length - 1).Trim(), out percent);
        }

        private VariableNode ParseVariableNode(string body)
        {
            var node = new VariableNode();
            var lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                var match = VarLineRegex.Match(trimmedLine);
                if (match.Success)
                {
                    string key = match.Groups["key"].Value.ToLowerInvariant();
                    string value = match.Groups["value"].Success ? match.Groups["value"].Value : match.Groups["value2"].Value;
                    if (key == "name")
                    {
                        if (string.IsNullOrEmpty(value))
                            Plugin.Logger.LogWarning("Variable name is empty.");
                        else if (value.Contains(" "))
                            Plugin.Logger.LogWarning($"Variable name '{value}' contains spaces, which is not allowed.");
                        else
                            node.Name = value;
                    }
                    else if (key == "set")
                    {
                        node.Operation = VariableOperation.Set;
                        node.Value = value;
                    }
                    else if (key == "add")
                    {
                        node.Operation = VariableOperation.Add;
                        node.Value = value;
                    }
                    else if (key == "subtract")
                    {
                        node.Operation = VariableOperation.Subtract;
                        node.Value = value;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Unknown command inside var block: '{key}'");
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Could not parse line in var block: '{trimmedLine}'");
                }
            }

            if (string.IsNullOrEmpty(node.Name))
                Plugin.Logger.LogWarning("Variable block missing 'name' field.");

            return node;
        }

        private ActionEntry BuildActionEntry(Match match, int countryId)
        {
            bool isStandalone = match.Groups["standalone"].Success;
            string cmd;
            if (isStandalone)
            {
                cmd = match.Groups["standalone"].Value.ToLowerInvariant();
            }
            else
            {
                cmd = match.Groups["cmd"].Value.ToLowerInvariant();
            }

            if (!SupportedCommands.Contains(cmd))
            {
                Plugin.Logger.LogWarning($"Unsupported command: '{cmd}'");
                return null;
            }

            var token = isStandalone ? new CommandToken { Cmd = cmd } : ReadCommandToken(match);
            var entry = new ActionEntry
            {
                CountryId = countryId,
                Command = token.Cmd,
                TargetId = token.TargetId,
                SecondaryTargetId = token.SecondaryTargetId,
                TextValue = token.TextValue,
                SecondaryTextValue = token.SecondaryTextValue,
                Mode = token.Mode,
                Amount = token.Amount
            };

            if (token.Cmd == "revolt")
            {
                if (!entry.TargetId.HasValue && !string.IsNullOrEmpty(entry.TextValue) && int.TryParse(entry.TextValue, out int t1))
                {
                    entry.TargetId = t1;
                    entry.TextValue = null;
                }
                if (!entry.SecondaryTargetId.HasValue && !string.IsNullOrEmpty(entry.SecondaryTextValue) && int.TryParse(entry.SecondaryTextValue, out int t2))
                {
                    entry.SecondaryTargetId = t2;
                    entry.SecondaryTextValue = null;
                }
            }

            if (token.Cmd == "power_lock" && token.TargetId.HasValue)
            {
                entry.Amount = token.TargetId.Value;
                entry.TargetId = null;
            }

            if (token.Cmd == "set_autonomy")
            {
                if (token.SecondaryTargetId.HasValue)
                {
                    entry.CountryId = token.TargetId.Value;
                    entry.Amount = token.SecondaryTargetId.Value;
                    entry.TargetId = null;
                    entry.SecondaryTargetId = null;
                }
                else if (token.TargetId.HasValue)
                {
                    if (countryId > 0)
                    {
                        entry.CountryId = countryId;
                        entry.Amount = token.TargetId.Value;
                        entry.TargetId = null;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Command 'set_autonomy' outside a country block requires two values: ID and value.");
                        return null;
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning("Command 'set_autonomy' requires a value.");
                    return null;
                }
            }

            if (token.Cmd == "set_loyalty")
            {
                if (token.SecondaryTargetId.HasValue)
                {
                    entry.CountryId = token.TargetId.Value;
                    entry.Amount = token.SecondaryTargetId.Value;
                    entry.TargetId = null;
                    entry.SecondaryTargetId = null;
                }
                else if (token.TargetId.HasValue)
                {
                    if (countryId > 0)
                    {
                        entry.CountryId = countryId;
                        entry.Amount = token.TargetId.Value;
                        entry.TargetId = null;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Command 'set_loyalty' outside a country block requires two values: ID and value.");
                        return null;
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning("Command 'set_loyalty' requires a value.");
                    return null;
                }
            }

            if (token.Cmd == "change_rightful_owner")
            {
                if (!token.TargetId.HasValue || string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'change_rightful_owner' requires a country ID and a city name.");
                    return null;
                }
                entry.TargetId = token.TargetId;
                entry.TextValue = token.TextValue;
                entry.CountryId = 0;
            }

            if (token.Cmd == "ai")
            {
                if (token.SecondaryTargetId.HasValue)
                {
                    entry.CountryId = token.TargetId.Value;
                    entry.Amount = token.SecondaryTargetId.Value;
                    entry.TargetId = null;
                    entry.SecondaryTargetId = null;
                }
                else if (token.TargetId.HasValue)
                {
                    if (countryId == -1)
                    {
                        entry.CountryId = 0;
                        entry.Amount = token.TargetId.Value;
                        entry.TargetId = null;
                    }
                    else if (countryId > 0)
                    {
                        entry.CountryId = countryId;
                        entry.Amount = token.TargetId.Value;
                        entry.TargetId = null;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Command 'ai' outside a country block requires two values: ID and 0/1.");
                        return null;
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning("Command 'ai' requires a value (0 or 1) or two values (ID and 0/1).");
                    return null;
                }
            }

            if (token.Cmd == "conditional_event")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'conditional_event' requires an event name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
                entry.SecondaryTextValue = token.SecondaryTextValue;
                if (string.IsNullOrEmpty(entry.SecondaryTextValue) && token.Amount.HasValue)
                {
                    entry.SecondaryTextValue = token.Amount.Value == 0 ? "off" : "on";
                }
                if (string.IsNullOrEmpty(entry.SecondaryTextValue))
                    entry.SecondaryTextValue = "on";
            }

            if (token.Cmd == "key_event")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'key_event' requires an event name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
                entry.SecondaryTextValue = token.SecondaryTextValue;
                if (string.IsNullOrEmpty(entry.SecondaryTextValue) && token.Amount.HasValue)
                {
                    entry.SecondaryTextValue = token.Amount.Value == 0 ? "off" : "on";
                }
                if (string.IsNullOrEmpty(entry.SecondaryTextValue))
                    entry.SecondaryTextValue = "on";
            }

            if (token.Cmd == "delay_event")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'delay_event' requires an event name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
                entry.SecondaryTextValue = token.SecondaryTextValue;
                if (string.IsNullOrEmpty(entry.SecondaryTextValue) && token.Amount.HasValue)
                {
                    entry.SecondaryTextValue = token.Amount.Value == 0 ? "off" : "on";
                }
                if (string.IsNullOrEmpty(entry.SecondaryTextValue))
                    entry.SecondaryTextValue = "on";
            }

            if (token.Cmd == "sound")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'sound' requires a file name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
                entry.SecondaryTextValue = token.SecondaryTextValue;
                if (!string.IsNullOrEmpty(entry.SecondaryTextValue) && entry.SecondaryTextValue != "on" && entry.SecondaryTextValue != "off" && entry.SecondaryTextValue != "enable" && entry.SecondaryTextValue != "disable" && entry.SecondaryTextValue != "1" && entry.SecondaryTextValue != "0" && entry.SecondaryTextValue != "yes" && entry.SecondaryTextValue != "no")
                {
                    Plugin.Logger.LogWarning($"Ignoring unknown mode '{entry.SecondaryTextValue}' for sound.");
                    entry.SecondaryTextValue = "off";
                }
                if (string.IsNullOrEmpty(entry.SecondaryTextValue))
                    entry.SecondaryTextValue = "off";
            }

            if (token.Cmd == "move_camera_to_country" || token.Cmd == "move_camera_to_city")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning($"Command '{token.Cmd}' requires a name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
            }

            if (token.Cmd == "set_flag")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'set_flag' requires a file name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
            }

            if (token.Cmd == "set_flag_to_country")
            {
                if (string.IsNullOrEmpty(token.TextValue) || !token.TargetId.HasValue)
                {
                    Plugin.Logger.LogWarning("Command 'set_flag_to_country' requires a file name and a country ID.");
                    return null;
                }
                entry.TextValue = token.TextValue;
                entry.TargetId = token.TargetId;
                entry.CountryId = 0;
            }

            if (token.Cmd == "event")
            {
                if (string.IsNullOrEmpty(token.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'event' requires a folder name.");
                    return null;
                }
                entry.TextValue = token.TextValue;
            }

            Plugin.Logger.LogInfo($"Command added: country {entry.CountryId} -> {token.Cmd}{DescribeValue(token)}");

            return entry;
        }

        private ConditionCheck BuildConditionCheck(Match match)
        {
            if (match.Groups["standalone"].Success)
            {
                string cmd = match.Groups["standalone"].Value.ToLowerInvariant();
                if (!SupportedConditions.Contains(cmd))
                {
                    Plugin.Logger.LogWarning($"Unsupported standalone condition: '{cmd}'");
                    return null;
                }
                return new ConditionCheck
                {
                    Type = cmd,
                    TargetId = null,
                    TextValue = null
                };
            }

            var token = ReadCommandToken(match);
            if (token.Cmd == null)
                return null;

            if (token.Cmd.StartsWith("var:"))
            {
                string varName = token.Cmd.Substring(4);
                string value = token.TextValue;
                string op = "";
                if (token.TargetId.HasValue)
                {
                    value = token.TargetId.Value.ToString();
                    if (!string.IsNullOrEmpty(token.TextValue))
                        op = token.TextValue;
                }
                if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(token.TextValue))
                    value = token.TextValue;
                return new ConditionCheck
                {
                    Type = "var",
                    VariableName = varName,
                    TextValue = value,
                    VariableOperator = op
                };
            }

            if (token.Cmd == "autonomy_is")
            {
                string val = token.TextValue;
                if (string.IsNullOrEmpty(val) && token.TargetId.HasValue)
                    val = token.TargetId.Value.ToString();
                return new ConditionCheck
                {
                    Type = "autonomy_is",
                    TextValue = val
                };
            }

            if (token.Cmd == "country_autonomy_is")
            {
                string val = token.TextValue;
                if (string.IsNullOrEmpty(val) && token.SecondaryTargetId.HasValue)
                    val = token.SecondaryTargetId.Value.ToString();
                return new ConditionCheck
                {
                    Type = "country_autonomy_is",
                    TargetId = token.TargetId,
                    TextValue = val
                };
            }

            if (token.Cmd == "loyalty_is")
            {
                string val = token.TextValue;
                if (string.IsNullOrEmpty(val) && token.TargetId.HasValue)
                    val = token.TargetId.Value.ToString();
                return new ConditionCheck
                {
                    Type = "loyalty_is",
                    TextValue = val
                };
            }

            if (token.Cmd == "country_loyalty_is")
            {
                string val = token.TextValue;
                if (string.IsNullOrEmpty(val) && token.SecondaryTargetId.HasValue)
                    val = token.SecondaryTargetId.Value.ToString();
                return new ConditionCheck
                {
                    Type = "country_loyalty_is",
                    TargetId = token.TargetId,
                    TextValue = val
                };
            }

            if (token.Cmd == "country_name_is")
            {
                return new ConditionCheck
                {
                    Type = "country_name_is",
                    TargetId = token.TargetId,
                    TextValue = token.TextValue
                };
            }

            if (!SupportedConditions.Contains(token.Cmd))
            {
                Plugin.Logger.LogWarning($"Unsupported condition: '{token.Cmd}'");
                return null;
            }

            return new ConditionCheck
            {
                Type = token.Cmd,
                TargetId = token.TargetId,
                TextValue = token.TextValue
            };
        }

        private struct CommandToken
        {
            public string Cmd;
            public int? TargetId;
            public int? SecondaryTargetId;
            public string TextValue;
            public string SecondaryTextValue;
            public string Mode;
            public int? Amount;
        }

        private static CommandToken ReadCommandToken(Match match)
        {
            var token = new CommandToken();

            if (match.Groups["cmd"].Success)
            {
                token.Cmd = match.Groups["cmd"].Value.ToLower();
                if (match.Groups["mode"].Success)
                {
                    token.Mode = match.Groups["mode"].Value.ToLower();
                    token.Amount = int.Parse(match.Groups["amount"].Value);
                }
                else if (match.Groups["num1"].Success && match.Groups["num2"].Success)
                {
                    token.TargetId = int.Parse(match.Groups["num1"].Value);
                    token.SecondaryTargetId = int.Parse(match.Groups["num2"].Value);
                }
                else if (match.Groups["str1"].Success && match.Groups["str2"].Success)
                {
                    token.TextValue = match.Groups["str1"].Value.Trim('"');
                    token.SecondaryTextValue = match.Groups["str2"].Value.Trim('"');
                }
                else if (match.Groups["str_num_str"].Success && match.Groups["str_num_num"].Success)
                {
                    token.TextValue = match.Groups["str_num_str"].Value.Trim('"');
                    token.TargetId = int.Parse(match.Groups["str_num_num"].Value);
                }
                else if (match.Groups["str_word"].Success && match.Groups["word"].Success)
                {
                    token.TextValue = match.Groups["str_word"].Value.Trim('"');
                    token.SecondaryTextValue = match.Groups["word"].Value.ToLower();
                }
                else if (match.Groups["str"].Success)
                {
                    token.TextValue = match.Groups["str"].Value.Trim('"');
                }
                else if (match.Groups["num_str_num"].Success && match.Groups["num_str_str"].Success)
                {
                    token.TargetId = int.Parse(match.Groups["num_str_num"].Value);
                    token.TextValue = match.Groups["num_str_str"].Value;
                }
                else if (match.Groups["float_num"].Success)
                {
                    token.TextValue = match.Groups["float_num"].Value;
                }
                else if (match.Groups["num_op"].Success)
                {
                    token.TargetId = int.Parse(match.Groups["num_op"].Value);
                    string op = match.Groups["op"].Value;
                    if (!string.IsNullOrEmpty(op))
                        token.TextValue = op;
                }
                else if (match.Groups["str_unquoted"].Success)
                {
                    token.TextValue = match.Groups["str_unquoted"].Value;
                }
            }
            else
            {
                token.Cmd = match.Groups["standalone"].Value.ToLower();
            }

            return token;
        }

        private static string DescribeValue(CommandToken token)
        {
            if (token.SecondaryTargetId.HasValue) return $" targets={token.TargetId},{token.SecondaryTargetId}";
            if (token.TargetId.HasValue && token.TextValue != null) return $" target={token.TargetId} op={token.TextValue}";
            if (token.TargetId.HasValue) return $" target={token.TargetId}";
            if (token.TextValue != null && token.SecondaryTextValue != null) return $" \"{token.TextValue}\" {token.SecondaryTextValue}";
            if (token.TextValue != null) return $" value=\"{token.TextValue}\"";
            if (token.Mode != null) return $" {token.Mode} {token.Amount}";
            return "";
        }

        public static (List<(string Key, string Body)> Blocks, string Leftover) ExtractBlocksWithLeftover(string text)
        {
            var blocks = new List<(string, string)>();
            var leftover = new StringBuilder();
            int pos = 0;
            int copiedUpTo = 0;

            while (pos < text.Length)
            {
                Match header = BlockHeaderRegex.Match(text, pos);
                if (!header.Success)
                    break;

                int openBraceIndex = header.Index + header.Length - 1;
                int depth = 1;
                int i = openBraceIndex + 1;

                while (i < text.Length && depth > 0)
                {
                    if (text[i] == '{') depth++;
                    else if (text[i] == '}') depth--;
                    i++;
                }

                if (depth != 0)
                {
                    Plugin.Logger.LogWarning($"Missing closing brace for block '{header.Groups["key"].Value.Trim()}' - file may be corrupted.");
                    break;
                }

                int closeBraceIndex = i - 1;
                string body = text.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                blocks.Add((header.Groups["key"].Value, body));

                leftover.Append(text, copiedUpTo, header.Index - copiedUpTo);
                copiedUpTo = i;
                pos = i;
            }

            leftover.Append(text, copiedUpTo, text.Length - copiedUpTo);
            return (blocks, leftover.ToString());
        }
    }
}