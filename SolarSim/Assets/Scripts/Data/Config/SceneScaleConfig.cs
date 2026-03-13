using UnityEngine;

namespace SpaceSim.Data.Config
{
    [CreateAssetMenu(fileName = "SceneScaleConfig", menuName = "SpaceSim/Scene Scale Config")]
    public class SceneScaleConfig : ScriptableObject
    {
        [Header("Distance Scaling")]
        [Tooltip("world distance (Mm) * this = scene units")]
        public float DistanceScale = 1.0f;

        [Header("Body Radius Scaling")]
        [Tooltip("world radius (Mm) * this = scene radius")]
        public float BodyRadiusScale = 1.0f;

        [Tooltip("Minimum scene diameter for any body")]
        public float MinBodyDiameter = 0.3f;

        [Header("Orbit Line")]
        public float OrbitLineBaseWidth = 0.05f;
        public float OrbitLineMaxWidthMultiplier = 2.0f;

        public float WorldToSceneDistance(double worldDistance)
        {
            return (float)(worldDistance * DistanceScale);
        }

        public float WorldToSceneDiameter(double worldRadius)
        {
            float raw = (float)(worldRadius * BodyRadiusScale * 2.0);
            return Mathf.Max(raw, MinBodyDiameter);
        }

        public float WorldToSceneRadius(double worldRadius)
        {
            return WorldToSceneDiameter(worldRadius) * 0.5f;
        }

        public Vector3 WorldToScenePosition(double x, double y, double z)
        {
            return new Vector3(
                (float)(x * DistanceScale),
                (float)(y * DistanceScale),
                (float)(z * DistanceScale));
        }
    }
}
