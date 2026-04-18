using System;
using System.IO;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    public sealed class SaveService
    {
        public const string DefaultFileName = "savegame.json";

        /// <summary>
        /// Previous primary snapshot produced by <see cref="File.Replace"/> on each successful save
        /// after the first. Used when the primary file is corrupt or truncated.
        /// </summary>
        public const string DefaultBackupFileName = "savegame.bak.json";

        private const string WriteStagingSuffix = ".tmp";

        private readonly string _directory;

        public SaveService(string directoryOverride = null)
        {
            _directory = string.IsNullOrEmpty(directoryOverride)
                ? Application.persistentDataPath
                : directoryOverride;
        }

        private string FullPath => Path.Combine(_directory, DefaultFileName);

        private string BackupPath => Path.Combine(_directory, DefaultBackupFileName);

        private string StagingPath => Path.Combine(_directory, DefaultFileName + WriteStagingSuffix);

        public bool HasSave() => File.Exists(FullPath) || File.Exists(BackupPath);

        public void Save(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(_directory);
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            string stagingPath = StagingPath;

            try
            {
                WriteSaveTextFlushed(stagingPath, json);

                if (!File.Exists(FullPath))
                {
                    File.Move(stagingPath, FullPath);
                }
                else
                {
                    File.Replace(stagingPath, FullPath, BackupPath);
                }
            }
            catch
            {
                TryDeleteFile(stagingPath);
                throw;
            }
        }

        public bool TryLoad(out SaveData data)
        {
            data = null;

            Exception primaryError = null;
            if (File.Exists(FullPath))
            {
                if (TryLoadCore(FullPath, out data, out primaryError))
                    return true;
            }

            if (File.Exists(BackupPath) && TryLoadCore(BackupPath, out data, out _))
            {
                Debug.LogWarning(
                    "[SaveService] Primary save unreadable; loaded from backup."
                    + (primaryError != null ? " " + primaryError.Message : string.Empty));
                return true;
            }

            if (File.Exists(FullPath) || File.Exists(BackupPath))
            {
                string detail = primaryError != null ? primaryError.Message : "validation failed";
                Debug.LogWarning($"[SaveService] Load failed - treating as no save. {detail}");
            }

            return false;
        }

        public void DeleteSave()
        {
            TryDeleteFile(FullPath);
            TryDeleteFile(BackupPath);
            TryDeleteFile(StagingPath);
        }

        private static void WriteSaveTextFlushed(string path, string contents)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(contents);
            }
        }

        private static bool TryLoadCore(string path, out SaveData data, out Exception error)
        {
            data = null;
            error = null;

            try
            {
                string json = File.ReadAllText(path);
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null)
                    return false;

                data = parsed;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; ignore locked or transient IO failures.
            }
        }
    }
}
