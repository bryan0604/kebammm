using UnityEngine;
using UnityEngine.InputSystem;

namespace Kebamm
{
    /// <summary>
    /// Mouse X aims drop position; left-click or Space drops the queued ball.
    /// Uses the new Input System (project activeInputHandler = Input System Only).
    /// </summary>
    public class KebammDropController : MonoBehaviour
    {
        public float DropCooldown = 0.35f;
        public float DropY = 3.6f;

        KebammContainer _container;
        KebammPrototypeBootstrap _bootstrap;
        KebammBall _preview;
        float _cooldownLeft;
        bool _canDrop = true;

        public void Init(KebammPrototypeBootstrap bootstrap, KebammContainer container, float dropY)
        {
            _bootstrap = bootstrap;
            _container = container;
            DropY = dropY;
            SpawnPreview();
        }

        public void SetDroppingEnabled(bool enabled)
        {
            _canDrop = enabled;
            if (_preview != null)
                _preview.gameObject.SetActive(enabled && (_container == null || !_container.IsOpen));
        }

        void Update()
        {
            if (_bootstrap == null || _container == null)
                return;

            if (_cooldownLeft > 0f)
                _cooldownLeft -= Time.deltaTime;

            bool allowDrop = _canDrop && !_container.IsOpen;
            if (_preview != null && _preview.gameObject.activeSelf != allowDrop)
                _preview.gameObject.SetActive(allowDrop);

            if (!allowDrop)
                return;

            UpdateAim();

            bool dropPressed = false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                dropPressed = true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                dropPressed = true;

            if (dropPressed && _cooldownLeft <= 0f)
                Drop();
        }

        void UpdateAim()
        {
            if (_preview == null)
                return;

            float x = transform.position.x;
            if (Mouse.current != null && Camera.main != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
                x = world.x;
            }

            float pad = _preview.Radius + 0.05f;
            float minX = _container.transform.position.x - _container.InnerHalfWidth + pad;
            float maxX = _container.transform.position.x + _container.InnerHalfWidth - pad;
            x = Mathf.Clamp(x, minX, maxX);
            _preview.transform.position = new Vector3(x, DropY, 0f);
        }

        void Drop()
        {
            if (_preview == null)
                return;

            Vector3 pos = _preview.transform.position;
            var colour = _preview.Colour;
            int tier = _preview.Tier;
            Destroy(_preview.gameObject);
            _preview = null;

            _bootstrap.SpawnBall(pos, colour, tier, preview: false);
            _cooldownLeft = DropCooldown;
            SpawnPreview();
        }

        public void SpawnPreview()
        {
            if (_preview != null)
                Destroy(_preview.gameObject);

            var colour = _bootstrap.NextQueuedColour();
            _preview = _bootstrap.SpawnBall(new Vector3(0f, DropY, 0f), colour, 0, preview: true);
            if (_preview != null)
                _preview.gameObject.SetActive(_canDrop && !_container.IsOpen);
        }

        public void ClearPreview()
        {
            if (_preview != null)
            {
                Destroy(_preview.gameObject);
                _preview = null;
            }
        }
    }
}
