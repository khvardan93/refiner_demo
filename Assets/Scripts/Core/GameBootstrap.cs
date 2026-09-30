using System;
using System.Collections.Generic;
using Configs;
using Offline;
using Save;
using UnityEngine;
using View;

namespace Core
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string SaveFileName = "space_prison_save.json";

        [SerializeField] private GameConfigs _config;

        [Header("Views")]
        [SerializeField] private PrisonView _prisonView;
        [SerializeField] private RoomActionPopup _actionPopup;
        [SerializeField] private HudView _hud;
        [SerializeField] private EconomyPanelView _economyPanel;
        [SerializeField] private OfflinePopupView _offlinePopup;
        [SerializeField] private TimeScaleView _timeScale;

        [Header("Saving")]
        [SerializeField] private bool _useSave = true;
        [SerializeField, Min(5f)] private float _autosaveSeconds = 30f;

        [Header("Mobile")]
        [SerializeField] private int _targetFrameRate = 60;

        private GameModel _model;
        private GameContext _context;
        private JsonSaveStorage _storage;
        private OfflineEarnings _offline;
        private float _autosaveTimer;
        private DateTime _pausedAtUtc;
        private bool _paused;

        public GameContext Context => _context;
        public OfflineReport LastOfflineReport { get; private set; }

        public event Action<OfflineReport> OfflineEarningsApplied;

        private void Awake()
        {
            Application.targetFrameRate = _targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            _storage = new JsonSaveStorage(SaveFileName);

            // In WebGL builds the save file lives in an in-memory FS until pulled from
            // IndexedDB, so loading must wait for that sync to finish first.
            WebGLSaveSync.SyncFromIndexedDB(_ => InitializeGame());
        }

        private void InitializeGame()
        {
            var setup = _config.ToSetup();
            _offline = new OfflineEarnings(new SystemTimeProvider(), setup.OfflineCap);

            if (_useSave && _storage.TryLoad(out var state))
            {
                _model = new GameModel(setup, state, out var warnings);
                foreach (var warning in warnings)
                    Debug.LogWarning($"[Save] {warning}");

                LastOfflineReport = _offline.Apply(state.LastSaveUtc, _model.Simulation, _model.Economy);
            }
            else
            {
                _model = new GameModel(setup);
            }

            _context = new GameContext(_model, new ContentLookup(_config));

            _economyPanel.Init(_context);
            _hud.Init(_context);
            _actionPopup.Init(_context);
            _offlinePopup.Init(_context);
            _prisonView.Init(_context);
            _prisonView.SlotClicked += _actionPopup.Open;

            if (_timeScale != null)
                _timeScale.Init(_context);
        }

        private void Start()
        {
            if (LastOfflineReport != null && LastOfflineReport.HasGains)
                RaiseOfflineApplied(LastOfflineReport);
        }

        private void Update()
        {
            if (_model == null || _paused)
                return;

            _model.Simulation.Update(Time.unscaledDeltaTime);

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= _autosaveSeconds)
            {
                _autosaveTimer = 0;
                Save();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (_model == null)
                return;

            if (pause)
            {
                _paused = true;
                _pausedAtUtc = DateTime.UtcNow;
                Save();
            }
            else if (_paused)
            {
                _paused = false;
                var report = _offline.Apply(_pausedAtUtc, _model.Simulation, _model.Economy);
                LastOfflineReport = report;
                if (report.HasGains)
                    RaiseOfflineApplied(report);
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            _prisonView.SlotClicked -= _actionPopup.Open;
            _model?.Dispose();
        }

        public void Save()
        {
            if (_model == null || !_useSave)
                return;

            _storage.Save(SaveMapper.Capture(_model, DateTime.UtcNow));
        }

        [ContextMenu("Delete Save")]
        private void DeleteSave()
        {
            new JsonSaveStorage(SaveFileName).Delete();
            _useSave = false; 
            Debug.Log("[Save] Deleted. Restart play mode for a fresh game.");
        }

        private void RaiseOfflineApplied(OfflineReport report)
        {
            Debug.Log($"[Offline] {NumberFormat.Duration(report.Elapsed.TotalSeconds)}{(report.WasCapped ? " (capped)" : string.Empty)}");

            _actionPopup.Close();
            _economyPanel.Close();
            _offlinePopup.Show(report);

            OfflineEarningsApplied?.Invoke(report);
        }

        private void OnValidate()
        {
            this.RequireAssigned(_config, nameof(_config));
            this.RequireAssigned(_prisonView, nameof(_prisonView));
            this.RequireAssigned(_actionPopup, nameof(_actionPopup));
            this.RequireAssigned(_hud, nameof(_hud));
            this.RequireAssigned(_economyPanel, nameof(_economyPanel));
            this.RequireAssigned(_offlinePopup, nameof(_offlinePopup));
        }
    }
}
