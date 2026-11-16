using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Monster trebuchet (GDD [Level] Monster Group Design). One per level, placeholder art.
    /// - After Y player ball drops (Level Data trebuchet_start_drops; Magic Bombs never count) and at least
    ///   trebuchet_min_monsters in the level (default 5), construction is telegraphed and starts at stage 0.
    ///   The Yth drop does not also count toward a stage. Construction and firing do not re-check the count.
    /// - Every X more drops (trebuchet_stage_drops) adds Z stages (trebuchet_stage_gain); 5 stages = complete.
    ///   Stage bumps play after the drop resolves (ball lands / settles).
    /// - Complete: every W drops (trebuchet_launch_drops) it tosses a rock onto the launcher line, only while fewer
    ///   than 3 rocks are in play (live count). Player drops are blocked for the short toss.
    /// - Stationary static body, drawn in front of everything. Takes damage like obstacles (bombing run only).
    ///   HP 0: darkened leftover in place, no more building or launches, no rebuild this level.
    /// </summary>
    public class KebammTrebuchet : MonoBehaviour
    {
        public const int MaxStages = 5;
        public const int MaxRocks = 3;
        public const int MinMonstersToBuild = 5;
        public const int SortingOrder = 60; // in front of monsters (1), balls (10..20), rocks (25), overlays (30), bomb indicator (40)
        const float ResolveTimeout = 1.5f;
        const float ResolveExtra = 0.15f;
        const float TossDuration = 0.3f;
        const float ArmSwingDuration = 0.12f;

        public static KebammTrebuchet Instance { get; private set; }
        /// <summary>True while a rock toss is playing: the launcher cannot drop.</summary>
        public static bool TossLock => Instance != null && Instance._tossing > 0;

        static readonly Color WoodColour = new Color(0.58f, 0.4f, 0.22f);
        static readonly Color DarkWoodColour = new Color(0.4f, 0.27f, 0.15f);
        static readonly Color ScaffoldColour = new Color(1f, 0.85f, 0.4f, 0.35f);
        static readonly Color ReadyColour = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color DestroyedColour = new Color(0.25f, 0.25f, 0.25f);
        static readonly Color HitColour = Color.white;

        KebammPrototypeBootstrap _boot;
        KebammLevel _level;
        KebammSiegeData _data;

        int _drops, _sinceStage, _sinceLaunch, _stage, _shownStage;
        bool _started, _destroyed;
        bool _disabled;     // level has trebuchet_enabled Off: nothing built, shown or counted
        int _tossing;       // tosses currently playing (input lock)
        int _reservedRocks; // tosses decided but whose rock is not spawned yet
        float _health;

        Transform _visual, _arm, _armTip;
        GameObject _scaffold;
        readonly GameObject[] _stageParts = new GameObject[MaxStages];
        SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        Color[] _baseColours = Array.Empty<Color>();
        BoxCollider2D _col;
        Rigidbody2D _rb;
        AudioSource _audio;
        AudioClip _startClip, _stageClip, _tossClip;
        Coroutine _flash;

        public bool IsDestroyed => _destroyed;
        public bool IsComplete => _started && _stage >= MaxStages;
        public int Stage => _stage;
        public float Health => _health;

        // ---- Level Data (readable names, design letters in comments) ----
        int StartDrops => _level != null ? _level.TrebuchetStartDrops : KebammLevel.DefaultTrebuchetStartDrops;   // Y
        int StageDrops => _level != null ? _level.TrebuchetStageDrops : KebammLevel.DefaultTrebuchetStageDrops;   // X
        int StageGain => _level != null ? _level.TrebuchetStageGain : KebammLevel.DefaultTrebuchetStageGain;      // Z
        int LaunchDrops => _level != null ? _level.TrebuchetLaunchDrops : KebammLevel.DefaultTrebuchetLaunchDrops; // W
        int RockDestroyCount => _level != null ? _level.RockDestroyCount : KebammLevel.DefaultRockDestroyCount;    // T
        float RockSize => _level != null ? _level.RockSizeOr(_data.rockScale) : _data.rockScale;                   // rock_size (per level, fallback siege_data rockScale)
        int MinMonsters => _level != null ? _level.TrebuchetMinMonstersOr(MinMonstersToBuild) : MinMonstersToBuild; // trebuchet_min_monsters (per level, fallback 5)

        public static KebammTrebuchet Create(KebammPrototypeBootstrap boot, Transform parent)
        {
            var go = new GameObject("Trebuchet");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<KebammTrebuchet>();
            t._boot = boot;
            t.Build();
            Instance = t;
            return t;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---- Placeholder visual: scaffold (stage 0) + one part per stage ----
        void Build()
        {
            _data = KebammSiegeData.Get();
            _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Static; // stationary: no impact / force / knockback
            _col = gameObject.AddComponent<BoxCollider2D>();
            _col.size = new Vector2(1.5f, 1.7f);
            _col.offset = new Vector2(0f, 0.85f);
            _col.enabled = false;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            _scaffold = Part("Scaffold", _visual, KebammVisualFactory.SquareSprite, new Vector2(0f, 0.85f), new Vector2(1.6f, 1.7f), ScaffoldColour, 0).gameObject;

            // 1: base beam + wheels
            var s1 = Group("Stage1_Base");
            Part("Beam", s1, KebammVisualFactory.SquareSprite, new Vector2(0f, 0.2f), new Vector2(1.5f, 0.18f), WoodColour, 2);
            Part("WheelL", s1, KebammVisualFactory.CircleSprite, new Vector2(-0.5f, 0.13f), new Vector2(0.28f, 0.28f), DarkWoodColour, 3);
            Part("WheelR", s1, KebammVisualFactory.CircleSprite, new Vector2(0.5f, 0.13f), new Vector2(0.28f, 0.28f), DarkWoodColour, 3);
            // 2: A-frame uprights
            var s2 = Group("Stage2_Frame");
            Part("PostL", s2, KebammVisualFactory.SquareSprite, new Vector2(-0.22f, 0.75f), new Vector2(0.12f, 1.05f), WoodColour, 1).localRotation = Quaternion.Euler(0f, 0f, -12f);
            Part("PostR", s2, KebammVisualFactory.SquareSprite, new Vector2(0.22f, 0.75f), new Vector2(0.12f, 1.05f), WoodColour, 1).localRotation = Quaternion.Euler(0f, 0f, 12f);
            // 3: throwing arm (pivot on the frame top)
            var s3 = Group("Stage3_Arm");
            _arm = new GameObject("ArmPivot").transform;
            _arm.SetParent(s3, false);
            _arm.localPosition = new Vector3(0f, 1.25f, 0f);
            Part("Arm", _arm, KebammVisualFactory.SquareSprite, new Vector2(0.3f, 0f), new Vector2(1.5f, 0.1f), DarkWoodColour, 4);
            _armTip = new GameObject("ArmTip").transform;
            _armTip.SetParent(_arm, false);
            _armTip.localPosition = new Vector3(1.05f, 0f, 0f);
            // 4: counterweight + sling
            // (lives on the arm so it swings with it)
            var s4 = new GameObject("Stage4_Counterweight").transform;
            s4.SetParent(_arm, false);
            Part("Counterweight", s4, KebammVisualFactory.SquareSprite, new Vector2(-0.45f, -0.12f), new Vector2(0.34f, 0.34f), new Color(0.35f, 0.35f, 0.38f), 5);
            Part("Sling", s4, KebammVisualFactory.CircleSprite, new Vector2(1.05f, 0f), new Vector2(0.22f, 0.22f), new Color(0.45f, 0.42f, 0.4f), 5);
            // 5: complete - red banner on top
            var s5 = Group("Stage5_Ready");
            Part("Pole", s5, KebammVisualFactory.SquareSprite, new Vector2(0f, 1.6f), new Vector2(0.05f, 0.5f), DarkWoodColour, 6);
            Part("Flag", s5, KebammVisualFactory.TriangleSprite, new Vector2(0.15f, 1.75f), new Vector2(0.3f, 0.22f), ReadyColour, 7).localRotation = Quaternion.Euler(0f, 0f, -90f);
            _stageParts[0] = s1.gameObject;
            _stageParts[1] = s2.gameObject;
            _stageParts[2] = s3.gameObject;
            _stageParts[3] = s4.gameObject;
            _stageParts[4] = s5.gameObject;

            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColours = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColours[i] = _renderers[i].color;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            KebammAudio.RegisterSound(_audio);
            _startClip = Tone("TrebuchetStart", 330f, 0.35f, 0.5f);
            _stageClip = Tone("TrebuchetStage", 520f, 0.12f, 0.4f);
            _tossClip = Tone("TrebuchetToss", 180f, 0.2f, 0.6f);

            _visual.gameObject.SetActive(false);
        }

        Transform Group(string n)
        {
            var g = new GameObject(n).transform;
            g.SetParent(_visual, false);
            return g;
        }

        static Transform Part(string n, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color colour, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = SortingOrder + order;
            return go.transform;
        }

        /// <summary>Placeholder SFX: a short decaying tone (no game audio exists yet).</summary>
        static AudioClip Tone(string n, float freq, float seconds, float volume)
        {
            const int rate = 22050;
            int count = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float env = 1f - i / (float)count;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * env * volume;
            }
            var clip = AudioClip.Create(n, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip)
        {
            if (_audio != null && clip != null)
                _audio.PlayOneShot(clip);
        }

        // ---- Level lifecycle ----

        /// <summary>New level / restart / debug jump: clears all trebuchet state. Call after monsters spawn.</summary>
        public void ResetForLevel(KebammLevel level)
        {
            gameObject.SetActive(true); // a previous Off level may have switched the whole object off
            StopAllCoroutines();
            DOTween.Kill(this);
            KebammDestroyedFire.Clear(transform); // the trebuchet is reused across levels, so last level's fires must go
            _flash = null;
            _level = level;
            _disabled = false;
            _data = KebammSiegeData.Get();
            _drops = _sinceStage = _sinceLaunch = _stage = 0;
            _shownStage = -1;
            _started = _destroyed = false;
            _tossing = _reservedRocks = 0;
            _health = _data.trebuchetHp;
            name = $"{_data.trebuchetName}_{_data.trebuchetId}";
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].color = _baseColours[i];
            if (_arm != null)
                _arm.localRotation = Quaternion.Euler(0f, 0f, -25f);
            _visual.localScale = Vector3.one;
            _visual.gameObject.SetActive(false);
            _col.enabled = false;
            if (level == null)
                return;
            if (!level.trebuchetEnabled)
            {
                _disabled = true; // Off level: no object shown, no counting, no messages, no rocks, no toss lock
                gameObject.SetActive(false);
                Debug.Log($"[Kebamm] Level {level.levelNumber}: trebuchet_enabled is Off, no trebuchet in this level.");
                return;
            }
            Place();
            // Y = 0: start right away (still needs trebuchet_min_monsters, default 5).
            if (StartDrops <= 0 && _boot != null && _boot.MonstersInLevel >= MinMonsters)
            {
                _started = true;
                BeginConstruction();
            }
        }

        /// <summary>
        /// Village ground, random X on the platform (re-rolled on every level reset). The whole trebuchet stays on the
        /// platform and on screen: half-width 0.85 x scale + siege_data trebuchetEdgeMargin from the edges. Same Y, facing the jar (left).
        /// </summary>
        void Place()
        {
            float scale = _data.trebuchetScale;
            transform.localScale = Vector3.one * scale;
            float groundTop = _boot.MonsterY - 1f + _boot.GroundThickness * 0.5f;
            float half = 0.85f * scale + _data.trebuchetEdgeMargin;
            float min = -_boot.GroundWidth * 0.5f + half;
            float max = _boot.GroundWidth * 0.5f - half;
            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float halfW = cam.orthographicSize * cam.aspect;
                min = Mathf.Max(min, cam.transform.position.x - halfW + half);
                max = Mathf.Min(max, cam.transform.position.x + halfW - half);
            }
            float x = min < max ? UnityEngine.Random.Range(min, max) : (min + max) * 0.5f;
            transform.position = new Vector3(x, groundTop, 0f);
            Debug.Log($"[Kebamm] Trebuchet placed at random x={x:0.00} (range {min:0.00} to {max:0.00}).");
        }

        void BeginConstruction()
        {
            _visual.gameObject.SetActive(true);
            _col.enabled = true;
            IgnoreMonstersAndObstacles();
            ShowStage(0);
            Play(_startClip);
            Vfx(1.8f, new Color(1f, 0.6f, 0.2f, 0.5f));
            FloatText("Monsters are building\na TREBUCHET!", new Vector3(0f, (_boot.Container != null ? _boot.Container.transform.position.y : 0f), 0f), new Color(1f, 0.75f, 0.25f), 1.8f, 1.3f);
            Debug.Log($"[Kebamm] Trebuchet construction started after {_drops} drop(s) ({_boot.MonstersInLevel} monsters). Stage 0/{MaxStages}; +{StageGain} stage every {StageDrops} drop(s).");
        }

        void IgnoreMonstersAndObstacles()
        {
            KebammMonster.LiveColliders.RemoveAll(c => c == null);
            foreach (var c in KebammMonster.LiveColliders)
                Physics2D.IgnoreCollision(_col, c, true);
            KebammObstacle.LiveColliders.RemoveAll(c => c == null);
            foreach (var c in KebammObstacle.LiveColliders)
                Physics2D.IgnoreCollision(_col, c, true);
        }

        void ShowStage(int stage)
        {
            _shownStage = stage;
            _scaffold.SetActive(stage < MaxStages);
            for (int i = 0; i < MaxStages; i++)
                _stageParts[i].SetActive(i < stage);
            if (stage <= 0)
                return;
            _visual.DOKill();
            _visual.localScale = Vector3.one;
            _visual.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.6f).SetId(this);
            Play(_stageClip);
            Vfx(1.2f, new Color(0.85f, 0.75f, 0.55f, 0.55f), true);
            string text = stage >= MaxStages ? "TREBUCHET READY!" : $"Trebuchet {stage}/{MaxStages}";
            FloatText(text, transform.position + Vector3.up * (2.1f * _data.trebuchetScale), stage >= MaxStages ? ReadyColour : Color.white, 1.1f, stage >= MaxStages ? 1.1f : 0.8f);
            Debug.Log(stage >= MaxStages
                ? $"[Kebamm] Trebuchet complete: launches a rock every {LaunchDrops} drop(s) (max {MaxRocks} rocks)."
                : $"[Kebamm] Trebuchet stage {stage}/{MaxStages}");
        }

        // ---- Player drops ----

        /// <summary>Called by the launcher for every normal player ball drop (never Magic Bombs).</summary>
        public void OnPlayerDrop(KebammBall dropped)
        {
            if (_disabled || _destroyed || _level == null || _boot == null || _boot.IsBombingRun)
                return;
            if (!_started)
            {
                _drops++;
                if (_drops >= StartDrops && _boot.MonstersInLevel >= MinMonsters)
                {
                    _started = true; // this drop starts construction and does not count toward stage 1
                    StartCoroutine(AfterResolve(dropped, BeginConstruction));
                }
                return;
            }
            if (_stage < MaxStages)
            {
                if (++_sinceStage < StageDrops)
                    return;
                _sinceStage = 0;
                _stage = Mathf.Min(MaxStages, _stage + StageGain);
                int target = _stage;
                if (_stage >= MaxStages)
                    _sinceLaunch = 0;
                StartCoroutine(AfterResolve(dropped, () => ShowStage(target)));
                return;
            }
            if (++_sinceLaunch < LaunchDrops)
                return;
            _sinceLaunch = 0;
            int inPlay = KebammRock.Live.Count + _reservedRocks;
            if (inPlay >= MaxRocks)
            {
                Debug.Log($"[Kebamm] Trebuchet idle: {inPlay}/{MaxRocks} rocks already in play.");
                return;
            }
            _reservedRocks++;
            StartCoroutine(AfterResolve(dropped, () =>
            {
                if (_boot != null && _boot.IsBombingRun)
                {
                    _reservedRocks = Mathf.Max(0, _reservedRocks - 1); // the jar opened meanwhile: no toss
                    return;
                }
                StartCoroutine(Toss());
            }));
        }

        /// <summary>Waits until the dropped ball has landed (in play), is gone, or a short timeout.</summary>
        IEnumerator AfterResolve(KebammBall ball, Action action)
        {
            float t = 0f;
            while (ball != null && !ball.InPlay && t < ResolveTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(ResolveExtra);
            if (!_destroyed)
                action();
        }

        IEnumerator Toss()
        {
            _tossing++;
            try
            {
                if (_destroyed || _boot == null || _boot.Container == null)
                    yield break;
                Play(_tossClip);
                // Arm swings over toward the jar.
                _arm.DOKill();
                _arm.DOLocalRotate(new Vector3(0f, 0f, 140f), ArmSwingDuration).SetEase(Ease.OutQuad).SetId(this);
                yield return new WaitForSeconds(ArmSwingDuration * 0.7f);

                var container = _boot.Container;
                float size = RockSize;
                float radius = size * 0.5f;
                float cx = container.transform.position.x;
                float half = Mathf.Max(0f, container.InnerHalfWidth - radius - 0.05f);
                float targetX = UnityEngine.Random.Range(cx - half, cx + half);
                float lineY = _boot.DropController != null ? _boot.DropController.DropY : container.TopY + 0.35f;

                var go = new GameObject("Rock");
                go.transform.SetParent(_boot.BallsRoot != null ? _boot.BallsRoot : transform.parent, false);
                Vector3 from = _armTip.position;
                go.transform.position = new Vector3(from.x, from.y, 0f);
                var rock = go.AddComponent<KebammRock>();
                rock.Init(_data, RockDestroyCount, size);
                _reservedRocks = Mathf.Max(0, _reservedRocks - 1);
                Debug.Log($"[Kebamm] Trebuchet launches a rock at x={targetX:0.00} on the launcher line (rocks in play {KebammRock.Live.Count}/{MaxRocks}).");

                Vector3 to = new Vector3(targetX, lineY, 0f);
                float arc = Mathf.Max(1f, Mathf.Abs(to.y - from.y) * 0.4f + 1f);
                float time = 0f;
                while (time < TossDuration && rock != null)
                {
                    time += Time.deltaTime;
                    float k = Mathf.Clamp01(time / TossDuration);
                    Vector3 p = Vector3.Lerp(from, to, k);
                    p.y += Mathf.Sin(k * Mathf.PI) * arc;
                    rock.transform.position = p;
                    yield return null;
                }
                if (rock != null)
                {
                    rock.transform.position = to;
                    rock.Land();
                }
                _arm.DOLocalRotate(new Vector3(0f, 0f, -25f), 0.25f).SetEase(Ease.InOutQuad).SetId(this);
            }
            finally
            {
                _tossing = Mathf.Max(0, _tossing - 1);
            }
        }

        // ---- Damage (same rule as KebammObstacle: HP - ball damage, bombing run only) ----

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_boot == null || !_boot.IsBombingRun || _destroyed)
                return;
            var ball = collision.collider.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview || ball.IsBomb || ball.Spent)
                return;
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)ball.transform.position;
            ball.TakeDamage(ball.Damage);
            ReceiveHit(ball.Damage, "direct");
            KebammImpactExplosion.Explode(ball, point, this);
        }

        public void ReceiveHit(float ballDamage, string source)
        {
            if (_destroyed || !_started || _boot == null || !_boot.IsBombingRun)
                return;
            _health = Mathf.Max(0f, _health - ballDamage);
            Debug.Log($"[Kebamm] Trebuchet hit ({source}) damage={ballDamage} HP={_health}/{_data.trebuchetHp}");
            _boot.NotifyMonsterImpact();
            KebammDamageNumber.Spawn(_col.bounds, ballDamage);
            if (_health <= 0f)
            {
                Destroyed();
                return;
            }
            if (_flash != null)
                StopCoroutine(_flash);
            _flash = StartCoroutine(Flash());
        }

        IEnumerator Flash()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].color = HitColour;
            yield return new WaitForSeconds(0.08f);
            if (!_destroyed)
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null)
                        _renderers[i].color = _baseColours[i];
            _flash = null;
        }

        /// <summary>HP 0: darkened leftover in place (like monsters/obstacles), no collision, no build/launch, no rebuild.</summary>
        void Destroyed()
        {
            _destroyed = true;
            StopAllCoroutines();
            _flash = null;
            _tossing = 0;
            _reservedRocks = 0;
            for (int i = KebammRock.Live.Count - 1; i >= 0; i--) // a toss cut short must not leave a rock hanging in the air
                if (KebammRock.Live[i] != null && !KebammRock.Live[i].Landed)
                    KebammRock.Live[i].Land();
            // Explosion Bits: each visible part bursts in its own colour.
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].enabled && _renderers[i].gameObject.activeInHierarchy && _renderers[i].sprite != null)
                    KebammExplosionBits.Burst(_renderers[i].bounds, _baseColours[i], _col);
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].color = DestroyedColour;
            KebammDestroyedFire.Spawn(transform, _col.bounds); // before the collider switches off
            _col.enabled = false;
            Debug.Log("[Kebamm] Trebuchet destroyed: darkened, no more building or launches this level.");
        }

        // ---- Placeholder VFX / text ----

        void Vfx(float size, Color colour, bool stageUp = false)
        {
            if (KebammTrebuchetSmokeFx.Play(transform.position + Vector3.up * (0.85f * _data.trebuchetScale), _data.trebuchetScale, stageUp))
                return; // construction smoke prefab replaces the placeholder circle
            var go = new GameObject("TrebuchetVfx");
            go.transform.position = transform.position + Vector3.up * (0.85f * _data.trebuchetScale);
            go.transform.localScale = Vector3.one * (size * 0.3f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KebammVisualFactory.CircleSprite;
            sr.color = colour;
            sr.sortingOrder = SortingOrder - 1;
            go.transform.DOScale(size * _data.trebuchetScale, 0.35f).SetEase(Ease.OutQuad).SetLink(go);
            sr.DOFade(0f, 0.45f).SetEase(Ease.OutQuad).SetLink(go).OnComplete(() => { if (go != null) Destroy(go); });
        }

        void FloatText(string text, Vector3 pos, Color colour, float seconds, float size)
        {
            var go = new GameObject("TrebuchetText");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = colour;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(20f, 4f);
            TMP_FontAsset font = KebammDamageNumber.GetFont();
            if (font != null)
                tmp.font = font;
            var mr = tmp.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = SortingOrder + 20;
            float s = 0.07f * size;
            go.transform.localScale = Vector3.one * s * 0.5f;
            go.transform.DOScale(s, 0.2f).SetEase(Ease.OutBack).SetLink(go);
            go.transform.DOMoveY(pos.y + 0.4f, seconds).SetEase(Ease.OutQuad).SetLink(go);
            DOTween.To(() => tmp.alpha, a => tmp.alpha = a, 0f, 0.35f).SetDelay(Mathf.Max(0f, seconds - 0.35f)).SetLink(go)
                .OnComplete(() => { if (go != null) Destroy(go); });
        }
    }
}