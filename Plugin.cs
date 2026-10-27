﻿using BepInEx;
using BepInEx.Logging;
using PAoC_Mod.Executor;
using PAoC_Mod.Parser;
using PAoC_Mod.UI;
using PAoC_Mod.Utils;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PAoC_Mod
{
    [BepInPlugin("com.arbuzianchelik.paoc", "Prepared Actions of Countries", "1.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static Plugin Instance;
        internal static string ModDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private ActionExecutor _executor;
        private DateTime _lastLoggedDate = DateTime.MinValue;
        private string _attemptedScenarioName;
        private Session _subscribedSession;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            Logger.LogInfo("PAoC Mod loaded!");
            TrySubscribe();
        }

        private void Update()
        {
            if (EventPanel.IsEventOpen && Session.Instance != null && !Session.Instance.gamePaused)
            {
                Session.Instance.gamePaused = true;
            }

            if (_subscribedSession == null)
                TrySubscribe();

            if (Session.Instance == null || !Session.Instance.activeStateOn)
                return;

            if (_executor == null)
            {
                TryLoadScenario();
                return;
            }

            try
            {
                DateTime currentDate = GameAccessor.GetCurrentGameDate();
                if (currentDate != DateTime.MinValue && currentDate != _lastLoggedDate)
                {
                    _lastLoggedDate = currentDate;
                    Logger.LogInfo($"Current date: {currentDate:dd/MM/yyyy}");
                }
                _executor.CheckAndExecute();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in PAoC main loop: {ex}");
            }
        }

        private void TrySubscribe()
        {
            if (Session.Instance == null) return;
            if (_subscribedSession == Session.Instance) return;

            if (_subscribedSession != null)
                _subscribedSession.activeStateStarted -= OnGameStarted;

            Session.Instance.activeStateStarted += OnGameStarted;
            _subscribedSession = Session.Instance;

            _attemptedScenarioName = null;
            _executor = null;
            _lastLoggedDate = DateTime.MinValue;
        }

        private void OnGameStarted()
        {
            try
            {
                TryLoadScenario();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled error while handling activeStateStarted: {ex}");
            }
        }

        private void TryLoadScenario()
        {
            try
            {
                if (Session.Instance == null) return;
                if (!Session.Instance.activeStateOn) return;

                string scenarioName = GameAccessor.GetCurrentScenarioName();
                if (string.IsNullOrEmpty(scenarioName))
                    return;

                if (scenarioName == _attemptedScenarioName)
                    return;

                _attemptedScenarioName = scenarioName;
                _executor = null;
                _lastLoggedDate = DateTime.MinValue;

                if (string.IsNullOrEmpty(Constants.SCENARIO_PATH))
                {
                    Logger.LogWarning("Constants.SCENARIO_PATH is empty, scenario path not ready yet.");
                    _attemptedScenarioName = null;
                    return;
                }

                string scenarioPath = Path.Combine(Constants.SCENARIO_PATH, scenarioName);

                if (!Directory.Exists(scenarioPath))
                {
                    Logger.LogWarning($"Scenario folder not found: {scenarioPath}");
                    return;
                }

                string paocSubDir = Path.Combine(scenarioPath, "PAoC");
                string paocFileInSub = Path.Combine(paocSubDir, "PAoC.txt");
                string paocFileInRoot = Path.Combine(scenarioPath, "PAoC.txt");

                bool subExists = File.Exists(paocFileInSub);
                bool rootExists = File.Exists(paocFileInRoot);

                string paocFile;
                string paocDataDir;

                if (subExists && rootExists)
                {
                    Logger.LogWarning($"Both {paocFileInSub} and {paocFileInRoot} exist - using the one in the PAoC folder. Remove the old one to avoid confusion.");
                    paocFile = paocFileInSub;
                    paocDataDir = paocSubDir;
                }
                else if (subExists)
                {
                    paocFile = paocFileInSub;
                    paocDataDir = paocSubDir;
                }
                else if (rootExists)
                {
                    paocFile = paocFileInRoot;
                    paocDataDir = scenarioPath;
                }
                else
                {
                    Logger.LogInfo($"No PAoC.txt found (checked {paocFileInSub} and {paocFileInRoot}).");
                    return;
                }

                GameAccessor.PaocDataDirectory = paocDataDir;
                ActionExecutor.ClearConditionalEvents();
                ActionExecutor.ClearKeyEvents();
                ActionExecutor.ClearSoundPlayer();
                ActionExecutor.ClearDelayEvents();
                ActionExecutor.ClearVariables();

                var parser = new PaocParser();
                var schedule = parser.Parse(paocFile);
                if (schedule == null || schedule.Count == 0)
                {
                    Logger.LogWarning("Schedule is empty or corrupted.");
                    return;
                }

                _executor = new ActionExecutor(schedule);
                Logger.LogInfo($"Loaded {schedule.Count} scheduled date(s) for scenario '{scenarioName}'.");

                string condDir = Path.Combine(paocDataDir, "Conditional_events");
                if (Directory.Exists(condDir))
                {
                    foreach (var file in Directory.GetFiles(condDir, "*.txt"))
                    {
                        try
                        {
                            var condParser = new ConditionalEventParser();
                            var evt = condParser.Parse(file);
                            ActionExecutor.RegisterConditionalEvent(evt);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to load conditional event '{file}': {ex}");
                        }
                    }
                }

                string keyDir = Path.Combine(paocDataDir, "Key_events");
                if (Directory.Exists(keyDir))
                {
                    foreach (var file in Directory.GetFiles(keyDir, "*.txt"))
                    {
                        try
                        {
                            var keyParser = new KeyEventParser();
                            var evt = keyParser.Parse(file);
                            ActionExecutor.RegisterKeyEvent(evt);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to load key event '{file}': {ex}");
                        }
                    }
                }

                string delayDir = Path.Combine(paocDataDir, "Delay_events");
                if (Directory.Exists(delayDir))
                {
                    foreach (var file in Directory.GetFiles(delayDir, "*.txt"))
                    {
                        try
                        {
                            var delayParser = new DelayEventParser();
                            var evt = delayParser.Parse(file);
                            ActionExecutor.RegisterDelayEvent(evt);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogError($"Failed to load delay event '{file}': {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading PAoC.txt: {ex}");
            }
        }

        private void OnDestroy()
        {
            if (_subscribedSession != null)
                _subscribedSession.activeStateStarted -= OnGameStarted;
            Logger.LogInfo("PAoC Mod unloaded.");
        }
    }
}