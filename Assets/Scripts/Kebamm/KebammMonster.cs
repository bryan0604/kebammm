using System.Collections;
using UnityEngine;

namespace Kebamm
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KebammMonster : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        float _health;
        SpriteRenderer _sr;
        Color _baseColor;
        Coroutine _flashRoutine;
        bool _defeated;

        public float Health => _health;
        public float MaxHealth => maxHealth;

        public void Init(float hp)
        {
            maxHealth = hp;
            _health = hp;
            _defeated = false;
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = new Color(0.75f, 0.22f, 0.55f);
            _sr.color = _baseColor;
            name = "Monster";
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_defeated)
                return;

            var ball = collision.collider.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview)
                return;

            float damage = (ball.Tier + 1) * 5f;
            _health = Mathf.Max(0f, _health - damage);
            Debug.Log($"[Kebamm] Monster hit by {ball.Colour} T{ball.Tier} for {damage} dmg. HP={_health}/{maxHealth}");

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash());

            // Ball is consumed on impact for a readable prototype loop.
            Destroy(ball.gameObject);

            if (_health <= 0f && !_defeated)
            {
                _defeated = true;
                Debug.Log("[Kebamm] Monster defeated");
                _sr.color = new Color(0.25f, 0.25f, 0.25f);
            }
        }

        IEnumerator Flash()
        {
            _sr.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            if (!_defeated)
                _sr.color = _baseColor;
            _flashRoutine = null;
        }

        public void ResetHealth()
        {
            _health = maxHealth;
            _defeated = false;
            if (_sr != null)
                _sr.color = _baseColor;
            Debug.Log($"[Kebamm] Monster HP reset to {_health}");
        }
    }
}
