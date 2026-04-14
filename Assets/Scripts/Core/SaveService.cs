using System;
using System.IO;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    public sealed class SaveService
    {
        public const string DefaultFileName = "savegame.json";

        private readonly string _directory;

        public SaveService(string directoryOverride = null)
        {
            _directory = string.IsNullOrEmpty(directoryOverride)
                ? Application.persistentDataPath
                : directoryOverride;
        }

        private string FullPath => Path.Combine(_directory, DefaultFileName);

        public bool HasSave() => File.Exists(FullPath);

        public void Save(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(_directory);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(FullPath, json);
        }

        public bool TryLoad(out SaveData data)
        {
            data = null;
            if (!HasSave())
                return false;

            try
            {
                string json = File.ReadAllText(FullPath);
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null)
                    return false;

                data = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Load failed - treating as no save. {ex.Message}");
                return false;
            }
        }

        public void DeleteSave()
        {
            if (HasSave())
                File.Delete(FullPath);
        }
    }
}
