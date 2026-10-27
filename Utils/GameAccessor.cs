using System;
using System.Collections;
using System.IO;
using System.Linq;
using PAoC_Mod.Executor;
using PAoC_Mod.Models;
using PAoC_Mod.Parser;
using PAoC_Mod.UI;
using UnityEngine;

namespace PAoC_Mod.Utils
{
    public static class GameAccessor
    {
        public static string PaocDataDirectory;

        public static DateTime GetCurrentGameDate()
        {
            if (Events.Instance == null)
                return DateTime.MinValue;

            try
            {
                if (Settings.timeScale == TimeScale.Days)
                {
                    DateTime baseDate = new DateTime(Events.Instance.startYear, Events.Instance.startMonth + 1, 1);
                    return baseDate.AddDays((double)((int)(Events.Instance.inGameTime * Constants.DAYS_PER_SECOND)));
                }
                else
                {
                    float totalMonths = (Events.Instance.monthScaleStartTime + Events.Instance.inGameTime) * Constants.MONTHS_PER_SECOND;
                    int year = Mathf.FloorToInt(totalMonths / 12f);
                    int month0 = Mathf.FloorToInt(totalMonths % 12f);
                    return new DateTime(year, month0 + 1, 1);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                Plugin.Logger.LogWarning("Computed year is out of DateTime range.");
                return DateTime.MinValue;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error computing date: {ex.Message}");
                return DateTime.MinValue;
            }
        }

        public static string GetCurrentScenarioName()
        {
            try
            {
                var manager = MySingletonPersistent<PersistentDataManager>.Instance;
                if (manager == null) return null;
                if (!manager.IsThereAnyData()) return null;

                var collection = manager.GetDataCollection();
                return collection != null ? collection.filename : null;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in GetCurrentScenarioName: {ex.Message}");
                return null;
            }
        }

        public static Nation GetNationById(int id)
        {
            try
            {
                if (id < 0 || id >= Events.allNations.Count)
                    return null;
                return Events.allNations[id];
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in GetNationById: {ex.Message}");
                return null;
            }
        }

        public static void InvokeManualAttack(Nation attacker, Nation target, bool toDeath)
        {
            try
            {
                Events.Instance.ManualAttack(attacker, target, toDeath);
                Plugin.Logger.LogInfo($"ManualAttack: {attacker.name} -> {target.name} (toDeath={toDeath})");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualAttack failed: {ex.Message}");
            }
        }

        public static void InvokePeace(Nation a, Nation b)
        {
            try
            {
                Events.Instance.ManualPeace(a, b);
                Plugin.Logger.LogInfo($"ManualPeace: {a.name} <-> {b.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualPeace failed: {ex.Message}");
            }
        }

        public static void InvokeJoinFaction(Nation a, Nation b)
        {
            try
            {
                bool result = Events.Instance.ManualAlly(a, b);
                Plugin.Logger.LogInfo($"ManualAlly: {a.name} + {b.name} -> {result}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualAlly failed: {ex.Message}");
            }
        }

        public static void InvokeLeaveFaction(Nation n)
        {
            try
            {
                Events.Instance.ManualBreakAlly(n);
                Plugin.Logger.LogInfo($"ManualBreakAlly: {n.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualBreakAlly failed: {ex.Message}");
            }
        }

        public static void InvokePuppet(Nation liege, Nation puppet)
        {
            try
            {
                bool result = Events.Instance.ManualPuppet(liege, puppet);
                Plugin.Logger.LogInfo($"ManualPuppet: {liege.name} -> {puppet.name} -> {result}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualPuppet failed: {ex.Message}");
            }
        }

        public static void InvokeRelease(Nation liege, Nation puppet)
        {
            try
            {
                bool result = Events.Instance.ManualRelease(liege, puppet);
                Plugin.Logger.LogInfo($"ManualRelease: {liege.name} -> {puppet.name} -> {result}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualRelease failed: {ex.Message}");
            }
        }

        public static void InvokeRename(Nation nation, string newName)
        {
            try
            {
                string oldName = nation.name;
                nation.name = newName;
                Plugin.Logger.LogInfo($"Renamed: {oldName} -> {newName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Rename failed: {ex.Message}");
            }
        }

        public static void InvokeRecolor(Nation nation, string hex)
        {
            try
            {
                if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
                {
                    nation.UpdateColor(color);
                    Plugin.Logger.LogInfo($"Recolored {nation.name} to #{hex}");
                }
                else
                {
                    Plugin.Logger.LogWarning($"Invalid hex color: {hex}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Recolor failed: {ex.Message}");
            }
        }

        public static void InvokeDie(Nation nation)
        {
            try
            {
                Events.Instance.ManualKill(nation);
                Plugin.Logger.LogInfo($"Killed: {nation.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualKill failed: {ex.Message}");
            }
        }

        public static void InvokeAnnex(Nation attacker, Nation target, bool overrideRightfulCityOwner)
        {
            try
            {
                Events.Instance.ManualAnnex(attacker, target, overrideRightfulCityOwner);
                Plugin.Logger.LogInfo($"ManualAnnex: {attacker.name} annexed {target.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ManualAnnex failed: {ex.Message}");
            }
        }

        public static void InvokeRevolt(Nation revoltNation, Nation fromNation)
        {
            try
            {
                if (!revoltNation.destroyed)
                {
                    Plugin.Logger.LogWarning($"Revolt failed: {revoltNation.name} is not currently destroyed, cannot revolt.");
                    return;
                }

                City originCity = null;
                try
                {
                    originCity = fromNation.cities.Find(c => c.tile.position == revoltNation.originalPos && !c.tile.isTheCapital());
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Could not check original capital position for {revoltNation.name}: {ex.Message}");
                }

                if (originCity == null)
                    originCity = fromNation.cities.Find(c => c.rightfulOwner == revoltNation && !c.tile.isTheCapital());

                if (originCity == null)
                    originCity = fromNation.cities.Find(c => !c.tile.isTheCapital());

                if (originCity == null)
                {
                    Plugin.Logger.LogWarning($"Revolt failed: {fromNation.name} has no non-capital city available for {revoltNation.name} to revolt in.");
                    return;
                }

                originCity.SetRightfulOwner(revoltNation);

                Nation resultNation = Events.Instance.CreateRevoltNation(originCity, false, true, revoltNation);
                if (resultNation == null)
                {
                    Plugin.Logger.LogWarning($"Revolt failed: CreateRevoltNation returned null for city '{originCity.name}'.");
                    return;
                }
                if (resultNation != revoltNation)
                {
                    Plugin.Logger.LogWarning($"Revolt for {revoltNation.name} unexpectedly resulted in a different nation ({resultNation.name}).");
                }

                var claimedTiles = fromNation.tiles.Where(t => t.rightfulOwner == revoltNation).ToList();
                foreach (var tile in claimedTiles)
                {
                    tile.RevoltOnTile(revoltNation);
                }

                foreach (var tile in resultNation.tiles.ToList())
                {
                    tile.UpdateSafetyAndAlertEnemies();
                }

                MySingleton<Map>.Instance.UpdateWholeMap();

                Plugin.Logger.LogInfo($"Revolt: {revoltNation.name} rose up against {fromNation.name} in {originCity.name}, claiming {claimedTiles.Count} additional tile(s).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Revolt failed: {ex.Message}");
            }
        }

        public static void InvokeRenameFaction(Nation nation, string newName)
        {
            try
            {
                if (nation.alliance == null)
                {
                    Plugin.Logger.LogWarning($"{nation.name} is not in an alliance.");
                    return;
                }
                string oldName = nation.alliance.name;
                nation.alliance.name = newName;
                Plugin.Logger.LogInfo($"Alliance renamed: {oldName} -> {newName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"RenameFaction failed: {ex.Message}");
            }
        }

        public static void InvokeRecolorFaction(Nation nation, string hex)
        {
            try
            {
                if (nation.alliance == null)
                {
                    Plugin.Logger.LogWarning($"{nation.name} is not in an alliance.");
                    return;
                }
                if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
                {
                    nation.alliance.color = color;
                    Plugin.Logger.LogInfo($"Alliance recolored to #{hex}");
                }
                else
                {
                    Plugin.Logger.LogWarning($"Invalid hex color: {hex}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"RecolorFaction failed: {ex.Message}");
            }
        }

        public static void InvokeDisbandFaction(Nation nation)
        {
            try
            {
                if (nation.alliance == null)
                {
                    Plugin.Logger.LogWarning($"{nation.name} is not in an alliance.");
                    return;
                }
                nation.alliance.Disband();
                Plugin.Logger.LogInfo($"Alliance disbanded by {nation.name}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"DisbandFaction failed: {ex.Message}");
            }
        }

        public static void InvokePower(Nation nation, string mode, int amount)
        {
            try
            {
                if (mode == "set")
                {
                    int displayValue = Mathf.Clamp(amount, 1, 6);
                    int internalCE = (6 - displayValue) * 2;
                    nation.ModifyCE(internalCE - nation.combatEfficiency);
                    Plugin.Logger.LogInfo($"Power set: display {displayValue} -> internal {nation.combatEfficiency} for {nation.name}");
                }
                else if (mode == "add")
                {
                    int internalDelta = -2 * amount;
                    nation.ModifyCE(internalDelta);
                    Plugin.Logger.LogInfo($"Power add: visual {amount} -> internal delta {internalDelta} for {nation.name}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Power command failed: {ex.Message}");
            }
        }

        public static void InvokeMoney(Nation nation, string mode, int amount)
        {
            try
            {
                if (mode == "set")
                    nation.StoredBns = amount;
                else
                    nation.StoredBns += amount;
                Plugin.Logger.LogInfo($"Money {mode} {amount} applied to {nation.name} (now {nation.StoredBns})");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Money command failed: {ex.Message}");
            }
        }

        public static void InvokeSuperEvent(string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(PaocDataDirectory))
                {
                    Plugin.Logger.LogWarning("Cannot show superevent: PAoC data directory not set.");
                    return;
                }

                string folder = Path.Combine(Path.Combine(PaocDataDirectory, "Superevents"), folderName);
                string defFile = Path.Combine(folder, "superevent.txt");
                if (!File.Exists(defFile))
                {
                    Plugin.Logger.LogWarning($"Superevent definition not found: {defFile}");
                    return;
                }

                var data = new EventDefinitionParser().ParseSuperEvent(defFile);
                string imagePath = string.IsNullOrEmpty(data.ImageFileName) ? null : Path.Combine(folder, data.ImageFileName);
                string soundFileName = string.IsNullOrEmpty(data.SoundFileName) ? "sound.wav" : data.SoundFileName;
                string soundPath = Path.Combine(folder, soundFileName);
                if (!File.Exists(soundPath))
                    soundPath = null;

                if (Plugin.Instance == null)
                {
                    Plugin.Logger.LogWarning("Plugin instance not available, showing superevent without image/sound.");
                    EventPanel.ShowSuperEvent(data, null, null);
                    return;
                }

                Plugin.Instance.StartCoroutine(LoadSuperEventAssetsAndShow(data, imagePath, soundPath));
                Plugin.Logger.LogInfo($"Superevent shown: {folderName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Superevent failed: {ex.Message}");
            }
        }

        private static IEnumerator LoadSuperEventAssetsAndShow(SuperEventData data, string imagePath, string soundPath)
        {
            yield return EventPanel.EnsureAssets();

            Sprite image = null;
            if (imagePath != null)
                yield return EventLoader.LoadSprite(imagePath, s => image = s);

            AudioClip clip = null;
            if (soundPath != null)
                yield return EventLoader.LoadAudioClip(soundPath, c => clip = c);

            if (data.WhenActivated != null)
                ActionExecutor.RunEffect(data.WhenActivated);

            EventPanel.ShowSuperEvent(data, image, clip);
        }

        public static void InvokeNews(string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(PaocDataDirectory))
                {
                    Plugin.Logger.LogWarning("Cannot show news: PAoC data directory not set.");
                    return;
                }

                string folder = Path.Combine(Path.Combine(PaocDataDirectory, "News"), folderName);
                string defFile = Path.Combine(folder, "news.txt");
                if (!File.Exists(defFile))
                {
                    Plugin.Logger.LogWarning($"News definition not found: {defFile}");
                    return;
                }

                var data = new EventDefinitionParser().ParseNews(defFile);
                string imagePath = string.IsNullOrEmpty(data.ImageFileName) ? null : Path.Combine(folder, data.ImageFileName);

                if (Plugin.Instance == null)
                {
                    Plugin.Logger.LogWarning("Plugin instance not available, showing news without image.");
                    EventPanel.ShowNews(data, null, () =>
                    {
                        if (data.Effect != null)
                            ActionExecutor.RunEffect(data.Effect);
                    });
                    return;
                }

                Plugin.Instance.StartCoroutine(LoadNewsAssetsAndShow(data, imagePath));
                Plugin.Logger.LogInfo($"News shown: {folderName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"News failed: {ex.Message}");
            }
        }

        private static IEnumerator LoadNewsAssetsAndShow(NewsData data, string imagePath)
        {
            yield return EventPanel.EnsureAssets();

            Sprite image = null;
            if (imagePath != null)
                yield return EventLoader.LoadSprite(imagePath, s => image = s);

            if (data.WhenActivated != null)
                ActionExecutor.RunEffect(data.WhenActivated);

            EventPanel.ShowNews(data, image, () =>
            {
                if (data.Effect != null)
                    ActionExecutor.RunEffect(data.Effect);
            });
        }

        public static void InvokeEvent(string folderName)
        {
            try
            {
                if (string.IsNullOrEmpty(PaocDataDirectory))
                {
                    Plugin.Logger.LogWarning("Cannot show event: PAoC data directory not set.");
                    return;
                }

                string folder = Path.Combine(Path.Combine(PaocDataDirectory, "Events"), folderName);
                string defFile = Path.Combine(folder, "event.txt");
                if (!File.Exists(defFile))
                {
                    Plugin.Logger.LogWarning($"Event definition not found: {defFile}");
                    return;
                }

                var data = new EventDefinitionParser().ParseEvent(defFile);
                string imagePath = string.IsNullOrEmpty(data.ImageFileName) ? null : Path.Combine(folder, data.ImageFileName);

                if (Plugin.Instance == null)
                {
                    Plugin.Logger.LogWarning("Plugin instance not available, showing event without image.");
                    EventPanel.ShowEvent(data, null, (optionName) =>
                    {
                        var option = data.Options.Find(o => o.Name == optionName);
                        if (option != null && option.Effect != null)
                            ActionExecutor.RunEffect(option.Effect);
                    });
                    return;
                }

                Plugin.Instance.StartCoroutine(LoadEventAssetsAndShow(data, imagePath));
                Plugin.Logger.LogInfo($"Event shown: {folderName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Event failed: {ex.Message}");
            }
        }

        private static IEnumerator LoadEventAssetsAndShow(EventData data, string imagePath)
        {
            yield return EventPanel.EnsureAssets();

            Sprite image = null;
            if (imagePath != null)
                yield return EventLoader.LoadSprite(imagePath, s => image = s);

            EventPanel.ShowEvent(data, image, (optionName) =>
            {
                var option = data.Options.Find(o => o.Name == optionName);
                if (option != null && option.Effect != null)
                    ActionExecutor.RunEffect(option.Effect);
            });
        }
    }
}