using UnityEngine;
using SpaceSim.World.Entities;
using SpaceSim.Data.Config;

using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.Rendering.Planets
{
    public class CelestialBodyView : MonoBehaviour
    {
        public EntityId BoundEntityId { get; private set; } = EntityId.None;
        public CelestialBodyType BodyType { get; private set; }

        /// <summary>Visual scale reduction factor for station cubes.</summary>
        private const float StationScaleFactor = 1.0f / 3.0f;

        private MeshRenderer _meshRenderer;

        public void Bind(CelestialBody body, SceneScaleConfig scaleConfig)
        {
            BoundEntityId = body.Id;
            BodyType = body.BodyType;
            gameObject.name = $"Body_{body.BodyType}_{body.Id}";
            ApplyScale(body, scaleConfig);
            ApplyColor(body);
        }

        public void SetWorldPosition(Vector3 scenePosition)
        {
            transform.position = scenePosition;
        }

        public void SetHighlight(bool highlighted)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer != null && _meshRenderer.material != null)
            {
                _meshRenderer.material.SetColor("_EmissionColor",
                    highlighted ? Color.white * 0.3f : Color.black);
            }
        }

        public void SetRepresentationMode(int mode) { }

        private void ApplyScale(CelestialBody body, SceneScaleConfig scaleConfig)
        {
            float diameter;
            if (scaleConfig != null)
                diameter = scaleConfig.WorldToSceneDiameter(body.Radius);
            else
                diameter = (float)(body.Radius * 2.0);

            // Stations are rendered 3x smaller for visual clarity.
            if (body.BodyType == CelestialBodyType.Station)
                diameter *= StationScaleFactor;

            transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        private void ApplyColor(CelestialBody body)
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer == null) return;

            Color color;

            if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
            {
                // Different colors per ship role for easy identification.
                color = body.ShipInfo.Role switch
                {
                    ShipRole.Player => new Color(0.2f, 1.0f, 0.4f),    // Bright green.
                    ShipRole.Trader => new Color(0.9f, 0.7f, 0.2f),    // Gold/yellow.
                    ShipRole.Patrol => new Color(0.9f, 0.3f, 0.3f),    // Red.
                    ShipRole.Civilian => new Color(0.7f, 0.7f, 0.8f),  // Light grey.
                    _ => new Color(0.6f, 0.8f, 0.9f)
                };
            }
            else if (body.BodyType == CelestialBodyType.Station && body.StationInfo != null)
            {
                // Station colors: orbital = cyan/white, surface = warm orange.
                color = body.StationInfo.Kind switch
                {
                    StationKind.Orbital => new Color(0.5f, 0.9f, 1.0f),   // Cyan-white.
                    StationKind.Surface => new Color(0.9f, 0.6f, 0.3f),   // Warm orange.
                    _ => new Color(0.8f, 0.8f, 0.9f)
                };
            }
            else
            {
                color = body.BodyType switch
                {
                    CelestialBodyType.Star => new Color(1f, 0.9f, 0.3f),
                    CelestialBodyType.Planet => new Color(0.3f, 0.5f, 0.8f),
                    CelestialBodyType.Moon => new Color(0.7f, 0.7f, 0.7f),
                    CelestialBodyType.Asteroid => new Color(0.5f, 0.4f, 0.3f),
                    CelestialBodyType.Station => new Color(0.8f, 0.8f, 0.9f),
                    CelestialBodyType.Ship => new Color(0.6f, 0.8f, 0.9f),
                    _ => Color.white
                };
            }

            _meshRenderer.material.color = color;

            // Emission for stars and player ships.
            if (body.BodyType == CelestialBodyType.Star)
                _meshRenderer.material.SetColor("_EmissionColor", color * 0.5f);
            else if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo?.Role == ShipRole.Player)
                _meshRenderer.material.SetColor("_EmissionColor", color * 0.15f);
            else if (body.BodyType == CelestialBodyType.Station)
                _meshRenderer.material.SetColor("_EmissionColor", color * 0.1f);
        }
    }
}
