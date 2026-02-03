using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace KrayonEditor
{
    /// <summary>
    /// Guarda y carga preferencias del editor
    /// </summary>
    public static class EditorPrefs
    {
        private static readonly string PREFS_FILE = "editor_prefs.json";
        private static Dictionary<string, object> _prefs = new Dictionary<string, object>();
        private static bool _isDirty = false;

        static EditorPrefs()
        {
            Load();
        }

        public static void SetString(string key, string value)
        {
            _prefs[key] = value;
            _isDirty = true;
        }

        public static string GetString(string key, string defaultValue = "")
        {
            if (_prefs.TryGetValue(key, out object value) && value is string str)
            {
                return str;
            }
            return defaultValue;
        }

        public static void SetInt(string key, int value)
        {
            _prefs[key] = value;
            _isDirty = true;
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            if (_prefs.TryGetValue(key, out object value))
            {
                if (value is int intVal)
                    return intVal;
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    return element.GetInt32();
            }
            return defaultValue;
        }

        public static void SetFloat(string key, float value)
        {
            _prefs[key] = value;
            _isDirty = true;
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            if (_prefs.TryGetValue(key, out object value))
            {
                if (value is float floatVal)
                    return floatVal;
                if (value is double doubleVal)
                    return (float)doubleVal;
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    return element.GetSingle();
            }
            return defaultValue;
        }

        public static void SetBool(string key, bool value)
        {
            _prefs[key] = value;
            _isDirty = true;
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (_prefs.TryGetValue(key, out object value))
            {
                if (value is bool boolVal)
                    return boolVal;
                if (value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.True)
                        return true;
                    if (element.ValueKind == JsonValueKind.False)
                        return false;
                }
            }
            return defaultValue;
        }

        public static bool HasKey(string key)
        {
            return _prefs.ContainsKey(key);
        }

        public static void DeleteKey(string key)
        {
            if (_prefs.Remove(key))
            {
                _isDirty = true;
            }
        }

        public static void DeleteAll()
        {
            _prefs.Clear();
            _isDirty = true;
            Save();
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_prefs, options);
                File.WriteAllText(PREFS_FILE, json);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar EditorPrefs: {ex.Message}");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(PREFS_FILE))
                {
                    var json = File.ReadAllText(PREFS_FILE);
                    _prefs = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                        ?? new Dictionary<string, object>();
                }
                else
                {
                    _prefs = new Dictionary<string, object>();
                }
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar EditorPrefs: {ex.Message}");
                _prefs = new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Guarda automáticamente si hay cambios pendientes
        /// </summary>
        public static void AutoSave()
        {
            if (_isDirty)
            {
                Save();
            }
        }
    }
}