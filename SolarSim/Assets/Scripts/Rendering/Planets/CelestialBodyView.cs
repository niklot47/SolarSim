using UnityEngine;
using SpaceSim.World.Entities;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Planets
{
    /// <summary>
    /// MonoBehaviour representing a rendered celestial body in the scene.
    /// Reads data from CelestialBody and applies visual state.
    /// Future: support multiple representation modes / LOD.
    /// </summary>
    public class CelestialBodyView : MonoBehaviour
    {
        /// <summary>The entity id this view is bound to.</summary>
        public EntityId BoundEntityId { get; private set; } = EntityId.None;

        /// <summary>The body type (cached for rendering decisions).</summary>
        public CelestialBodyType BodyType { get; private set; }

        private MeshRenderer _meshRenderer;

        /// <summary>
        /// Bind this view to a world entity.
        /// </summary>
        public void Bind(CelestialBody body)
        {
            BoundEntityId = body.Id;
            BodyType = body.BodyType;

            // Use ASCII-safe name: BodyType + entity id. No localized text in scene hierarchy.
            gameObject.name = $"Body_{body.BodyType}_{body.Id}";

            ApplyScale(body.Radius);
            ApplyColor(body.BodyType);
        }

        /// <summary>
        /// Update world position from simulation data.
        /// </summary>
        public void SetWorldPosition(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// Apply visual highlight state (e.g. selection).
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponentInChildren<MeshRenderer>();

            if (_meshRenderer != null && _meshRenderer.material != null)
            {
                if (highlighted)
                    _meshRenderer.material.SetColor("_EmissionColor", Color.white * 0.3f);
                else
                    _meshRenderer.material.SetColor("_EmissionColor", Color.black);
            }
        }

        /// <summary>
        /// Placeholder for future LOD / representation mode switching.
        /// </summary>
        public void SetRepresentationMode(int mode)
        {
            // Reserved for future LOD system. No-op for now.
        }

        // --- Private helpers ---

        private void ApplyScale(double radius)
        {
            float r = (float)radius;
            transform.localScale = new Vector3(r * 2f, r * 2f, r * 2f);
        }

        private void ApplyColor(CelestialBodyType bodyType)
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer == null) return;

            Color color = bodyType switch
            {
                CelestialBodyType.Star => new Color(1f, 0.9f, 0.3f),
                CelestialBodyType.Planet => new Color(0.3f, 0.5f, 0.8f),
                CelestialBodyType.Moon => new Color(0.7f, 0.7f, 0.7f),
                CelestialBodyType.Asteroid => new Color(0.5f, 0.4f, 0.3f),
                CelestialBodyType.Station => new Color(0.8f, 0.8f, 0.9f),
                _ => Color.white
            };

            _meshRenderer.material.color = color;

            if (bodyType == CelestialBodyType.Star)
                _meshRenderer.material.SetColor("_EmissionColor", color * 0.5f);
        }
    }
}
