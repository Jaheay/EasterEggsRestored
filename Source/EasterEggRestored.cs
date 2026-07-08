using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EasterEggRestored
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class EasterEggRestoredBehaviour : MonoBehaviour
    {
        private const string LogPrefix = "[EasterEggRestored] ";
        private const string SettingsPath = "GameData/EasterEggRestored/PluginData/EasterEggRestored.cfg";

        private readonly List<StaticRestore> restores = new List<StaticRestore>();
        private float nextAttemptTime;
        private int attemptCount;
        private bool loaded;

        public void Start()
        {
            DontDestroyOnLoad(this);
            LoadSettings();
            GameEvents.onLevelWasLoadedGUIReady.Add(OnLevelReady);
            nextAttemptTime = 0f;
        }

        public void OnDestroy()
        {
            GameEvents.onLevelWasLoadedGUIReady.Remove(OnLevelReady);
        }

        public void Update()
        {
            if (!loaded)
                return;

            if (Time.realtimeSinceStartup < nextAttemptTime)
                return;

            nextAttemptTime = Time.realtimeSinceStartup + 1.0f;
            TryApplyAll();
        }

        private void OnLevelReady(GameScenes scene)
        {
            nextAttemptTime = 0f;
        }

        private void LoadSettings()
        {
            restores.Clear();

            string path = Path.Combine(KSPUtil.ApplicationRootPath, SettingsPath.Replace('/', Path.DirectorySeparatorChar));
            ConfigNode root = null;

            if (File.Exists(path))
            {
                try
                {
                    root = ConfigNode.Load(path);
                }
                catch (Exception ex)
                {
                    Debug.LogError(LogPrefix + "Could not load settings file: " + path + " :: " + ex);
                }
            }
            else
            {
                Debug.LogWarning(LogPrefix + "Settings file not found, using built-in Vall/Icehenge default: " + path);
            }

            if (root != null)
            {
                ConfigNode config = root.GetNode("EASTER_EGG_RESTORED") ?? root;
                ConfigNode[] nodes = config.GetNodes("STATIC_RESTORE");

                for (int i = 0; i < nodes.Length; i++)
                {
                    StaticRestore restore;
                    if (StaticRestore.TryParse(nodes[i], out restore))
                    {
                        if (restore.RequirementsMet())
                        {
                            restores.Add(restore);
                        }
                        else
                        {
                            Debug.Log(LogPrefix + "Skipping " + restore.Label + ": folder requirements not met.");
                        }
                    }
                }
            }

            if (restores.Count == 0)
            {
                restores.Add(new StaticRestore
                {
                    BodyName = "Vall",
                    CityName = "Icehenge",
                    RepositionRadial = new Vector3(16296.905786f, -261481.564573f, 149552.426329f),
                    RepositionRadiusOffset = 1668.901232,
                    RepositionToSphere = true,
                    RepositionToSphereSurface = false,
                    RepositionToSphereSurfaceAddHeight = false,
                    ReorientToSphere = true,
                    ReorientInitialUp = Vector3.up,
                    ReorientFinalAngle = 190f,
                    ApplyAllMatches = true
                });
            }

            loaded = true;
            Debug.Log(LogPrefix + "Loaded " + restores.Count + " static restore rule(s).");
        }

        private void TryApplyAll()
        {
            attemptCount++;

            if (FlightGlobals.Bodies == null || FlightGlobals.Bodies.Count == 0)
            {
                if (attemptCount <= 10 || attemptCount % 10 == 0)
                    Debug.Log(LogPrefix + "Waiting for FlightGlobals.Bodies. attempt=" + attemptCount);
                return;
            }

            bool allComplete = true;
            for (int i = 0; i < restores.Count; i++)
            {
                StaticRestore restore = restores[i];
                if (restore.Applied)
                    continue;

                bool complete = ApplyRestore(restore);
                allComplete = allComplete && complete;
            }

            if (allComplete)
            {
                enabled = false;
                Debug.Log(LogPrefix + "All static restore rules applied. Disabling updater.");
            }
            else if (attemptCount >= 120)
            {
                enabled = false;
                Debug.LogWarning(LogPrefix + "Stopped after 120 attempts. At least one restore rule did not apply.");
            }
        }

        private bool ApplyRestore(StaticRestore restore)
        {
            CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                string.Equals(b.bodyName, restore.BodyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.name, restore.BodyName, StringComparison.OrdinalIgnoreCase));

            if (body == null)
            {
                LogWaiting(restore, "body not found");
                return false;
            }

            if (body.pqsController == null)
            {
                LogWaiting(restore, "body has no pqsController");
                return false;
            }

            PQSCity[] matches = body.pqsController.GetComponentsInChildren<PQSCity>(true)
                .Where(c => string.Equals(c.name, restore.CityName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0)
            {
                LogWaiting(restore, "PQSCity not found on body");
                return false;
            }

            int count = restore.ApplyAllMatches ? matches.Length : 1;
            for (int i = 0; i < count; i++)
            {
                ApplyToCity(body, matches[i], restore, i, matches.Length);
            }

            restore.Applied = true;
            return true;
        }

        private void ApplyToCity(CelestialBody body, PQSCity city, StaticRestore restore, int matchIndex, int matchCount)
        {
            Vector3 oldRadial = city.repositionRadial;
            double oldOffset = city.repositionRadiusOffset;

            city.repositionRadial = restore.RepositionRadial;
            city.repositionRadiusOffset = restore.RepositionRadiusOffset;
            city.repositionToSphere = restore.RepositionToSphere;
            city.repositionToSphereSurface = restore.RepositionToSphereSurface;
            city.repositionToSphereSurfaceAddHeight = restore.RepositionToSphereSurfaceAddHeight;
            city.reorientToSphere = restore.ReorientToSphere;
            city.reorientInitialUp = restore.ReorientInitialUp;
            city.reorientFinalAngle = restore.ReorientFinalAngle;

            try
            {
                city.ResetCelestialBody();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "ResetCelestialBody failed for " + restore.Label + ": " + ex.Message);
            }

            try
            {
                city.Orientate(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "Orientate(true) failed for " + restore.Label + ": " + ex.Message);
            }

            try
            {
                city.CheckLocals();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "CheckLocals failed for " + restore.Label + ": " + ex.Message);
            }

            try
            {
                body.pqsController.RebuildSphere();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "RebuildSphere failed for " + restore.Label + ": " + ex.Message);
            }

            Debug.Log(LogPrefix + "Applied " + restore.Label +
                " match=" + (matchIndex + 1) + "/" + matchCount +
                " oldRadial=" + FormatVector(oldRadial) +
                " oldOffset=" + oldOffset.ToString("R", CultureInfo.InvariantCulture) +
                " newRadial=" + FormatVector(city.repositionRadial) +
                " newOffset=" + city.repositionRadiusOffset.ToString("R", CultureInfo.InvariantCulture) +
                " transformLocal=" + FormatVector(city.transform.localPosition) +
                " transformWorld=" + FormatVector(city.transform.position));
        }

        private void LogWaiting(StaticRestore restore, string reason)
        {
            if (attemptCount <= 10 || attemptCount % 10 == 0)
                Debug.Log(LogPrefix + "Waiting for " + restore.Label + ": " + reason + ". attempt=" + attemptCount);
        }

        private static string FormatVector(Vector3 v)
        {
            return v.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                   v.y.ToString("R", CultureInfo.InvariantCulture) + "," +
                   v.z.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class StaticRestore
    {
        public string BodyName;
        public string CityName;
        public Vector3 RepositionRadial;
        public double RepositionRadiusOffset;
        public bool RepositionToSphere;
        public bool RepositionToSphereSurface;
        public bool RepositionToSphereSurfaceAddHeight;
        public bool ReorientToSphere;
        public Vector3 ReorientInitialUp;
        public float ReorientFinalAngle;
        public bool ApplyAllMatches;
        public List<string> NeedsAllFolders = new List<string>();
        public List<string> NeedsAnyFolders = new List<string>();
        public bool Applied;

        public string Label
        {
            get { return BodyName + "/" + CityName; }
        }

        public static bool TryParse(ConfigNode node, out StaticRestore restore)
        {
            restore = new StaticRestore();

            restore.BodyName = GetString(node, "body", "");
            restore.CityName = GetString(node, "city", "");
            if (string.IsNullOrEmpty(restore.BodyName) || string.IsNullOrEmpty(restore.CityName))
            {
                Debug.LogWarning("[EasterEggRestored] STATIC_RESTORE missing body or city; skipping node.");
                return false;
            }

            restore.RepositionRadial = GetVector3(node, "repositionRadial", Vector3.zero);
            restore.RepositionRadiusOffset = GetDouble(node, "repositionRadiusOffset", 0.0);
            restore.RepositionToSphere = GetBool(node, "repositionToSphere", true);
            restore.RepositionToSphereSurface = GetBool(node, "repositionToSphereSurface", false);
            restore.RepositionToSphereSurfaceAddHeight = GetBool(node, "repositionToSphereSurfaceAddHeight", false);
            restore.ReorientToSphere = GetBool(node, "reorientToSphere", true);
            restore.ReorientInitialUp = GetVector3(node, "reorientInitialUp", Vector3.up);
            restore.ReorientFinalAngle = GetFloat(node, "reorientFinalAngle", 0f);
            restore.ApplyAllMatches = GetBool(node, "applyAllMatches", true);
            restore.NeedsAllFolders = GetStringList(node, "needsFolder");
            restore.NeedsAnyFolders = GetStringList(node, "needsAnyFolder");
            return true;
        }

        public bool RequirementsMet()
        {
            for (int i = 0; i < NeedsAllFolders.Count; i++)
            {
                if (!GameDataFolderExists(NeedsAllFolders[i]))
                    return false;
            }

            if (NeedsAnyFolders.Count > 0)
            {
                bool foundAny = false;
                for (int i = 0; i < NeedsAnyFolders.Count; i++)
                {
                    if (GameDataFolderExists(NeedsAnyFolders[i]))
                    {
                        foundAny = true;
                        break;
                    }
                }

                if (!foundAny)
                    return false;
            }

            return true;
        }

        private static bool GameDataFolderExists(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return true;

            string relative = folderName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", relative);
            return Directory.Exists(path);
        }

        private static string GetString(ConfigNode node, string key, string defaultValue)
        {
            return node.HasValue(key) ? node.GetValue(key) : defaultValue;
        }

        private static List<string> GetStringList(ConfigNode node, string key)
        {
            List<string> values = new List<string>();
            if (!node.HasValue(key))
                return values;

            string[] rawValues = node.GetValues(key);
            for (int i = 0; i < rawValues.Length; i++)
            {
                string[] parts = rawValues[i].Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < parts.Length; j++)
                {
                    string value = parts[j].Trim();
                    if (value.Length > 0)
                        values.Add(value);
                }
            }

            return values;
        }

        private static bool GetBool(ConfigNode node, string key, bool defaultValue)
        {
            if (!node.HasValue(key))
                return defaultValue;

            bool value;
            return bool.TryParse(node.GetValue(key), out value) ? value : defaultValue;
        }

        private static float GetFloat(ConfigNode node, string key, float defaultValue)
        {
            if (!node.HasValue(key))
                return defaultValue;

            float value;
            return float.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        private static double GetDouble(ConfigNode node, string key, double defaultValue)
        {
            if (!node.HasValue(key))
                return defaultValue;

            double value;
            return double.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        private static Vector3 GetVector3(ConfigNode node, string key, Vector3 defaultValue)
        {
            if (!node.HasValue(key))
                return defaultValue;

            string[] parts = node.GetValue(key).Split(',');
            if (parts.Length != 3)
                return defaultValue;

            float x;
            float y;
            float z;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x))
                return defaultValue;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                return defaultValue;
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                return defaultValue;

            return new Vector3(x, y, z);
        }
    }
}
