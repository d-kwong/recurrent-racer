using UnityEngine;
namespace Racing
{
    [CreateAssetMenu(menuName = "Racing/Prototype Settings")]
    public sealed class PrototypeSettings : ScriptableObject
    {
        [Min(1)] public float carLength = 5.4f;
        [Min(0.5f)] public float carWidth = 1.333333f;
        [Min(0.5f)] public float carHeight = 0.95f;
        [Range(0.35f, 1)] public float noseWidthRatio = 0.56f;
        [Range(0.25f, 0.65f)] public float cockpitHeightRatio = 0.52f;
        public float cockpitLongitudinalOffset = -0.3f;
        [Min(0.5f)] public float collisionWidth = 3.38f;
        [Min(0)] public float wheelGap = 0.1195833f;
        [Min(0.1f)] public float wheelWidth = 0.55f;
        [Min(0.2f)] public float wheelDiameter = 0.95f;
        public float wheelVerticalOffset = -0.08f;
        [Min(6)] public float roadWidth = 9;
        [Min(0.2f)] public float curbWidth = 1;
        [Min(20)] public float straightLength = 100;
        [Min(10)] public float cornerRadius = 16;
        public Color bodyColor = new Color(1,0.025f,0.015f);
        public Color accentColor = Color.white;
        public Color wheelColor = new Color(0.015f,0.015f,0.015f);
        public Color roadColor = new Color(0.32f,0.34f,0.36f);
        public Color groundColor = new Color(0.035f,0.19f,0.065f);
    }
}
