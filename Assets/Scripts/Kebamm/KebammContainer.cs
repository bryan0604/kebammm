using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Left/right walls + floor/gate. Floor opens on capacity release so balls fall out.
    /// </summary>
    public class KebammContainer : MonoBehaviour
    {
        public Collider2D FloorCollider { get; private set; }
        public Transform FloorVisual { get; private set; }
        public bool IsOpen { get; private set; }

        SpriteRenderer _floorSr;
        Color _floorColor = new Color(0.55f, 0.45f, 0.35f);

        public float InnerHalfWidth { get; private set; }
        public float CapacityY { get; private set; }
        public float FloorY { get; private set; }

        public void Build(float width, float height, float wallThickness, float capacityFromTop)
        {
            InnerHalfWidth = width * 0.5f;
            FloorY = transform.position.y - height * 0.5f;
            float topY = transform.position.y + height * 0.5f;
            CapacityY = topY - capacityFromTop;

            // Left wall
            CreateWall("WallLeft",
                new Vector3(transform.position.x - InnerHalfWidth - wallThickness * 0.5f, transform.position.y, 0f),
                new Vector2(wallThickness, height + wallThickness));

            // Right wall
            CreateWall("WallRight",
                new Vector3(transform.position.x + InnerHalfWidth + wallThickness * 0.5f, transform.position.y, 0f),
                new Vector2(wallThickness, height + wallThickness));

            // Floor / gate
            var floorGo = new GameObject("FloorGate");
            floorGo.transform.SetParent(transform, false);
            floorGo.transform.position = new Vector3(transform.position.x, FloorY - wallThickness * 0.5f, 0f);
            Vector2 floorSize = new Vector2(width + wallThickness * 2f, wallThickness);
            floorGo.transform.localScale = new Vector3(floorSize.x, floorSize.y, 1f);
            Sprite baseArt = LoadOnce(BasePath, ref _baseSprite, ref _baseLoaded, "grey/brown floor");
            if (baseArt != null)
            {
                // Base art (740x50 px @ 200 PPU) tiled to the floor width; 40 px end caps kept, centre repeats.
                var art = new GameObject("BaseArt");
                art.transform.SetParent(floorGo.transform, false);
                art.transform.localScale = new Vector3(1f / floorSize.x, 1f / floorSize.y, 1f);
                _floorSr = art.AddComponent<SpriteRenderer>();
                _floorSr.sprite = baseArt;
                _floorSr.drawMode = SpriteDrawMode.Tiled;
                _floorSr.tileMode = SpriteTileMode.Continuous;
                _floorSr.size = floorSize;
                _floorColor = Color.white;
            }
            else
            {
                _floorSr = floorGo.AddComponent<SpriteRenderer>();
                _floorSr.sprite = KebammVisualFactory.SquareSprite;
            }
            _floorSr.color = _floorColor;
            _floorSr.sortingOrder = 2;
            FloorCollider = floorGo.AddComponent<BoxCollider2D>();
            FloorVisual = floorGo.transform;

            // Capacity line visual
            var lineGo = new GameObject("CapacityLineVisual");
            lineGo.transform.SetParent(transform, false);
            lineGo.transform.position = new Vector3(transform.position.x, CapacityY, 0f);
            var lineSr = lineGo.AddComponent<SpriteRenderer>();
            lineSr.sprite = KebammVisualFactory.SquareSprite;
            lineSr.color = new Color(1f, 0.35f, 0.2f, 0.65f);
            lineSr.sortingOrder = 5;
            lineGo.transform.localScale = new Vector3(width, 0.06f, 1f);

            IsOpen = false;
        }

        void CreateWall(string wallName, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            Sprite pillar = LoadOnce(PillarPath, ref _pillarSprite, ref _pillarLoaded, "grey walls");
            if (pillar != null)
            {
                // Pillar art at natural size (50x1090 px @ 200 PPU = 0.25 x 5.45 units) on an unscaled child.
                var art = new GameObject("PillarArt");
                art.transform.SetParent(go.transform, false);
                art.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
                var artSr = art.AddComponent<SpriteRenderer>();
                artSr.sprite = pillar;
                // Sliced: top/bottom caps keep their pixels (borders in pillar.png.meta), shaft stretches.
                artSr.drawMode = SpriteDrawMode.Sliced;
                artSr.size = size;
                artSr.sortingOrder = 3;
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = KebammVisualFactory.SquareSprite;
                sr.color = new Color(0.4f, 0.42f, 0.5f);
                sr.sortingOrder = 3;
            }

            // Collider unchanged: 1x1 box scaled to the wall size.
            go.AddComponent<BoxCollider2D>();
        }

        const string PillarPath = "Kebamm/Environment/pillar";
        static Sprite _pillarSprite;
        static bool _pillarLoaded;

        const string BasePath = "Kebamm/Environment/base";
        static Sprite _baseSprite;
        static bool _baseLoaded;

        static Sprite LoadOnce(string path, ref Sprite cache, ref bool loaded, string fallbackName)
        {
            if (!loaded)
            {
                loaded = true;
                cache = Resources.Load<Sprite>(path);
                if (cache == null)
                    Debug.LogWarning("[Kebamm] Sprite missing at Resources/" + path + "; using " + fallbackName + ".");
            }
            return cache;
        }

        public void OpenFloor()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            if (FloorCollider != null)
                FloorCollider.enabled = false;
            if (_floorSr != null)
                _floorSr.color = new Color(_floorColor.r, _floorColor.g, _floorColor.b, 0.25f);
            Debug.Log("[Kebamm] Container floor OPEN — balls releasing");
        }

        public void CloseFloor()
        {
            IsOpen = false;
            if (FloorCollider != null)
                FloorCollider.enabled = true;
            if (_floorSr != null)
                _floorSr.color = _floorColor;
            Debug.Log("[Kebamm] Container floor CLOSED");
        }
    }
}
