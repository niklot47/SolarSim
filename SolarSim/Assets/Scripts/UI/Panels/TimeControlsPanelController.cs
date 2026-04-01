using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Simulation.Time;
using SpaceSim.UI.Localization;

namespace SpaceSim.UI.Panels
{
    /// <summary>
    /// UI Toolkit controller for time controls strip.
    ///
    /// Time scale presets (sim-s = real seconds):
    ///   x1       — real time (docking, combat)
    ///   x86400   — 1 real second = 1 game day  (strategic, default)
    ///   x864000  — 1 real second = 10 game days (fast-forward)
    /// </summary>
    public class TimeControlsPanelController : MonoBehaviour
    {
        private SimulationClock _clock;
        private VisualElement   _root;

        private Button _btnPause;
        private Button _btnX1;
        private Button _btnX2;
        private Button _btnX3;
        private Label  _statusLabel;

        // Time scale presets: real-time, 1 day/sec, 10 days/sec.
        private readonly double[] _speeds      = { 1.0, 86400.0, 864000.0 };
        private readonly string[] _speedLabels = { "x1", "x86400", "x864000" };

        private Button[] _speedButtons;

        public void Initialize(SimulationClock clock)
        {
            _clock = clock;
        }

        public void SetupUI(VisualElement root)
        {
            _root = root;
            if (_root == null) return;

            _btnPause = _root.Q<Button>("time-btn-pause");
            _btnX1    = _root.Q<Button>("time-btn-x1");
            _btnX2    = _root.Q<Button>("time-btn-x10");
            _btnX3    = _root.Q<Button>("time-btn-x100");
            _statusLabel = _root.Q<Label>("time-status");

            _speedButtons = new[] { _btnX1, _btnX2, _btnX3 };

            if (_btnPause != null)
            {
                _btnPause.text = UIStrings.Get("time.pause");
                _btnPause.clicked += OnPauseClicked;
            }

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null) continue;
                _speedButtons[i].text = _speedLabels[i];
                int index = i;
                _speedButtons[i].clicked += () => SetSpeed(index);
            }

            // Apply initial speed that matches clock's current TimeScale.
            SyncButtonsToCurrentSpeed();
            UpdateVisuals();
        }

        private void OnPauseClicked()
        {
            if (_clock == null) return;
            if (_clock.IsPaused) _clock.Resume();
            else                 _clock.Pause();
            UpdateVisuals();
        }

        private void SetSpeed(int index)
        {
            if (_clock == null) return;
            if (index < 0 || index >= _speeds.Length) return;
            _clock.TimeScale = _speeds[index];
            if (_clock.IsPaused) _clock.Resume();
            UpdateVisuals();
        }

        /// <summary>
        /// Find which preset button matches the current TimeScale and highlight it.
        /// Called once on setup so the UI reflects the initial clock state.
        /// </summary>
        private void SyncButtonsToCurrentSpeed()
        {
            if (_clock == null) return;
            // Find the closest preset — default to index 1 (x86400) if none match.
            double best = double.MaxValue;
            int bestIdx = 1;
            for (int i = 0; i < _speeds.Length; i++)
            {
                double diff = System.Math.Abs(_clock.TimeScale - _speeds[i]);
                if (diff < best) { best = diff; bestIdx = i; }
            }
            // Don't change the clock, just update the visuals to match.
        }

        private void UpdateVisuals()
        {
            if (_clock == null) return;

            if (_btnPause != null)
                _btnPause.text = _clock.IsPaused
                    ? UIStrings.Get("time.resume")
                    : UIStrings.Get("time.pause");

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null) continue;
                bool active = !_clock.IsPaused
                    && System.Math.Abs(_clock.TimeScale - _speeds[i]) < 1.0;
                if (active) _speedButtons[i].AddToClassList("time-btn-active");
                else        _speedButtons[i].RemoveFromClassList("time-btn-active");
            }

            if (_statusLabel != null)
            {
                if (_clock.IsPaused)
                    _statusLabel.text = UIStrings.Get("time.status.paused");
                else
                {
                    double ts = _clock.TimeScale;
                    if (ts >= 86400.0)
                        _statusLabel.text = $"x{ts:F0}";
                    else
                        _statusLabel.text = $"x{ts:F0}";
                }
            }
        }
    }
}
