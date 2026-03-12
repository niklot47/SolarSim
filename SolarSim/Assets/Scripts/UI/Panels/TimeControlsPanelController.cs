using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Simulation.Time;
using SpaceSim.UI.Localization;

namespace SpaceSim.UI.Panels
{
    /// <summary>
    /// UI Toolkit controller for time controls strip.
    /// Provides pause/resume and time scale buttons.
    /// </summary>
    public class TimeControlsPanelController : MonoBehaviour
    {
        private SimulationClock _clock;
        private VisualElement _root;

        private Button _btnPause;
        private Button _btnX1;
        private Button _btnX10;
        private Button _btnX100;
        private Label _statusLabel;

        private readonly double[] _speeds = { 1.0, 10.0, 100.0 };
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
            _btnX1 = _root.Q<Button>("time-btn-x1");
            _btnX10 = _root.Q<Button>("time-btn-x10");
            _btnX100 = _root.Q<Button>("time-btn-x100");
            _statusLabel = _root.Q<Label>("time-status");

            _speedButtons = new[] { _btnX1, _btnX10, _btnX100 };

            // Set button labels from localization.
            if (_btnPause != null)
            {
                _btnPause.text = UIStrings.Get("time.pause");
                _btnPause.clicked += OnPauseClicked;
            }
            if (_btnX1 != null)
            {
                _btnX1.text = "x1";
                _btnX1.clicked += () => SetSpeed(0);
            }
            if (_btnX10 != null)
            {
                _btnX10.text = "x10";
                _btnX10.clicked += () => SetSpeed(1);
            }
            if (_btnX100 != null)
            {
                _btnX100.text = "x100";
                _btnX100.clicked += () => SetSpeed(2);
            }

            // Default: x1, running.
            SetSpeed(0);
            UpdateVisuals();
        }

        private void OnPauseClicked()
        {
            if (_clock == null) return;

            if (_clock.IsPaused)
                _clock.Resume();
            else
                _clock.Pause();

            UpdateVisuals();
        }

        private void SetSpeed(int index)
        {
            if (_clock == null) return;
            if (index < 0 || index >= _speeds.Length) return;

            _clock.TimeScale = _speeds[index];
            if (_clock.IsPaused)
                _clock.Resume();

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_clock == null) return;

            // Update pause button text.
            if (_btnPause != null)
            {
                _btnPause.text = _clock.IsPaused
                    ? UIStrings.Get("time.resume")
                    : UIStrings.Get("time.pause");
            }

            // Highlight active speed button.
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null) continue;

                bool active = !_clock.IsPaused
                    && System.Math.Abs(_clock.TimeScale - _speeds[i]) < 0.01;

                if (active)
                    _speedButtons[i].AddToClassList("time-btn-active");
                else
                    _speedButtons[i].RemoveFromClassList("time-btn-active");
            }

            // Status text.
            if (_statusLabel != null)
            {
                if (_clock.IsPaused)
                    _statusLabel.text = UIStrings.Get("time.status.paused");
                else
                    _statusLabel.text = $"x{_clock.TimeScale:F0}";
            }
        }
    }
}
