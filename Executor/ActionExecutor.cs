using PAoC_Mod.Models;
using PAoC_Mod.Parser;
using PAoC_Mod.UI;
using PAoC_Mod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace PAoC_Mod.Executor
{
    public class ActionExecutor
    {
        private static readonly HashSet<string> RequiresTarget = new HashSet<string>
        {
            "attack", "peace", "fight_to_death", "join_faction", "puppet", "release", "annex"
        };

        private static readonly HashSet<string> RequiresText = new HashSet<string>
        {
            "rename", "recolor", "rename_faction", "recolor_faction", "superevent", "news", "rename_city", "sound", "move_camera_to_country", "move_camera_to_city", "set_flag", "set_flag_to_country", "event", "key_event", "delay_event"
        };

        private static readonly HashSet<string> RequiresModeAmount = new HashSet<string>
        {
            "power", "money"
        };

        private static readonly System.Random Random = new System.Random();

        private readonly Dictionary<DateTime, List<ScheduleNode>> _schedule;
        private readonly HashSet<DateTime> _executedDates = new HashSet<DateTime>();

        private static readonly Dictionary<string, ConditionalEventData> _conditionalEvents = new Dictionary<string, ConditionalEventData>();
        private static readonly HashSet<string> _activeEvents = new HashSet<string>();

        private static readonly Dictionary<string, KeyEventData> _keyEvents = new Dictionary<string, KeyEventData>();
        private static readonly HashSet<string> _activeKeyEvents = new HashSet<string>();

        private static readonly Dictionary<string, DelayEventData> _delayEvents = new Dictionary<string, DelayEventData>();
        private static readonly Dictionary<string, DateTime> _activeDelayEvents = new Dictionary<string, DateTime>();

        private static GameObject _soundPlayer;
        private static AudioSource _soundSource;

        private static FieldInfo _followField;
        private static FieldInfo _zoomLimitField;
        private static bool _reflectionInit;

        private static readonly Dictionary<string, object> _variables = new Dictionary<string, object>();

        public static string ReplaceVariables(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var kvp in _variables)
            {
                string placeholder = "var:" + kvp.Key;
                if (text.Contains(placeholder))
                {
                    text = text.Replace(placeholder, kvp.Value?.ToString() ?? "");
                }
            }
            return text;
        }

        private static void InitReflection()
        {
            if (_reflectionInit) return;
            _reflectionInit = true;
            try
            {
                _followField = typeof(Inputs).GetField("cameraFollowPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                _zoomLimitField = typeof(Inputs).GetField("zoomLimit", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to init reflection for camera movement: {ex.Message}");
            }
        }

        private static void PanCameraToTile(Vector2Int tile, bool isCity)
        {
            try
            {
                InitReflection();
                var inputs = UnityEngine.Object.FindFirstObjectByType<Inputs>();
                if (inputs == null)
                {
                    Plugin.Logger.LogWarning("Inputs not found, cannot move camera.");
                    return;
                }

                Vector3 worldPos = Map.grid.GetWorldPosition(tile.x, tile.y, true);
                if (_followField != null)
                {
                    Vector3 follow = (Vector3)_followField.GetValue(inputs);
                    follow.x = worldPos.x;
                    follow.y = worldPos.y;
                    _followField.SetValue(inputs, follow);
                }
                else
                {
                    Plugin.Logger.LogWarning("cameraFollowPosition field not found.");
                }

                if (_zoomLimitField != null)
                {
                    Vector2 zoomLimit = (Vector2)_zoomLimitField.GetValue(inputs);
                    float targetZoom = Mathf.Lerp(zoomLimit.x, zoomLimit.y, isCity ? 0.1f : 0.28f);
                    inputs.SetZoom(targetZoom);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to pan camera: {ex.Message}");
            }
        }

        private static IEnumerator LoadFlagTexture(string path, Action<Texture2D> onLoaded)
        {
            string url = "file://" + path.Replace("\\", "/");
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            using (request)
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Logger.LogWarning($"Failed to load flag image: {request.error}");
                    onLoaded(null);
                    yield break;
                }
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                onLoaded(texture);
            }
        }

        private static void SetFlag(Nation nation, string fileName)
        {
            try
            {
                string flagsDir = Path.Combine(GameAccessor.PaocDataDirectory, "Flags");
                string fullPath = Path.Combine(flagsDir, fileName);
                if (!File.Exists(fullPath))
                {
                    Plugin.Logger.LogWarning($"Flag file not found: {fullPath}");
                    return;
                }

                if (Plugin.Instance == null)
                {
                    Plugin.Logger.LogWarning("Plugin instance not available for flag loading.");
                    return;
                }

                Plugin.Instance.StartCoroutine(LoadFlagTexture(fullPath, texture =>
                {
                    if (texture == null)
                    {
                        Plugin.Logger.LogWarning($"Failed to load flag from {fullPath}");
                        return;
                    }

                    Texture2D resized = ResizeTexture(texture, 36, 24);
                    nation.flag = resized;
                    Plugin.Logger.LogInfo($"Flag set for {nation.name} from {fileName}");
                }));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set flag for {nation.name}: {ex.Message}");
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private static void ExecuteVariable(VariableNode node)
        {
            if (string.IsNullOrEmpty(node.Name))
            {
                Plugin.Logger.LogWarning("Variable operation skipped: name is empty.");
                return;
            }
            if (string.IsNullOrEmpty(node.Value))
            {
                Plugin.Logger.LogWarning($"Variable operation skipped: value is empty for '{node.Name}'.");
                return;
            }

            if (!_variables.TryGetValue(node.Name, out object current))
            {
                if (node.Operation == VariableOperation.Set)
                {
                    if (int.TryParse(node.Value, out int intVal))
                        current = intVal;
                    else if (float.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                        current = floatVal;
                    else
                        current = node.Value;
                    _variables[node.Name] = current;
                    Plugin.Logger.LogInfo($"Variable '{node.Name}' set to '{current}'");
                    return;
                }
                else
                {
                    current = 0;
                    _variables[node.Name] = current;
                }
            }

            switch (node.Operation)
            {
                case VariableOperation.Set:
                    if (int.TryParse(node.Value, out int intVal2))
                        current = intVal2;
                    else if (float.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal2))
                        current = floatVal2;
                    else
                        current = node.Value;
                    _variables[node.Name] = current;
                    Plugin.Logger.LogInfo($"Variable '{node.Name}' set to '{current}'");
                    break;
                case VariableOperation.Add:
                case VariableOperation.Subtract:
                    if (!(current is int) && !(current is float))
                    {
                        Plugin.Logger.LogWarning($"Cannot add/subtract to non-numeric variable '{node.Name}' (current type: {current?.GetType()}).");
                        return;
                    }
                    float delta = 0;
                    if (int.TryParse(node.Value, out int addInt))
                        delta = addInt;
                    else if (float.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float addFloat))
                        delta = addFloat;
                    else
                    {
                        Plugin.Logger.LogWarning($"Cannot add/subtract non-numeric value '{node.Value}' to variable '{node.Name}'.");
                        return;
                    }
                    float currentFloat = Convert.ToSingle(current);
                    if (node.Operation == VariableOperation.Subtract)
                        delta = -delta;
                    current = currentFloat + delta;
                    _variables[node.Name] = current;
                    Plugin.Logger.LogInfo($"Variable '{node.Name}' changed to '{current}'");
                    break;
            }
        }

        public ActionExecutor(Dictionary<DateTime, List<ScheduleNode>> schedule)
        {
            _schedule = schedule;
            Plugin.Logger.LogInfo($"ActionExecutor created, schedule contains {_schedule.Count} date(s)");
        }

        public static void RegisterConditionalEvent(ConditionalEventData evt)
        {
            if (!_conditionalEvents.ContainsKey(evt.Name))
            {
                _conditionalEvents[evt.Name] = evt;
                Plugin.Logger.LogInfo($"Registered conditional event: {evt.Name}");
            }
        }

        public static void ClearConditionalEvents()
        {
            _conditionalEvents.Clear();
            _activeEvents.Clear();
            Plugin.Logger.LogInfo("Conditional events cache cleared.");
        }

        public static void RegisterKeyEvent(KeyEventData evt)
        {
            if (!_keyEvents.ContainsKey(evt.Name))
            {
                _keyEvents[evt.Name] = evt;
                Plugin.Logger.LogInfo($"Registered key event: {evt.Name} (key: {evt.Key})");
            }
        }

        public static void ClearKeyEvents()
        {
            _keyEvents.Clear();
            _activeKeyEvents.Clear();
            Plugin.Logger.LogInfo("Key events cache cleared.");
        }

        public static void RegisterDelayEvent(DelayEventData evt)
        {
            if (!_delayEvents.ContainsKey(evt.Name))
            {
                _delayEvents[evt.Name] = evt;
                Plugin.Logger.LogInfo($"Registered delay event: {evt.Name}");
            }
        }

        public static void ClearDelayEvents()
        {
            _delayEvents.Clear();
            _activeDelayEvents.Clear();
            Plugin.Logger.LogInfo("Delay events cache cleared.");
        }

        public static void ClearSoundPlayer()
        {
            if (_soundPlayer != null)
            {
                UnityEngine.Object.Destroy(_soundPlayer);
                _soundPlayer = null;
                _soundSource = null;
            }
        }

        public static void ClearVariables()
        {
            _variables.Clear();
            Plugin.Logger.LogInfo("Variables cache cleared.");
        }

        public void CheckAndExecute()
        {
            DateTime currentDate = GameAccessor.GetCurrentGameDate();
            if (currentDate == DateTime.MinValue)
                return;

            bool daysMode = Settings.timeScale == TimeScale.Days;

            foreach (var entry in _schedule)
            {
                DateTime planned = entry.Key;
                if (_executedDates.Contains(planned))
                    continue;

                bool matches = daysMode
                    ? currentDate.Date >= planned.Date
                    : (currentDate.Year > planned.Year || (currentDate.Year == planned.Year && currentDate.Month >= planned.Month));

                if (matches)
                {
                    Plugin.Logger.LogInfo($"Executing actions for {currentDate:dd/MM/yyyy} (planned {planned:dd/MM/yyyy}, mode={(daysMode ? "Days" : "Months")})");
                    ExecuteNodes(entry.Value, null);
                    _executedDates.Add(planned);
                }
            }

            CheckConditionalEvents();
            CheckKeyEvents();
            CheckDelayEvents();
        }

        public static void RunEffect(List<ScheduleNode> nodes)
        {
            ExecuteNodes(nodes, null);
        }

        private static void ExecuteNodes(List<ScheduleNode> nodes, int? contextCountryId)
        {
            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                try
                {
                    if (node is ActionNode actionNode)
                    {
                        bool isGlobalCommand = actionNode.Action.Command == "superevent" || actionNode.Action.Command == "news" || actionNode.Action.Command == "sound" || actionNode.Action.Command == "move_camera_to_country" || actionNode.Action.Command == "move_camera_to_city" || actionNode.Action.Command == "set_flag" || actionNode.Action.Command == "set_flag_to_country" || actionNode.Action.Command == "event" || actionNode.Action.Command == "key_event" || actionNode.Action.Command == "delay_event" || actionNode.Action.Command == "revolt" || actionNode.Action.Command == "set_autonomy" || actionNode.Action.Command == "change_rightful_owner" || actionNode.Action.Command == "set_loyalty";
                        if (isGlobalCommand)
                        {
                            ExecuteAction(actionNode.Action);
                            continue;
                        }

                        int effectiveId = actionNode.Action.CountryId != 0 ? actionNode.Action.CountryId : (contextCountryId ?? 0);
                        if (effectiveId == 0)
                        {
                            Plugin.Logger.LogWarning($"Action '{actionNode.Action.Command}' has no CountryId and no context available, skipping.");
                            continue;
                        }

                        if (actionNode.Action.Command == "ai" && actionNode.Action.CountryId == 0 && contextCountryId.HasValue)
                        {
                            var nation = GameAccessor.GetNationById(contextCountryId.Value);
                            if (nation != null)
                            {
                                nation.aiDisabled = actionNode.Action.Amount.Value == 0;
                                Plugin.Logger.LogInfo($"AI for {nation.name} set to {(nation.aiDisabled ? "disabled" : "enabled")}.");
                            }
                            else
                            {
                                Plugin.Logger.LogWarning($"Country not found for command 'ai'.");
                            }
                            continue;
                        }

                        var actionCopy = new ActionEntry
                        {
                            CountryId = effectiveId,
                            Command = actionNode.Action.Command,
                            TargetId = actionNode.Action.TargetId,
                            SecondaryTargetId = actionNode.Action.SecondaryTargetId,
                            TextValue = actionNode.Action.TextValue,
                            SecondaryTextValue = actionNode.Action.SecondaryTextValue,
                            Mode = actionNode.Action.Mode,
                            Amount = actionNode.Action.Amount
                        };
                        ExecuteAction(actionCopy);
                    }
                    else if (node is CountryNode countryNode)
                    {
                        int effectiveId = countryNode.CountryId != 0 ? countryNode.CountryId : (contextCountryId ?? 0);
                        if (effectiveId == 0)
                        {
                            Plugin.Logger.LogWarning("CountryNode has no CountryId and no context available, skipping.");
                            continue;
                        }
                        ExecuteNodes(countryNode.Children, effectiveId);
                    }
                    else if (node is ChanceNode chanceNode)
                    {
                        if (Random.Next(100) < chanceNode.Percent)
                            ExecuteNodes(chanceNode.Children, contextCountryId);
                    }
                    else if (node is RandomNode randomNode)
                    {
                        ExecuteRandomNode(randomNode, contextCountryId);
                    }
                    else if (node is ConditionBlockNode conditionNode)
                    {
                        int effectiveId = conditionNode.CountryId != 0 ? conditionNode.CountryId : (contextCountryId ?? 0);
                        ExecuteConditionBlock(conditionNode, effectiveId);
                    }
                    else if (node is EveryoneNode everyoneNode)
                    {
                        ExecuteEveryoneNode(everyoneNode);
                    }
                    else if (node is VariableNode variableNode)
                    {
                        ExecuteVariable(variableNode);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error executing schedule node: {ex}");
                }
            }
        }

        private static void ExecuteEveryoneNode(EveryoneNode node)
        {
            var aliveNations = Events.activeNations.ToList();
            foreach (var nation in aliveNations)
            {
                ExecuteNodes(node.Children, nation.id);
            }
        }

        private static void ExecuteRandomNode(RandomNode node, int? contextCountryId)
        {
            int total = node.Options.Sum(o => o.Weight);
            if (total <= 0)
            {
                Plugin.Logger.LogWarning("Random block has no valid weighted options.");
                return;
            }

            int roll = Random.Next(total);
            int cumulative = 0;
            foreach (var option in node.Options)
            {
                cumulative += option.Weight;
                if (roll < cumulative)
                {
                    ExecuteNodes(option.Children, contextCountryId);
                    return;
                }
            }
        }

        private static void ExecuteConditionBlock(ConditionBlockNode node, int effectiveCountryId)
        {
            Nation country = null;
            if (effectiveCountryId != 0)
                country = GameAccessor.GetNationById(effectiveCountryId);

            if (country == null && effectiveCountryId != 0)
            {
                Plugin.Logger.LogWarning($"Country {effectiveCountryId} not found for condition check.");
            }

            int total = node.Conditions.Count;
            int failed = 0;
            foreach (var condition in node.Conditions)
            {
                bool passed = EvaluateCondition(country, condition);
                Plugin.Logger.LogInfo($"[IF] country {effectiveCountryId} ({country?.name ?? "NONE"}): {DescribeCondition(condition)} -> {(passed ? "PASSED" : "FAILED")}");
                if (!passed)
                    failed++;
            }

            int trueCount = total - failed;
            Plugin.Logger.LogInfo($"[IF] country {effectiveCountryId} ({country?.name ?? "NONE"}): {trueCount}/{total} conditions true, {failed} failed");

            foreach (var branch in node.ThenBranches)
            {
                bool matches;
                switch (branch.Op)
                {
                    case ConditionOperator.All:
                        matches = total > 0 && trueCount == total;
                        break;
                    case ConditionOperator.Exact:
                        matches = trueCount == branch.Value;
                        break;
                    case ConditionOperator.GreaterOrEqual:
                        matches = trueCount >= branch.Value;
                        break;
                    case ConditionOperator.LessOrEqual:
                        matches = trueCount <= branch.Value;
                        break;
                    default:
                        matches = false;
                        break;
                }

                if (matches)
                {
                    Plugin.Logger.LogInfo($"[IF] country {effectiveCountryId}: running a 'then' block (op={branch.Op}, value={branch.Value})");
                    ExecuteNodes(branch.Children, effectiveCountryId != 0 ? effectiveCountryId : (int?)null);
                }
            }

            foreach (var branch in node.ElseBranches)
            {
                bool matches;
                switch (branch.Op)
                {
                    case ConditionOperator.All:
                        matches = total > 0 && failed == total;
                        break;
                    case ConditionOperator.Exact:
                        matches = failed == branch.Value;
                        break;
                    case ConditionOperator.GreaterOrEqual:
                        matches = failed >= branch.Value;
                        break;
                    case ConditionOperator.LessOrEqual:
                        matches = failed <= branch.Value;
                        break;
                    default:
                        matches = false;
                        break;
                }

                if (matches)
                {
                    Plugin.Logger.LogInfo($"[IF] country {effectiveCountryId}: running an 'else' block (op={branch.Op}, value={branch.Value})");
                    ExecuteNodes(branch.Children, effectiveCountryId != 0 ? effectiveCountryId : (int?)null);
                }
            }
        }

        private static string DescribeCondition(ConditionCheck condition)
        {
            if (condition.IsNegatedGroup)
                return $"not: {condition.NestedConditions.Count} nested conditions";
            if (condition.Type == "var")
                return $"var:{condition.VariableName} = \"{condition.TextValue}\"";
            if (condition.TargetId.HasValue && condition.TextValue != null)
            {
                if (condition.Type == "power_is" || condition.Type == "has_gold" || condition.Type == "own_tiles")
                    return $"{condition.Type} = {condition.TargetId}{condition.TextValue}";
                if (condition.Type == "city_rightful_to")
                    return $"{condition.Type} = \"{condition.TextValue}\" {condition.TargetId}";
                return $"{condition.Type} = {condition.TargetId} \"{condition.TextValue}\"";
            }
            if (condition.TargetId.HasValue)
                return $"{condition.Type} = {condition.TargetId}";
            if (condition.TextValue != null)
                return $"{condition.Type} = \"{condition.TextValue}\"";
            return condition.Type;
        }

        private static bool CheckAutonomy(Nation nation, string value)
        {
            if (nation == null || string.IsNullOrEmpty(value))
                return false;

            if (!nation.IsPuppet())
                return false;

            if (int.TryParse(value, out int autonomy))
                return nation.puppetRank == autonomy;

            switch (value.ToLowerInvariant())
            {
                case "vassal": return nation.IsPVassal();
                case "puppet": return nation.IsPPuppet();
                case "satellite": return nation.IsPSatellite();
                default:
                    Plugin.Logger.LogWarning($"Unknown autonomy type: '{value}'. Expected 'vassal', 'puppet', 'satellite', or a number.");
                    return false;
            }
        }

        private static bool CheckLoyalty(Nation nation, string value)
        {
            if (nation == null || string.IsNullOrEmpty(value))
                return false;

            if (!nation.IsPuppet())
                return false;

            if (int.TryParse(value, out int loyalty))
                return nation.puppetLoyalty == loyalty;

            switch (value.ToLowerInvariant())
            {
                case "low": return nation.IsLLow();
                case "medium": return nation.IsLMedium();
                case "high": return nation.IsLHigh();
                default:
                    Plugin.Logger.LogWarning($"Unknown loyalty type: '{value}'. Expected 'low', 'medium', 'high', or a number.");
                    return false;
            }
        }

        public static bool EvaluateCondition(Nation country, ConditionCheck condition)
        {
            if (condition.IsNegatedGroup)
            {
                if (condition.NestedConditions == null || condition.NestedConditions.Count == 0)
                    return false;

                int total = condition.NestedConditions.Count;
                int falseCount = 0;
                foreach (var nested in condition.NestedConditions)
                {
                    if (!EvaluateCondition(country, nested))
                        falseCount++;
                }

                switch (condition.NotOperator)
                {
                    case ConditionOperator.All:
                        return falseCount == total;
                    case ConditionOperator.Exact:
                        return falseCount == condition.NotValue;
                    case ConditionOperator.GreaterOrEqual:
                        return falseCount >= condition.NotValue;
                    case ConditionOperator.LessOrEqual:
                        return falseCount <= condition.NotValue;
                    default:
                        return false;
                }
            }

            if (condition.Type == "var")
            {
                if (string.IsNullOrEmpty(condition.VariableName))
                    return false;
                if (!_variables.TryGetValue(condition.VariableName, out object varValue))
                    return false;
                string expected = condition.TextValue;
                string varOp = condition.VariableOperator ?? "";
                if (varOp == "" && expected.StartsWith("\"") && expected.EndsWith("\""))
                    expected = expected.Trim('"');
                float floatExpected = 0;
                int intExpected = 0;
                bool isNumeric = false;
                if (int.TryParse(expected, out intExpected))
                    isNumeric = true;
                else if (float.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out floatExpected))
                    isNumeric = true;
                if (isNumeric)
                {
                    float currentFloat = Convert.ToSingle(varValue);
                    float targetFloat = floatExpected != 0 ? floatExpected : intExpected;
                    if (varOp == ">") return currentFloat >= targetFloat;
                    if (varOp == "<") return currentFloat <= targetFloat;
                    return currentFloat == targetFloat;
                }
                else
                {
                    return varValue?.ToString() == expected;
                }
            }

            if (condition.Type == "autonomy_is")
            {
                return CheckAutonomy(country, condition.TextValue);
            }

            if (condition.Type == "country_autonomy_is")
            {
                if (!condition.TargetId.HasValue) return false;
                var target = GameAccessor.GetNationById(condition.TargetId.Value);
                return CheckAutonomy(target, condition.TextValue);
            }

            if (condition.Type == "loyalty_is")
            {
                return CheckLoyalty(country, condition.TextValue);
            }

            if (condition.Type == "country_loyalty_is")
            {
                if (!condition.TargetId.HasValue) return false;
                var target = GameAccessor.GetNationById(condition.TargetId.Value);
                return CheckLoyalty(target, condition.TextValue);
            }

            if (condition.Type == "country_name_is")
            {
                if (!condition.TargetId.HasValue || string.IsNullOrEmpty(condition.TextValue))
                    return false;
                var target = GameAccessor.GetNationById(condition.TargetId.Value);
                return target != null && target.name == condition.TextValue;
            }

            if (country == null)
            {
                switch (condition.Type)
                {
                    case "country_exist":
                        if (condition.TargetId.HasValue)
                        {
                            var target = GameAccessor.GetNationById(condition.TargetId.Value);
                            return target != null && !target.destroyed;
                        }
                        return false;
                    case "destroyed":
                        if (condition.TargetId.HasValue)
                        {
                            var target = GameAccessor.GetNationById(condition.TargetId.Value);
                            return target != null && target.destroyed;
                        }
                        return false;
                    default:
                        return false;
                }
            }

            switch (condition.Type)
            {
                case "in_war":
                    return country.IsAtWar();
                case "in_war_with":
                    {
                        var other = condition.TargetId.HasValue ? GameAccessor.GetNationById(condition.TargetId.Value) : null;
                        return other != null && country.IsWarEnemyOf(other);
                    }
                case "name_is":
                    return country.name == condition.TextValue;
                case "is_puppet":
                    return country.IsPuppet();
                case "is_puppet_of":
                    return country.IsPuppet() && condition.TargetId.HasValue && country.liegeId == condition.TargetId.Value;
                case "is_in_faction":
                    return country.IsAllied();
                case "is_in_faction_with":
                    {
                        var other = condition.TargetId.HasValue ? GameAccessor.GetNationById(condition.TargetId.Value) : null;
                        return other != null && country.IsAlliedTo(other);
                    }
                case "controls_city":
                    return condition.TextValue != null && country.cities.Any(c => c.name == condition.TextValue);
                case "country_controls_city":
                    if (!condition.TargetId.HasValue || string.IsNullOrEmpty(condition.TextValue))
                        return false;
                    var otherCountry = GameAccessor.GetNationById(condition.TargetId.Value);
                    if (otherCountry == null)
                        return false;
                    return otherCountry.cities.Any(c => c.name == condition.TextValue);
                case "power_is":
                    {
                        if (!condition.TargetId.HasValue)
                            return false;
                        int value = condition.TargetId.Value;
                        value = Math.Max(1, Math.Min(6, value));
                        int currentCE = country.combatEfficiency;
                        string op = condition.TextValue ?? "";
                        return CompareValues(currentCE, value, op);
                    }
                case "has_gold":
                    {
                        if (!condition.TargetId.HasValue)
                            return false;
                        int value = condition.TargetId.Value;
                        int currentGold = country.StoredBns;
                        string op = condition.TextValue ?? "";
                        return CompareValues(currentGold, value, op);
                    }
                case "color_is":
                    {
                        if (string.IsNullOrEmpty(condition.TextValue))
                            return false;
                        string hex = condition.TextValue.TrimStart('#');
                        if (ColorUtility.TryParseHtmlString("#" + hex, out Color targetColor))
                        {
                            Color currentColor = country.color;
                            float tolerance = 0.01f;
                            return Mathf.Abs(currentColor.r - targetColor.r) < tolerance &&
                                   Mathf.Abs(currentColor.g - targetColor.g) < tolerance &&
                                   Mathf.Abs(currentColor.b - targetColor.b) < tolerance;
                        }
                        return false;
                    }
                case "country_exist":
                    {
                        if (condition.TargetId.HasValue)
                        {
                            var target = GameAccessor.GetNationById(condition.TargetId.Value);
                            return target != null && !target.destroyed;
                        }
                        else
                        {
                            return country != null && !country.destroyed;
                        }
                    }
                case "destroyed":
                    {
                        if (condition.TargetId.HasValue)
                        {
                            var target = GameAccessor.GetNationById(condition.TargetId.Value);
                            return target != null && target.destroyed;
                        }
                        else
                        {
                            return country.destroyed;
                        }
                    }
                case "destroyed_by":
                    if (!condition.TargetId.HasValue) return false;
                    return country.killerId == condition.TargetId.Value;
                case "own_tiles":
                    {
                        if (!condition.TargetId.HasValue) return false;
                        int targetTiles = condition.TargetId.Value;
                        int currentTiles = country.area;
                        string op = condition.TextValue ?? "";
                        return CompareValues(currentTiles, targetTiles, op);
                    }
                case "city_rightful_to":
                    {
                        if (string.IsNullOrEmpty(condition.TextValue) || !condition.TargetId.HasValue) return false;
                        var targetCountry = GameAccessor.GetNationById(condition.TargetId.Value);
                        if (targetCountry == null) return false;
                        var city = Events.allCities.Find(c => c.name == condition.TextValue);
                        if (city == null) return false;
                        return city.rightfulOwner == targetCountry;
                    }
                case "revolted":
                    {
                        if (condition.TargetId.HasValue)
                        {
                            var target = GameAccessor.GetNationById(condition.TargetId.Value);
                            return target != null && target.originId != 0 && target.originId != target.id;
                        }
                        else
                        {
                            return country.originId != 0 && country.originId != country.id;
                        }
                    }
                case "revolted_from":
                    if (!condition.TargetId.HasValue) return false;
                    return country.originId == condition.TargetId.Value;
                case "revolt_able":
                    {
                        if (string.IsNullOrEmpty(condition.TextValue)) return false;
                        var city = Events.allCities.Find(c => c.name == condition.TextValue);
                        if (city == null) return false;
                        return city.rightfulOwner == null || city.rightfulOwner != city.owner;
                    }
                default:
                    Plugin.Logger.LogWarning($"Unknown condition type: {condition.Type}");
                    return false;
            }
        }

        private static bool CompareValues(int current, int target, string op)
        {
            switch (op)
            {
                case ">": return current >= target;
                case "<": return current <= target;
                default: return current == target;
            }
        }

        private static IEnumerator PlaySoundAsync(string soundPath, bool stopOthers)
        {
            if (stopOthers)
            {
                EventPanel.StopSuperEventSound();
                if (_soundSource != null && _soundSource.isPlaying)
                    _soundSource.Stop();
            }

            AudioClip clip = null;
            yield return EventLoader.LoadAudioClip(soundPath, c => clip = c);
            if (clip == null)
            {
                Plugin.Logger.LogWarning($"Failed to load sound: {soundPath}");
                yield break;
            }

            if (_soundPlayer == null)
            {
                _soundPlayer = new GameObject("PAoC_SoundPlayer");
                _soundSource = _soundPlayer.AddComponent<AudioSource>();
            }

            if (_soundSource.isPlaying)
                _soundSource.Stop();

            _soundSource.clip = clip;
            _soundSource.Play();
            Plugin.Logger.LogInfo($"Playing sound: {soundPath}");
        }

        private static void MoveCameraToCountry(string name)
        {
            try
            {
                var alive = Events.activeNations.Where(n => n != null && !n.destroyed && n.name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (alive.Count == 0)
                {
                    Plugin.Logger.LogWarning($"Country '{name}' not found for camera move.");
                    return;
                }
                var target = alive[Random.Next(alive.Count)];
                PanCameraToTile(target.pos, false);
                Plugin.Logger.LogInfo($"Camera moved to country: {target.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to move camera to country '{name}': {ex.Message}");
            }
        }

        private static void MoveCameraToCity(string name)
        {
            try
            {
                var cities = Events.allCities.Where(c => c != null && c.name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (cities.Count == 0)
                {
                    Plugin.Logger.LogWarning($"City '{name}' not found for camera move.");
                    return;
                }
                var target = cities[Random.Next(cities.Count)];
                PanCameraToTile(target.tile.position, true);
                Plugin.Logger.LogInfo($"Camera moved to city: {target.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to move camera to city '{name}': {ex.Message}");
            }
        }

        private static void ExecuteAction(ActionEntry action)
        {
            if (action.Command == "revolt")
            {
                if (!action.TargetId.HasValue || !action.SecondaryTargetId.HasValue)
                {
                    Plugin.Logger.LogWarning("Command 'revolt' requires two country IDs: revolt = REVOLTING_ID FROM_ID.");
                    return;
                }

                var revoltNation = GameAccessor.GetNationById(action.TargetId.Value);
                var fromNation = GameAccessor.GetNationById(action.SecondaryTargetId.Value);

                if (revoltNation == null || fromNation == null)
                {
                    Plugin.Logger.LogWarning($"Revolt failed: country {action.TargetId} or {action.SecondaryTargetId} not found.");
                    return;
                }

                GameAccessor.InvokeRevolt(revoltNation, fromNation);
                return;
            }

            if (action.Command == "change_rightful_owner")
            {
                if (!action.TargetId.HasValue || string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'change_rightful_owner' requires a country ID and a city name.");
                    return;
                }

                var newOwner = GameAccessor.GetNationById(action.TargetId.Value);
                if (newOwner == null)
                {
                    Plugin.Logger.LogWarning($"Country {action.TargetId.Value} not found for change_rightful_owner.");
                    return;
                }

                var matching = Events.allCities.FindAll(c => c.name == action.TextValue);
                if (matching.Count == 0)
                {
                    Plugin.Logger.LogWarning($"No city found with name '{action.TextValue}' for change_rightful_owner.");
                    return;
                }
                if (matching.Count > 1)
                {
                    Plugin.Logger.LogWarning($"Multiple cities ({matching.Count}) found with name '{action.TextValue}', cannot change rightful owner.");
                    return;
                }

                matching[0].SetRightfulOwner(newOwner);
                Plugin.Logger.LogInfo($"Rightful owner of '{action.TextValue}' changed to {newOwner.name}.");
                return;
            }

            if (action.Command == "superevent")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'superevent' requires a folder name.");
                    return;
                }
                GameAccessor.InvokeSuperEvent(action.TextValue);
                return;
            }

            if (action.Command == "news")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'news' requires a folder name.");
                    return;
                }
                GameAccessor.InvokeNews(action.TextValue);
                return;
            }

            if (action.Command == "event")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'event' requires a folder name.");
                    return;
                }
                GameAccessor.InvokeEvent(action.TextValue);
                return;
            }

            if (action.Command == "conditional_event")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'conditional_event' requires an event name.");
                    return;
                }
                string eventName = action.TextValue;
                string mode = action.SecondaryTextValue?.ToLower() ?? "on";
                bool enable;
                if (mode == "on" || mode == "enable" || mode == "1")
                    enable = true;
                else if (mode == "off" || mode == "disable" || mode == "0")
                    enable = false;
                else
                {
                    Plugin.Logger.LogWarning($"Unknown mode '{mode}' for conditional_event. Use on/off/enable/disable/1/0.");
                    return;
                }

                if (!_conditionalEvents.ContainsKey(eventName))
                {
                    string eventPath = Path.Combine(GameAccessor.PaocDataDirectory, "Conditional_events", eventName + ".txt");
                    if (!File.Exists(eventPath))
                    {
                        Plugin.Logger.LogWarning($"Conditional event file not found: {eventPath}");
                        return;
                    }
                    var parser = new ConditionalEventParser();
                    var evt = parser.Parse(eventPath);
                    _conditionalEvents[eventName] = evt;
                }

                if (enable)
                {
                    _activeEvents.Add(eventName);
                    Plugin.Logger.LogInfo($"Conditional event '{eventName}' enabled.");
                }
                else
                {
                    _activeEvents.Remove(eventName);
                    Plugin.Logger.LogInfo($"Conditional event '{eventName}' disabled.");
                }
                return;
            }

            if (action.Command == "key_event")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'key_event' requires an event name.");
                    return;
                }
                string eventName = action.TextValue;
                string mode = action.SecondaryTextValue?.ToLower() ?? "on";
                bool enable;
                if (mode == "on" || mode == "enable" || mode == "1")
                    enable = true;
                else if (mode == "off" || mode == "disable" || mode == "0")
                    enable = false;
                else
                {
                    Plugin.Logger.LogWarning($"Unknown mode '{mode}' for key_event. Use on/off/enable/disable/1/0.");
                    return;
                }

                if (!_keyEvents.ContainsKey(eventName))
                {
                    string eventPath = Path.Combine(GameAccessor.PaocDataDirectory, "Key_events", eventName + ".txt");
                    if (!File.Exists(eventPath))
                    {
                        Plugin.Logger.LogWarning($"Key event file not found: {eventPath}");
                        return;
                    }
                    var parser = new KeyEventParser();
                    var evt = parser.Parse(eventPath);
                    _keyEvents[eventName] = evt;
                }

                if (enable)
                {
                    _activeKeyEvents.Add(eventName);
                    Plugin.Logger.LogInfo($"Key event '{eventName}' enabled.");
                }
                else
                {
                    _activeKeyEvents.Remove(eventName);
                    Plugin.Logger.LogInfo($"Key event '{eventName}' disabled.");
                }
                return;
            }

            if (action.Command == "delay_event")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'delay_event' requires an event name.");
                    return;
                }
                string eventName = action.TextValue;
                string mode = action.SecondaryTextValue?.ToLower() ?? "on";
                bool enable;
                if (mode == "on" || mode == "enable" || mode == "1")
                    enable = true;
                else if (mode == "off" || mode == "disable" || mode == "0")
                    enable = false;
                else
                {
                    Plugin.Logger.LogWarning($"Unknown mode '{mode}' for delay_event. Use on/off/enable/disable/1/0.");
                    return;
                }

                if (!_delayEvents.ContainsKey(eventName))
                {
                    string eventPath = Path.Combine(GameAccessor.PaocDataDirectory, "Delay_events", eventName + ".txt");
                    if (!File.Exists(eventPath))
                    {
                        Plugin.Logger.LogWarning($"Delay event file not found: {eventPath}");
                        return;
                    }
                    var parser = new DelayEventParser();
                    var evt = parser.Parse(eventPath);
                    _delayEvents[eventName] = evt;
                }

                if (enable)
                {
                    DateTime currentDate = GameAccessor.GetCurrentGameDate();
                    if (currentDate == DateTime.MinValue)
                    {
                        Plugin.Logger.LogWarning("Cannot enable delay event: unable to get current game date.");
                        return;
                    }

                    var evt = _delayEvents[eventName];

                    int randomDays = 0;
                    int randomMonths = 0;
                    int randomYears = 0;

                    if (evt.RandomMinDays > 0 || evt.RandomMaxDays > 0 ||
                        evt.RandomMinMonths > 0 || evt.RandomMaxMonths > 0 ||
                        evt.RandomMinYears > 0 || evt.RandomMaxYears > 0)
                    {
                        if (evt.RandomMinDays > 0 || evt.RandomMaxDays > 0)
                        {
                            int min = evt.RandomMinDays;
                            int max = evt.RandomMaxDays;
                            if (min == 0 && max > 0) min = 0;
                            if (min > max) { int temp = min; min = max; max = temp; }
                            randomDays = Random.Next(max - min + 1) + min;
                        }
                        if (evt.RandomMinMonths > 0 || evt.RandomMaxMonths > 0)
                        {
                            int min = evt.RandomMinMonths;
                            int max = evt.RandomMaxMonths;
                            if (min == 0 && max > 0) min = 0;
                            if (min > max) { int temp = min; min = max; max = temp; }
                            randomMonths = Random.Next(max - min + 1) + min;
                        }
                        if (evt.RandomMinYears > 0 || evt.RandomMaxYears > 0)
                        {
                            int min = evt.RandomMinYears;
                            int max = evt.RandomMaxYears;
                            if (min == 0 && max > 0) min = 0;
                            if (min > max) { int temp = min; min = max; max = temp; }
                            randomYears = Random.Next(max - min + 1) + min;
                        }
                    }

                    DateTime activationDate = currentDate.AddDays(evt.Days + randomDays)
                                                         .AddMonths(evt.Months + randomMonths)
                                                         .AddYears(evt.Years + randomYears);

                    _activeDelayEvents[eventName] = activationDate;
                    Plugin.Logger.LogInfo($"Delay event '{eventName}' enabled, will trigger at {activationDate:dd.MM.yyyy} (delay: {evt.Days}d {evt.Months}m {evt.Years}y, random: {randomDays}d {randomMonths}m {randomYears}y)");
                }
                else
                {
                    _activeDelayEvents.Remove(eventName);
                    Plugin.Logger.LogInfo($"Delay event '{eventName}' disabled.");
                }
                return;
            }

            if (action.Command == "sound")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'sound' requires a file name.");
                    return;
                }
                string soundPath = Path.Combine(GameAccessor.PaocDataDirectory, "Sounds", action.TextValue);
                if (!File.Exists(soundPath))
                {
                    Plugin.Logger.LogWarning($"Sound file not found: {soundPath}");
                    return;
                }

                bool stopOthers = false;
                string mode = action.SecondaryTextValue?.ToLower() ?? "off";
                if (mode == "on" || mode == "enable" || mode == "1" || mode == "yes")
                    stopOthers = true;
                else if (mode == "off" || mode == "disable" || mode == "0" || mode == "no")
                    stopOthers = false;
                else
                {
                    Plugin.Logger.LogWarning($"Unknown mode '{mode}' for sound, defaulting to 'off'.");
                    stopOthers = false;
                }

                if (Plugin.Instance != null)
                    Plugin.Instance.StartCoroutine(PlaySoundAsync(soundPath, stopOthers));
                else
                    Plugin.Logger.LogWarning("Plugin instance not available for sound playback.");
                return;
            }

            if (action.Command == "move_camera_to_country")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'move_camera_to_country' requires a country name.");
                    return;
                }
                MoveCameraToCountry(action.TextValue);
                return;
            }

            if (action.Command == "move_camera_to_city")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'move_camera_to_city' requires a city name.");
                    return;
                }
                MoveCameraToCity(action.TextValue);
                return;
            }

            if (action.Command == "set_flag")
            {
                if (string.IsNullOrEmpty(action.TextValue))
                {
                    Plugin.Logger.LogWarning("Command 'set_flag' requires a file name.");
                    return;
                }
                if (action.CountryId == 0)
                {
                    Plugin.Logger.LogWarning("Command 'set_flag' used without country context.");
                    return;
                }
                var nation = GameAccessor.GetNationById(action.CountryId);
                if (nation == null)
                {
                    Plugin.Logger.LogWarning($"Country {action.CountryId} not found for set_flag.");
                    return;
                }
                SetFlag(nation, action.TextValue);
                return;
            }

            if (action.Command == "set_flag_to_country")
            {
                if (string.IsNullOrEmpty(action.TextValue) || !action.TargetId.HasValue)
                {
                    Plugin.Logger.LogWarning("Command 'set_flag_to_country' requires a file name and a country ID.");
                    return;
                }
                var nation = GameAccessor.GetNationById(action.TargetId.Value);
                if (nation == null)
                {
                    Plugin.Logger.LogWarning($"Country {action.TargetId.Value} not found for set_flag_to_country.");
                    return;
                }
                SetFlag(nation, action.TextValue);
                return;
            }

            var country = GameAccessor.GetNationById(action.CountryId);
            if (country == null)
            {
                Plugin.Logger.LogWarning($"Country not found for command '{action.Command}'.");
                return;
            }

            if (RequiresTarget.Contains(action.Command) && !action.TargetId.HasValue)
            {
                Plugin.Logger.LogWarning($"Command '{action.Command}' requires a target ID.");
                return;
            }

            if (RequiresText.Contains(action.Command) && string.IsNullOrEmpty(action.TextValue))
            {
                Plugin.Logger.LogWarning($"Command '{action.Command}' requires a text value.");
                return;
            }

            if (RequiresModeAmount.Contains(action.Command) && (action.Mode == null || !action.Amount.HasValue))
            {
                Plugin.Logger.LogWarning($"Command '{action.Command}' requires 'add' or 'set' followed by a number.");
                return;
            }

            Nation target = null;
            if (action.TargetId.HasValue)
            {
                target = GameAccessor.GetNationById(action.TargetId.Value);
                if (target == null)
                {
                    Plugin.Logger.LogWarning($"Target country {action.TargetId.Value} not found.");
                    return;
                }
            }

            switch (action.Command)
            {
                case "attack":
                    GameAccessor.InvokeManualAttack(country, target, false);
                    break;
                case "fight_to_death":
                    GameAccessor.InvokeManualAttack(country, target, true);
                    break;
                case "peace":
                    GameAccessor.InvokePeace(country, target);
                    break;
                case "join_faction":
                    GameAccessor.InvokeJoinFaction(country, target);
                    break;
                case "leave_faction":
                    GameAccessor.InvokeLeaveFaction(country);
                    break;
                case "puppet":
                    GameAccessor.InvokePuppet(country, target);
                    break;
                case "release":
                    GameAccessor.InvokeRelease(country, target);
                    break;
                case "rename":
                    GameAccessor.InvokeRename(country, action.TextValue);
                    break;
                case "recolor":
                    GameAccessor.InvokeRecolor(country, action.TextValue);
                    break;
                case "die":
                    GameAccessor.InvokeDie(country);
                    break;
                case "annex":
                    GameAccessor.InvokeAnnex(country, target, false);
                    break;
                case "rename_faction":
                    GameAccessor.InvokeRenameFaction(country, action.TextValue);
                    break;
                case "recolor_faction":
                    GameAccessor.InvokeRecolorFaction(country, action.TextValue);
                    break;
                case "disband_faction":
                    GameAccessor.InvokeDisbandFaction(country);
                    break;
                case "power":
                    if (action.Mode == "set")
                    {
                        int displayValue = Mathf.Clamp(action.Amount.Value, 1, 6);
                        int internalCE = (6 - displayValue) * 2;
                        country.ModifyCE(internalCE - country.combatEfficiency);
                        Plugin.Logger.LogInfo($"Power set: display {displayValue} -> internal {country.combatEfficiency} for {country.name}");
                    }
                    else if (action.Mode == "add")
                    {
                        int internalDelta = -2 * action.Amount.Value;
                        country.ModifyCE(internalDelta);
                        Plugin.Logger.LogInfo($"Power add: visual {action.Amount.Value} -> internal delta {internalDelta} for {country.name}");
                    }
                    break;
                case "money":
                    GameAccessor.InvokeMoney(country, action.Mode, action.Amount.Value);
                    break;
                case "power_lock":
                    if (!action.Amount.HasValue)
                    {
                        Plugin.Logger.LogWarning("Command 'power_lock' requires a value (0 or 1).");
                        return;
                    }
                    country.ceLock = action.Amount.Value != 0;
                    Plugin.Logger.LogInfo($"Power lock set to {country.ceLock} for {country.name}");
                    break;
                case "set_autonomy":
                    if (!action.Amount.HasValue)
                    {
                        Plugin.Logger.LogWarning("Command 'set_autonomy' requires a value.");
                        return;
                    }
                    country.Autonomy = action.Amount.Value;
                    Plugin.Logger.LogInfo($"Autonomy for {country.name} set to {country.puppetRank}.");
                    break;
                case "set_loyalty":
                    if (!action.Amount.HasValue)
                    {
                        Plugin.Logger.LogWarning("Command 'set_loyalty' requires a value.");
                        return;
                    }
                    country.Loyalty = action.Amount.Value;
                    Plugin.Logger.LogInfo($"Loyalty for {country.name} set to {country.puppetLoyalty}.");
                    break;
                case "rename_city":
                    if (string.IsNullOrEmpty(action.TextValue) || string.IsNullOrEmpty(action.SecondaryTextValue))
                    {
                        Plugin.Logger.LogWarning("Command 'rename_city' requires two text values: old name and new name.");
                        return;
                    }
                    var cities = Events.allCities.FindAll(c => c.name == action.TextValue);
                    if (cities.Count == 0)
                    {
                        Plugin.Logger.LogWarning($"No cities found with name '{action.TextValue}'.");
                        return;
                    }
                    if (cities.Count > 1)
                        Plugin.Logger.LogWarning($"Multiple cities found with name '{action.TextValue}', renaming all.");
                    foreach (var c in cities)
                    {
                        c.name = action.SecondaryTextValue;
                        c.clogic?.UpdateGraphic();
                    }
                    Plugin.Logger.LogInfo($"Renamed {cities.Count} city(ies) from '{action.TextValue}' to '{action.SecondaryTextValue}'.");
                    break;
                case "ai":
                    if (!action.Amount.HasValue)
                    {
                        Plugin.Logger.LogWarning("Command 'ai' requires a value (0 or 1).");
                        return;
                    }
                    country.aiDisabled = action.Amount.Value == 0;
                    Plugin.Logger.LogInfo($"AI for {country.name} set to {(country.aiDisabled ? "disabled" : "enabled")}.");
                    break;
                default:
                    Plugin.Logger.LogWarning($"Unknown command: {action.Command}");
                    break;
            }
        }

        private void CheckConditionalEvents()
        {
            if (_activeEvents.Count == 0) return;

            foreach (var name in _activeEvents.ToList())
            {
                if (!_conditionalEvents.TryGetValue(name, out var evt)) continue;

                if (evt.CountryConditions.Count > 0)
                {
                    bool allMatch = true;
                    foreach (var kvp in evt.CountryConditions)
                    {
                        int countryId = kvp.Key;
                        var localConditions = kvp.Value;
                        var nation = GameAccessor.GetNationById(countryId);
                        if (nation == null || nation.destroyed)
                        {
                            allMatch = false;
                            break;
                        }

                        int trueCount = 0;
                        int totalConditions = localConditions.Count + evt.Conditions.Count;
                        if (totalConditions == 0)
                        {
                            allMatch = false;
                            break;
                        }

                        foreach (var cond in localConditions)
                        {
                            if (EvaluateCondition(nation, cond))
                                trueCount++;
                        }
                        foreach (var cond in evt.Conditions)
                        {
                            if (EvaluateCondition(nation, cond))
                                trueCount++;
                        }

                        bool countryMatches = (trueCount == totalConditions);
                        if (!countryMatches)
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                    {
                        Plugin.Logger.LogInfo($"Conditional event '{name}' triggered (all country blocks matched).");
                        ExecuteNodes(evt.Effect, 0);
                        _activeEvents.Remove(name);
                    }
                    continue;
                }

                if (evt.CountryId.HasValue)
                {
                    var nation = GameAccessor.GetNationById(evt.CountryId.Value);
                    if (nation == null || nation.destroyed)
                    {
                        Plugin.Logger.LogInfo($"Conditional event '{name}' skipped: country {evt.CountryId.Value} does not exist or destroyed.");
                        continue;
                    }

                    int trueCount = 0;
                    foreach (var condition in evt.Conditions)
                    {
                        if (EvaluateCondition(nation, condition))
                            trueCount++;
                    }

                    bool matches = false;
                    switch (evt.Op)
                    {
                        case ConditionOperator.All:
                            matches = evt.Conditions.Count > 0 && trueCount == evt.Conditions.Count;
                            break;
                        case ConditionOperator.Exact:
                            matches = trueCount == evt.Value;
                            break;
                        case ConditionOperator.GreaterOrEqual:
                            matches = trueCount >= evt.Value;
                            break;
                        case ConditionOperator.LessOrEqual:
                            matches = trueCount <= evt.Value;
                            break;
                    }

                    if (matches)
                    {
                        Plugin.Logger.LogInfo($"Conditional event '{name}' triggered for {nation.name} ({trueCount}/{evt.Conditions.Count} conditions true).");
                        ExecuteNodes(evt.Effect, nation.id);
                        _activeEvents.Remove(name);
                    }
                    continue;
                }

                if (evt.Conditions.Count == 0) continue;

                foreach (var nation in Events.activeNations)
                {
                    int trueCount = 0;
                    foreach (var condition in evt.Conditions)
                    {
                        if (EvaluateCondition(nation, condition))
                            trueCount++;
                    }

                    bool matches = false;
                    switch (evt.Op)
                    {
                        case ConditionOperator.All:
                            matches = evt.Conditions.Count > 0 && trueCount == evt.Conditions.Count;
                            break;
                        case ConditionOperator.Exact:
                            matches = trueCount == evt.Value;
                            break;
                        case ConditionOperator.GreaterOrEqual:
                            matches = trueCount >= evt.Value;
                            break;
                        case ConditionOperator.LessOrEqual:
                            matches = trueCount <= evt.Value;
                            break;
                    }

                    if (matches)
                    {
                        Plugin.Logger.LogInfo($"Conditional event '{name}' triggered for {nation.name} ({trueCount}/{evt.Conditions.Count} conditions true).");
                        ExecuteNodes(evt.Effect, nation.id);
                        _activeEvents.Remove(name);
                        break;
                    }
                }
            }
        }

        private void CheckKeyEvents()
        {
            if (_activeKeyEvents.Count == 0) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            foreach (var name in _activeKeyEvents.ToList())
            {
                if (!_keyEvents.TryGetValue(name, out var evt)) continue;
                if (string.IsNullOrEmpty(evt.Key)) continue;
                if (Enum.TryParse<UnityEngine.InputSystem.Key>(evt.Key, true, out var key))
                {
                    if (keyboard[key].wasPressedThisFrame)
                    {
                        Plugin.Logger.LogInfo($"Key event '{name}' triggered by key '{evt.Key}'.");
                        ExecuteNodes(evt.Effect, 0);
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"Invalid key '{evt.Key}' in key event '{name}'.");
                }
            }
        }

        private void CheckDelayEvents()
        {
            if (_activeDelayEvents.Count == 0) return;
            DateTime currentDate = GameAccessor.GetCurrentGameDate();
            if (currentDate == DateTime.MinValue) return;

            var keys = _activeDelayEvents.Keys.ToList();
            foreach (var key in keys)
            {
                if (!_activeDelayEvents.TryGetValue(key, out DateTime activationTime))
                    continue;

                if (currentDate >= activationTime)
                {
                    if (_delayEvents.TryGetValue(key, out var evt))
                    {
                        Plugin.Logger.LogInfo($"Delay event '{key}' triggered at {currentDate:dd.MM.yyyy}.");
                        _activeDelayEvents.Remove(key);
                        ExecuteNodes(evt.Effect, 0);
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Delay event '{key}' has no definition, removing.");
                        _activeDelayEvents.Remove(key);
                    }
                }
            }
        }
    }
}