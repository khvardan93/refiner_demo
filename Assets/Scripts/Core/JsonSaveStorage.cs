using System;
using System.IO;
using Save;
using UnityEngine;

namespace Core
{
    public sealed class JsonSaveStorage
    {
        private readonly string _path;
        private readonly string _tempPath;

        public string FilePath => _path; 
                
        public JsonSaveStorage(string fileName)
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _tempPath = $"{_path}.tmp";
        }

        public bool TryLoad(out GameState state)
        {
            state = null;
            try
            {
                if (!File.Exists(_path))
                    return false;

                state = JsonUtility.FromJson<GameState>(File.ReadAllText(_path));
                return state != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read save, starting fresh. {e.Message}");
                state = null;
                return false;
            }
        }

        public void Save(GameState state)
        {
            try
            {
                File.WriteAllText(_tempPath, JsonUtility.ToJson(state));
                if (File.Exists(_path))
                    File.Delete(_path);
                File.Move(_tempPath, _path);
                WebGLSaveSync.SyncToIndexedDB();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Write failed: {e.Message}");
            }
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            WebGLSaveSync.SyncToIndexedDB();
        }
    }
}
