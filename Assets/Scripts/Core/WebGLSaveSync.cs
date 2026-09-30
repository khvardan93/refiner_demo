using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Bridges Application.persistentDataPath (an in-memory FS in WebGL builds) with the
    /// browser's IndexedDB store. Without this, writes made via System.IO.File never survive
    /// a page reload, since Unity's WebGL FS is not durable until FS.syncfs() runs.
    /// </summary>
    public static class WebGLSaveSync
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebGLSaveSync_Sync(bool populate, int id);
#endif

        private static int _nextId;
        private static readonly Dictionary<int, Action<bool>> PendingCallbacks = new();

        /// <summary>Pulls previously saved data from IndexedDB into the virtual FS. Call once before loading.</summary>
        public static void SyncFromIndexedDB(Action<bool> onComplete) => Sync(populate: true, onComplete);

        /// <summary>Pushes the virtual FS to IndexedDB so it survives a reload. Call after writing a save.</summary>
        public static void SyncToIndexedDB(Action<bool> onComplete = null) => Sync(populate: false, onComplete);

        private static void Sync(bool populate, Action<bool> onComplete)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSaveSyncReceiver.EnsureExists();
            var id = _nextId++;
            if (onComplete != null)
                PendingCallbacks[id] = onComplete;
            WebGLSaveSync_Sync(populate, id);
#else
            onComplete?.Invoke(true);
#endif
        }

        private static void Complete(int id, bool success)
        {
            if (PendingCallbacks.TryGetValue(id, out var callback))
            {
                PendingCallbacks.Remove(id);
                callback(success);
            }
        }

        private sealed class WebGLSaveSyncReceiver : MonoBehaviour
        {
            private static WebGLSaveSyncReceiver _instance;

            public static void EnsureExists()
            {
                if (_instance != null)
                    return;

                var go = new GameObject("WebGLSaveSyncReceiver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<WebGLSaveSyncReceiver>();
            }

            // Invoked by the .jslib plugin via SendMessage. Format: "<id>:<0|1>"
            private void OnSyncComplete(string message)
            {
                var parts = message.Split(':');
                var id = int.Parse(parts[0]);
                var success = parts[1] == "1";
                Complete(id, success);
            }
        }
    }
}
