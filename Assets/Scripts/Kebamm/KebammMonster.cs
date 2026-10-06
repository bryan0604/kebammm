using System.Collections;
using UnityEngine;

namespace Kebamm
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KebammMonster : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] KebammBallColour colour = KebammBallColour.Red;

        float _health;
        int _hitPts;
        int _destroyedPts;
        SpriteRenderer _sr;
        Color _baseColor;
        Coroutine _flashRoutine;
        bool _defeated;

        public float Health => _health;
        public float MaxHealth => maxHealth;
        public KebammBallColour Colour => colour;
        public bool IsDefeated => _defeated;

        public void Init(float hp, KebammBallColour ballColour, int hitPts, int destroyedPts)
        {
            maxHealth = hp;
            _health = hp;
            _defeated = false;
            _hitPts = hitPts;
            _destroyedPts = destroyedPts;
            colour = ballColour;
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = KebammVisualFactory.ColourFor(colour);
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

            float ballDamage = ball.Damage;
            float monsterHealth = _health;
            float damage = ballDamage - monsterHealth;
            _health = Mathf.Max(0f, monsterHealth - ballDamage);
            bool ballBroken = ball.TakeDamage(ballDamage);
            Debug.Log($"[Kebamm] Impact Damage={damage} (ball {ballDamage} - monster {monsterHealth}). Monster HP={_health}/{maxHealth}. Ball HP={ball.Health}");
            KebammScore.AddHit(_hitPts);
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.NotifyMonsterImpact();

            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
            KebammDamageNumber.Spawn(_sr.bounds, ballDamage);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash());

            if (ballBroken)
                Destroy(ball.gameObject);

            if (_health <= 0f && !_defeated)
            {
                _defeated = true;
                KebammScore.AddDestroy(_destroyedPts);
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