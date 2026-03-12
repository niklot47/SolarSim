using UnityEngine;

namespace SpaceSim.Rendering.Cameras
{
    /// <summary>
    /// Marks a GameObject as a valid camera focus target.
    /// Added to celestial body views automatically.
    /// </summary>
    public class CameraFocusTarget : MonoBehaviour
    {
        /// <summary>Optional offset from transform center for camera look-at point.</summary>
        public Vector3 FocusOffset = Vector3.zero;

        /// <summary>Suggested minimum camera distance for this target.</summary>
        public float SuggestedMinDistance = 5f;

        /// <summary>Get the world-space focus point.</summary>
        public Vector3 FocusPoint => transform.position + FocusOffset;
    }
}
