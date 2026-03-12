using System;

namespace SpaceSim.Simulation.Time
{
    /// <summary>
    /// Maintains simulation time independent of Unity frame rate.
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class SimulationClock
    {
        /// <summary>
        /// Current simulation time in seconds.
        /// </summary>
        public double CurrentTime { get; private set; }

        /// <summary>
        /// Total number of simulation ticks performed.
        /// </summary>
        public long TickCount { get; private set; }

        /// <summary>
        /// Time scale multiplier. 1.0 = real-time.
        /// </summary>
        public double TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0.0, value);
        }

        /// <summary>
        /// Whether the simulation clock is paused.
        /// </summary>
        public bool IsPaused { get; private set; }

        private double _timeScale = 1.0;

        public SimulationClock()
        {
            CurrentTime = 0.0;
            TickCount = 0;
            IsPaused = false;
        }

        /// <summary>
        /// Advance simulation time by deltaTime (in real seconds).
        /// Applies time scale. Does nothing if paused.
        /// </summary>
        public void Tick(double deltaTime)
        {
            if (IsPaused || deltaTime <= 0.0)
                return;

            double scaledDelta = deltaTime * _timeScale;
            CurrentTime += scaledDelta;
            TickCount++;
        }

        /// <summary>
        /// Pause the simulation clock.
        /// </summary>
        public void Pause()
        {
            IsPaused = true;
        }

        /// <summary>
        /// Resume the simulation clock.
        /// </summary>
        public void Resume()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Reset to initial state.
        /// </summary>
        public void Reset()
        {
            CurrentTime = 0.0;
            TickCount = 0;
            IsPaused = false;
            _timeScale = 1.0;
        }

        public override string ToString()
        {
            string state = IsPaused ? "PAUSED" : "RUNNING";
            return $"SimClock[{state}] t={CurrentTime:F2}s tick={TickCount} scale={_timeScale:F1}x";
        }
    }
}
