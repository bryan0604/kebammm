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

        Transform _wallL, _wallR, _floor, _line;
        SpriteRenderer _artL, _artR;
        float _floorTopY;

        /// <summary>Builds the walls, floor and cap line with the inner floor top fixed at floorTopY.</summary>
        public void Build(float floorTopY, float width, float height, float wallThickness, float capsLineHeight)
        {
            _floorTopY = floorTopY;
            _wallL = CreateWall("WallLeft", out _artL);
            _wallR = CreateWall("WallRight", out _artR);

            // Floor / gate
            var floorGo = new GameObject("FloorGate");
            floorGo.transform.SetParent(transform, false);
            _floor = floorGo.transform;
            Sprite baseArt = LoadOnce(BasePath, ref _baseSprite, ref _baseLoaded, "grey/brown floor");
            if (baseArt != null)
            {
                // Base art (740x50 px @ 200 PPU) tiled to the floor width; 40 px end caps kept, centre repeats.
                var art = new GameObject("BaseArt");
                art.transform.SetParent(floorGo.transform, false);
                _floorSr = art.AddComponent<SpriteRenderer>();
                _floorSr.sprite = baseArt;
                _floorSr.drawMode = SpriteDrawMode.Tiled;
                _floorSr.tileMode = SpriteTileMode.Continuous;
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
            _line = lineGo.transform;
            var lineSr = lineGo.AddComponent<SpriteRenderer>();
            lineSr.sprite = KebammVisualFactory.SquareSprite;
            lineSr.color = new Color(1f, 0.35f, 0.2f, 0.65f);
            lineSr.sortingOrder = 5;

            Layout(width, height, wallThickness, capsLineHeight);
            IsOpen = false;
        }

        /// <summary>Moves the inner floor top (world y); takes effect on the next Layout call.</summary>
        public void SetFloorTop(float floorTopY) { _floorTopY = floorTopY; }

        /// <summary>Re-sizes/positions walls (colliders + art), floor and cap line. Safe to call live; the floor top stays put.</summary>
        public void Layout(float width, float height, float wallThickness, float capsLineHeight)
        {
            float x = transform.position.x;
            transform.position = new Vector3(x, _floorTopY + height * 0.5f, transform.position.z);
            InnerHalfWidth = width * 0.5f;
            FloorY = _floorTopY;
            CapacityY = _floorTopY + capsLineHeight;
            TopY = _floorTopY + height;

            Vector2 wallSize = new Vector2(wallThickness, height + wallThickness);
            float wallY = transform.position.y;
            PlaceWall(_wallL, _artL, new Vector3(x - InnerHalfWidth - wallThickness * 0.5f, wallY, 0f), wallSize);
            PlaceWall(_wallR, _artR, new Vector3(x + InnerHalfWidth + wallThickness * 0.5f, wallY, 0f), wallSize);

            Vector2 floorSize = new Vector2(width + wallThickness * 2f, wallThickness);
            _floor.position = new Vector3(x, FloorY - wallThickness * 0.5f, 0f);
            _floor.localScale = new Vector3(floorSize.x, floorSize.y, 1f);
            if (_floorSr != null && _floorSr.transform != _floor)
            {
                _floorSr.transform.localScale = new Vector3(1f / floorSize.x, 1f / floorSize.y, 1f);
                _floorSr.size = floorSize;
            }

            _line.position = new Vector3(x, CapacityY, 0f);
            _line.localScale = new Vector3(width, 0.06f, 1f);
        }

        /// <summary>Inner top of the pillars (world y).</summary>
        public float TopY { get; private set; }

        static void PlaceWall(Transform wall, SpriteRenderer art, Vector3 pos, Vector2 size)
        {
            wall.position = pos;
            wall.localScale = new Vector3(size.x, size.y, 1f);
            if (art != null)
            {
                art.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
                art.size = size;
            }
        }

        Transform CreateWall(string wallName, out SpriteRenderer artSr)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);
            artSr = null;

            Sprite pillar = LoadOnce(PillarPath, ref _pillarSprite, ref _pillarLoaded, "grey walls");
            if (pillar != null)
            {
                // Pillar art on an unscaled child, stretched to the wall size.
                var art = new GameObject("PillarArt");
                art.transform.SetParent(go.transform, false);
                artSr = art.AddComponent<SpriteRenderer>();
                artSr.sprite = pillar;
                // Sliced: top/bottom caps keep their pixels (borders in pillar.png.meta), shaft stretches.
                artSr.drawMode = SpriteDrawMode.Sliced;
                artSr.sortingOrder = 3;
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = KebammVisualFactory.SquareSprite;
                sr.color = new Color(0.4f, 0.42f, 0.5f);
                sr.sortingOrder = 3;
            }

            // Collider: 1x1 box scaled to the wall size.
            go.AddComponent<BoxCollider2D>();
            return go.transform;
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
