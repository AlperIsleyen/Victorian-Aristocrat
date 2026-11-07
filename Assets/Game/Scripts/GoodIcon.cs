using System.Collections.Generic;
using UnityEngine;

namespace VictorianAristocrat
{
    public static class GoodIcon
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Texture2D texture;

        public static Sprite Get(string id)
        {
            if (cache.TryGetValue(id, out var sprite)) return sprite;
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Icon " + id };
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) texture.SetPixel(x, y, clear);
            var outline = new Color(.11f, .13f, .13f);
            switch (id)
            {
                case "wood":
                    Rect(10, 14, 44, 11, outline); Rect(12, 16, 40, 7, new Color(.55f,.30f,.13f));
                    Rect(15, 29, 38, 11, outline); Rect(17, 31, 34, 7, new Color(.67f,.40f,.19f));
                    Rect(13, 44, 37, 9, outline); Rect(15, 46, 33, 5, new Color(.52f,.28f,.12f)); break;
                case "grain":
                    for (int i = 0; i < 4; i++) { Line(15 + i * 10, 9, 17 + i * 9, 51, new Color(.43f,.34f,.12f), 2); Circle(16 + i * 10, 39, 5, new Color(.88f,.68f,.21f)); Circle(19 + i * 9, 47, 5, new Color(.95f,.79f,.31f)); } break;
                case "iron": case "coal": case "sulfur":
                    var ore = id == "iron" ? new Color(.54f,.51f,.48f) : id == "coal" ? new Color(.16f,.20f,.23f) : new Color(.88f,.77f,.19f);
                    Circle(23, 26, 15, outline); Circle(40, 28, 14, outline); Circle(31, 39, 16, outline);
                    Circle(23, 27, 12, ore); Circle(40, 29, 11, ore); Circle(31, 40, 12, ore); break;
                case "cotton": case "wool":
                    var fiber = id == "cotton" ? new Color(.96f,.95f,.87f) : new Color(.81f,.80f,.72f);
                    Circle(22, 30, 13, outline); Circle(42, 30, 13, outline); Circle(32, 41, 15, outline);
                    Circle(22, 30, 10, fiber); Circle(42, 30, 10, fiber); Circle(32, 41, 12, fiber);
                    Rect(25, 15, 14, 8, new Color(.48f,.32f,.17f)); break;
                case "food":
                    Circle(32, 31, 22, outline); Circle(32, 32, 19, new Color(.82f,.55f,.25f));
                    Line(20, 32, 44, 32, new Color(.95f,.77f,.45f), 2); break;
                case "cloth":
                    Rect(12, 16, 40, 35, outline); Rect(15, 19, 34, 29, new Color(.29f,.49f,.56f));
                    Line(17, 33, 46, 33, new Color(.68f,.74f,.69f), 2); break;
                case "metal":
                    Rect(11, 16, 42, 16, outline); Rect(14, 19, 36, 10, new Color(.65f,.70f,.71f));
                    Rect(18, 33, 36, 16, outline); Rect(21, 36, 30, 10, new Color(.80f,.83f,.79f)); break;
                case "arms":
                    Rect(9, 31, 46, 7, outline); Rect(11, 33, 42, 3, new Color(.62f,.62f,.58f));
                    Rect(20, 19, 18, 13, new Color(.43f,.25f,.14f)); Rect(37, 27, 8, 4, outline); break;
                case "timber":
                    for (int i = 0; i < 3; i++) { Rect(10, 14 + i * 14, 45, 9, outline); Rect(12, 16 + i * 14, 41, 5, new Color(.69f,.44f,.21f)); } break;
            }
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
            cache[id] = sprite;
            return sprite;
        }

        static void Rect(int x, int y, int w, int h, Color color)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++)
                if (xx >= 0 && xx < 64 && yy >= 0 && yy < 64) texture.SetPixel(xx, yy, color);
        }
        static void Circle(int x, int y, int radius, Color color)
        {
            for (int yy = -radius; yy <= radius; yy++) for (int xx = -radius; xx <= radius; xx++)
                if (xx * xx + yy * yy <= radius * radius && x + xx >= 0 && x + xx < 64 && y + yy >= 0 && y + yy < 64)
                    texture.SetPixel(x + xx, y + yy, color);
        }
        static void Line(int x1, int y1, int x2, int y2, Color color, int radius)
        {
            int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
            for (int i = 0; i <= steps; i++)
                Circle(Mathf.RoundToInt(Mathf.Lerp(x1, x2, i / (float)steps)), Mathf.RoundToInt(Mathf.Lerp(y1, y2, i / (float)steps)), radius, color);
        }
    }
}
