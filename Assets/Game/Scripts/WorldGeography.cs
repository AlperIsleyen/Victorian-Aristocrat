using UnityEngine;

namespace VictorianAristocrat
{
    // The same authored geography drives terrain, navigation and both maps.
    public static class WorldGeography
    {
        public const float HalfSize = 3800f;
        public const float SeaLevel = 8f;
        static readonly int[] islandIds = { 7, 19 };
        public static readonly Vector2[] Places =
        {
            new Vector2(-2300,1700), new Vector2(-2480,1340), new Vector2(-1940,1400),
            new Vector2(-1700,2600), new Vector2(-2020,2360), new Vector2(-1440,2820),
            new Vector2(600,1560), new Vector2(2300,1500), new Vector2(300,1200),
            new Vector2(-500,2500), new Vector2(-780,2160), new Vector2(-140,2740),
            new Vector2(-2200,-300), new Vector2(-2560,60), new Vector2(-1760,-660),
            new Vector2(-2700,-2000), new Vector2(-2440,-1640), new Vector2(-2180,-2160),
            new Vector2(600,-1400), new Vector2(2300,-1500), new Vector2(180,-1140),
            new Vector2(-500,-2500), new Vector2(-820,-2200), new Vector2(-160,-2780)
        };

        static readonly Vector2[] coast =
        {
            new Vector2(-3800,2000), new Vector2(-2400,1600), new Vector2(-1400,1040),
            new Vector2(0,1240), new Vector2(1400,1020), new Vector2(2400,1640),
            new Vector2(3800,2000)
        };

        public static float CoastX(float z)
        {
            for (int i = 1; i < coast.Length; i++)
                if (z <= coast[i].x)
                {
                    float t = Mathf.InverseLerp(coast[i - 1].x, coast[i].x, z);
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(coast[i - 1].y, coast[i].y, t)
                        + 55f * Mathf.Sin(z * .009f) + 30f * Mathf.Sin(z * .021f);
                }
            return coast[coast.Length - 1].y;
        }

        public static float LandDistance(float x, float z)
        {
            float mainland = CoastX(z) - x;
            float islands = -10000f;
            foreach (int id in islandIds)
            {
                Vector2 p = Places[id];
                float angle = Mathf.Atan2(z - p.y, x - p.x);
                float radius = 300f + 30f * Mathf.Sin(angle * 5f + id) + 17f * Mathf.Sin(angle * 9f);
                islands = Mathf.Max(islands, radius - Vector2.Distance(new Vector2(x, z), p));
            }
            return Mathf.Max(mainland, islands);
        }

        public static bool IsLand(float x, float z, float margin = 0f)
        { return Mathf.Abs(x) < HalfSize - 12 && Mathf.Abs(z) < HalfSize - 12 && LandDistance(x, z) > margin; }

        static float RawHeight(float x, float z)
        {
            float distance = LandDistance(x, z);
            if (distance <= 0) return Mathf.Max(1f, SeaLevel - 3f + distance * .025f);
            float hills = 13f * Mathf.PerlinNoise(x * .0011f + 13f, z * .0011f + 7f)
                        + 8f * Mathf.PerlinNoise(x * .003f + 5f, z * .003f + 19f);
            float northernRidge = Mathf.Clamp01((z - 1700f) / 1500f) * 25f;
            return SeaLevel + 1f + Mathf.Min(distance / 95f, 1f) * (8f + hills + northernRidge);
        }

        public static float Height(float x, float z)
        {
            float height = RawHeight(x, z);
            for (int i = 0; i < Places.Length; i++)
            {
                Vector2 p = Places[i];
                float d = Vector2.Distance(new Vector2(x, z), p);
                float radius = i % 3 == 0 ? 105f : 65f;
                if (d > radius * 1.8f) continue;
                float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius * 1.8f, d));
                height = Mathf.Lerp(height, RawHeight(p.x, p.y), blend);
            }
            return height;
        }

        public static Vector3 Position(int id)
        {
            Vector2 p = Places[id];
            return new Vector3(p.x, Height(p.x, p.y), p.y);
        }
    }
}
