using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasterEggsRestored
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class EasterEggsRestoredBehaviour : MonoBehaviour
    {
        private const string LogPrefix = "[EasterEggsRestored] ";

        private readonly List<StaticRestore> restores = new List<StaticRestore>();
        private float nextAttemptTime;
        private int attemptCount;

        public void Start()
        {
            DontDestroyOnLoad(this);
            LoadSettings();
            nextAttemptTime = 0f;
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup < nextAttemptTime)
                return;

            nextAttemptTime = Time.realtimeSinceStartup + 1.0f;
            TryApplyAll();
        }

        private void LoadSettings()
        {
            restores.Clear();

            ConfigNode[] roots = GameDatabase.Instance != null
                ? GameDatabase.Instance.GetConfigNodes("EASTER_EGG_RESTORED")
                : new ConfigNode[0];

            for (int r = 0; r < roots.Length; r++)
            {
                ConfigNode[] nodes = roots[r].GetNodes("STATIC_RESTORE");
                for (int i = 0; i < nodes.Length; i++)
                {
                    StaticRestore restore;
                    if (StaticRestore.TryParse(nodes[i], out restore))
                        restores.Add(restore);
                }
            }

            if (restores.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "No STATIC_RESTORE rules found in GameDatabase.");
            }
            else
            {
                Debug.Log(LogPrefix + "Loaded " + restores.Count + " static restore rule(s) from GameDatabase.");
            }
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

            PQSCity[] matches = FindCitiesOnBody(body, restore.CityName);

            if (matches.Length == 0 && restore.CloneIfMissing)
            {
                PQSCity clone = TryCloneMissingCity(body, restore);
                if (clone != null)
                {
                    matches = new[] { clone };
                }
            }

            if (matches.Length == 0)
            {
                LogWaiting(restore, "PQSCity not found on body");
                return false;
            }

            for (int i = 0; i < matches.Length; i++)
            {
                ApplyToCity(body, matches[i], restore, i, matches.Length);
            }

            restore.Applied = true;
            return true;
        }

        private static PQSCity[] FindCitiesOnBody(CelestialBody body, string cityName)
        {
            return body.pqsController.GetComponentsInChildren<PQSCity>(true)
                .Where(c => string.Equals(c.name, cityName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private PQSCity TryCloneMissingCity(CelestialBody targetBody, StaticRestore restore)
        {
            PQSCity source = FindSourceCityForClone(targetBody, restore);
            if (source == null)
            {
                LogWaiting(restore, "source PQSCity for clone not found");
                return null;
            }

            try
            {
                GameObject cloneObject = Object.Instantiate(source.gameObject);
                cloneObject.name = source.gameObject.name;
                cloneObject.transform.parent = targetBody.pqsController.transform;
                cloneObject.transform.localPosition = source.transform.localPosition;
                cloneObject.transform.localRotation = source.transform.localRotation;
                cloneObject.transform.localScale = source.transform.localScale;
                cloneObject.SetActive(true);

                PQSCity clone = cloneObject.GetComponent<PQSCity>() ?? cloneObject.GetComponentInChildren<PQSCity>(true);
                if (clone == null)
                {
                    Object.Destroy(cloneObject);
                    Debug.LogWarning(LogPrefix + "Clone created no PQSCity for " + restore.Label + ".");
                    return null;
                }

                clone.name = restore.CityName;
                clone.sphere = targetBody.pqsController;
                clone.transform.parent = targetBody.pqsController.transform;

                Debug.Log(LogPrefix + "Cloned missing " + restore.Label +
                    " from sourceBody=" + SafeCityBodyName(source) +
                    " sourceCity=" + source.name +
                    " sourceGameObject=" + source.gameObject.name + ".");

                return clone;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "Clone failed for " + restore.Label + ": " + ex);
                return null;
            }
        }

        private PQSCity FindSourceCityForClone(CelestialBody targetBody, StaticRestore restore)
        {
            string sourceCityName = string.IsNullOrEmpty(restore.CloneSourceCity) ? restore.CityName : restore.CloneSourceCity;
            string sourceBodyName = string.IsNullOrEmpty(restore.CloneSourceBody) ? restore.BodyName : restore.CloneSourceBody;

            PQSCity[] candidates = Resources.FindObjectsOfTypeAll<PQSCity>()
                .Where(c => c != null && c.gameObject != null)
                .Where(c => string.Equals(c.name, sourceCityName, StringComparison.OrdinalIgnoreCase))
                .Where(c => c.transform == null || !c.transform.IsChildOf(targetBody.pqsController.transform))
                .ToArray();

            if (candidates.Length == 0)
                return null;

            PQSCity bodyMatch = candidates.FirstOrDefault(c =>
                string.Equals(SafeCityBodyName(c), sourceBodyName, StringComparison.OrdinalIgnoreCase));

            if (bodyMatch != null)
                return bodyMatch;

            PQSCity fallback = candidates[0];
            Debug.LogWarning(LogPrefix + "No exact source body match for " + restore.Label +
                " sourceBody=" + sourceBodyName +
                "; using first matching city=" + fallback.name + ".");

            return fallback;
        }

        private void ApplyToCity(CelestialBody body, PQSCity city, StaticRestore restore, int matchIndex, int matchCount)
        {
            Vector3 oldRadial = city.repositionRadial;
            double oldOffset = city.repositionRadiusOffset;

            city.repositionRadial = restore.RepositionRadial;
            city.repositionRadiusOffset = restore.RepositionRadiusOffset;
            city.repositionToSphere = true;
            city.repositionToSphereSurface = false;
            city.repositionToSphereSurfaceAddHeight = false;
            city.reorientToSphere = true;
            city.reorientInitialUp = Vector3.up;
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

        private static string SafeCityBodyName(PQSCity city)
        {
            if (city == null)
                return "";

            try
            {
                if (city.celestialBody != null && !string.IsNullOrEmpty(city.celestialBody.bodyName))
                    return city.celestialBody.bodyName;
            }
            catch
            {
            }

            try
            {
                if (city.sphere != null && !string.IsNullOrEmpty(city.sphere.name))
                    return city.sphere.name;
            }
            catch
            {
            }

            return "";
        }
    }

    internal sealed class StaticRestore
    {
        public string BodyName;
        public string CityName;
        public Vector3 RepositionRadial;
        public double RepositionRadiusOffset;
        public float ReorientFinalAngle;
        public bool CloneIfMissing;
        public string CloneSourceBody;
        public string CloneSourceCity;
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
                Debug.LogWarning("[EasterEggsRestored] STATIC_RESTORE missing body or city; skipping node.");
                return false;
            }

            restore.RepositionRadial = GetVector3(node, "repositionRadial", Vector3.zero);
            restore.RepositionRadiusOffset = GetDouble(node, "repositionRadiusOffset", 0.0);
            restore.ReorientFinalAngle = GetFloat(node, "reorientFinalAngle", 0f);
            restore.CloneIfMissing = GetBool(node, "cloneIfMissing", false);
            restore.CloneSourceBody = GetString(node, "cloneSourceBody", "");
            restore.CloneSourceCity = GetString(node, "cloneSourceCity", "");
            return true;
        }

        private static string GetString(ConfigNode node, string key, string defaultValue)
        {
            return node.HasValue(key) ? node.GetValue(key) : defaultValue;
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
