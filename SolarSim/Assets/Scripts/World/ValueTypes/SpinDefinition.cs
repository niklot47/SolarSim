using System;

namespace SpaceSim.World.ValueTypes
{
    /// <summary>
    /// Pure data model for body self-rotation.
    /// Stored for future visualization; not fully rendered in MVP.
    /// </summary>
    [Serializable]
    public class SpinDefinition
    {
        /// <summary>Axial tilt in degrees relative to orbit normal.</summary>
        public double AxialTiltDeg;

        /// <summary>Rotation period in simulation seconds. Positive = prograde spin.</summary>
        public double RotationPeriod;

        /// <summary>Initial rotation angle in degrees at epoch.</summary>
        public double InitialRotationDeg;

        public SpinDefinition()
        {
            AxialTiltDeg = 0.0;
            RotationPeriod = 60.0;
            InitialRotationDeg = 0.0;
        }

        /// <summary>
        /// Create a simple non-tilted spin.
        /// </summary>
        public static SpinDefinition Simple(double rotationPeriod)
        {
            return new SpinDefinition
            {
                AxialTiltDeg = 0.0,
                RotationPeriod = rotationPeriod,
                InitialRotationDeg = 0.0
            };
        }

        public override string ToString()
        {
            return $"Spin[tilt={AxialTiltDeg:F1}° period={RotationPeriod:F1}s]";
        }
    }
}
