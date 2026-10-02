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
            _floorSr = floorGo.AddComponent<SpriteRenderer>();
            _floorSr.sprite = KebammVisualFactory.SquareSprite;
            _floorSr.color = new Color(0.55f, 0.45f, 0.35f);
            _floorSr.sortingOrder = 2;
            floorGo.transform.localScale = new Vector3(width + wallThickness * 2f, wallThickness, 1f);
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
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KebammVisualFactory.SquareSprite;
            sr.color = new Color(0.4f, 0.42f, 0.5f);
            sr.sortingOrder = 3;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<BoxCollider2D>();
        }

        public void OpenFloor()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            if (FloorCollider != null)
                FloorCollider.enabled = false;
            if (_floorSr != null)
                _floorSr.color = new Color(0.55f, 0.45f, 0.35f, 0.25f);
            Debug.Log("[Kebamm] Container floor OPEN — balls releasing");
        }

        public void CloseFloor()
        {
            IsOpen = false;
            if (FloorCollider != null)
                FloorCollider.enabled = true;
            if (_floorSr != null)
                _floorSr.color = new Color(0.55f, 0.45f, 0.35f, 1f);
            Debug.Log("[Kebamm] Container floor CLOSED");
        }
    }
}
