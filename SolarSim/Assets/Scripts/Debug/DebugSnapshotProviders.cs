using System;
using System.Collections.Generic;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Interface for subsystems that can contribute data to debug snapshots.
    /// Implement this and register with GameDebug.RegisterSnapshotProvider().
    ///
    /// Providers must be defensive — if a subsystem is partially initialized
    /// or unavailable, return a status indicating that, never throw.
    /// </summary>
    public interface IDebugSnapshotProvider
    {
        /// <summary>
        /// Unique name of this provider (e.g. "Ships", "Economy", "Docking").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Capture a snapshot contribution from this subsystem.
        /// Must not throw. If subsystem is unavailable, return a snapshot
        /// with Status = "unavailable" and empty Data.
        /// </summary>
        SubsystemSnapshot CaptureSnapshot();
    }

    /// <summary>
    /// Registry for snapshot providers. Thread-safe add/remove/enumerate.
    /// </summary>
    internal static class SnapshotProviderRegistry
    {
        private static readonly List<IDebugSnapshotProvider> _providers = new List<IDebugSnapshotProvider>();
        private static readonly object _lock = new object();

        public static void Register(IDebugSnapshotProvider provider)
        {
            if (provider == null) return;
            lock (_lock)
            {
                // Avoid duplicates by name.
                for (int i = 0; i < _providers.Count; i++)
                {
                    if (_providers[i].ProviderName == provider.ProviderName)
                    {
                        _providers[i] = provider; // Replace existing.
                        return;
                    }
                }
                _providers.Add(provider);
            }
        }

        public static void Unregister(string providerName)
        {
            lock (_lock)
            {
                _providers.RemoveAll(p => p.ProviderName == providerName);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _providers.Clear();
            }
        }

        /// <summary>
        /// Capture snapshots from all registered providers.
        /// Catches exceptions per-provider to ensure one broken provider
        /// does not prevent others from contributing.
        /// </summary>
        public static List<SubsystemSnapshot> CaptureAll()
        {
            var results = new List<SubsystemSnapshot>();
            lock (_lock)
            {
                foreach (var provider in _providers)
                {
                    try
                    {
                        var snapshot = provider.CaptureSnapshot();
                        if (snapshot != null)
                            results.Add(snapshot);
                    }
                    catch (Exception ex)
                    {
                        // Provider failed — record a stub instead of crashing.
                        results.Add(new SubsystemSnapshot
                        {
                            Name = provider.ProviderName,
                            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}"
                        });
                    }
                }
            }
            return results;
        }

        public static int Count
        {
            get { lock (_lock) { return _providers.Count; } }
        }

        public static List<string> GetProviderNames()
        {
            var names = new List<string>();
            lock (_lock)
            {
                foreach (var p in _providers)
                    names.Add(p.ProviderName);
            }
            return names;
        }
    }
}
