using UnityEngine;

namespace Kebamm
{
    /// <summary>Runtime sprite helpers so the prototype needs no imported art.</summary>
    public static class KebammVisualFactory
    {
        static Sprite _circle;
        static Sprite _square;

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null)
                    _circle = BuildCircle(64);
                return _circle;
            }
        }

        public static Sprite SquareSprite
        {
            get
            {
                if (_square == null)
                    _square = BuildSquare(16);
                return _square;
            }
        }

        static Sprite BuildCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            float r2 = r * r;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    tex.SetPixel(x, y, (dx * dx + dy * dy) <= r2 ? Color.white : Color.clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildSquare(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Color ColourFor(KebammBallColour colour)
        {
            switch (colour)
            {
                case KebammBallColour.Red: return new Color(0.92f, 0.28f, 0.28f);
                case KebammBallColour.Blue: return new Color(0.25f, 0.55f, 0.95f);
                case KebammBallColour.Green: return new Color(0.28f, 0.82f, 0.38f);
                case KebammBallColour.Yellow: return new Color(0.98f, 0.86f, 0.22f);
                default: return Color.white;
            }
        }
    }

    public enum KebammBallColour
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }
}
