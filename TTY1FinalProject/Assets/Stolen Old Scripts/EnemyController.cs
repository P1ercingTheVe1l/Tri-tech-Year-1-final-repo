using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System;

namespace TTY1
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        // Global multiplier applied to incoming damage. Perks can modify this.
        public static float DamageMultiplier = 1f;

        // Event fired when any enemy dies (used by perks like "In a Rush")
        public static event Action<GameObject> OnEnemyKilled;

        public event Action OnEnemyDied;

        // UI pause refcount: when > 0 enemies should stop moving/shooting
        private static int s_uiPauseRefCount = 0;

        public static void AddUiPause()
        {
            s_uiPauseRefCount++;
        }

        public static void RemoveUiPause()
        {
            s_uiPauseRefCount = Mathf.Max(0, s_uiPauseRefCount - 1);
        }

        // Shooting timing
        public Transform firePoint;
        public float fireRate = 2f;
        private float nextFireTime = 0f;

        // Animator used to play the shooting animation before the attack resolves.
        [Header("Animation")]
        [Tooltip("Optional Animator for the enemy. If assigned, the script will trigger the shoot animation before the attack.")]
        public Animator animator;
        [Tooltip("Trigger name on the Animator to start the shoot animation.")]
        public string shootTriggerName = "Shoot";
        [Tooltip("Fallback maximum time to wait for the shoot animation to finish (seconds).")]
        public float animationWaitTimeout = 2.0f;

        // New: delayed hitbox (spawned at player's position after animation and warning beam)
        [Header("Delayed Hitbox (hitscan alternative)")]
        [Tooltip("If true the enemy spawns a delayed hitbox at the player's position instead of firing a physical projectile.")]
        public bool useDelayedHitbox = true;
        [Tooltip("Radius of the spawned hitbox (meters).")]
        public float hitboxRadius = 1.5f;
        [Tooltip("Seconds that the warning beam is shown after the animation finishes before the hitbox activates.")]
        public float hitboxActivationDelay = 1.8f;
        [Tooltip("Lifetime of the spawned hitbox after activation (seconds).")]
        public float hitboxLifetime = 2.0f;
        [Tooltip("Horizontal knockback applied when hit.")]
        public float hitboxKnockback = 6f;
        [Tooltip("Vertical impulse applied when hit.")]
        public float hitboxVerticalImpulse = 2f;

        // Beam visuals
        [Header("Beam (warning)")]
        [Tooltip("Width of the warning beam shown in-game between the enemy and the target position.")]
        public float beamWidth = 0.08f;
        [Tooltip("Color of the warning beam during the warning period.")]
        public Color beamColor = Color.red;
        [Tooltip("Color of the beam once the hitbox becomes active.")]
        public Color beamActiveColor = Color.yellow;

        // Audio
        [Tooltip("Sound played when this enemy fires")]
        public AudioClip shootSound;
        [Tooltip("Sound played when this enemy dies")]
        public AudioClip deathSound;
        private AudioSource audioSource;

        // Simple ambient SFX (lightweight)
        [Header("Ambient SFX (simple)")]
        [Tooltip("Clips that can be played at random intervals")]
        public AudioClip[] ambientClips;
        [Tooltip("Minimum interval between ambient clips (seconds)")]
        public float sfxMinInterval = 8f;
        [Tooltip("Maximum interval between ambient clips (seconds)")]
        public float sfxMaxInterval = 18f;
        [Tooltip("Min volume for ambient clips")]
        [Range(0f, 1f)]
        public float sfxMinVolume = 0.8f;
        [Tooltip("Max volume for ambient clips")]
        [Range(0f, 1f)]
        public float sfxMaxVolume = 1f;
        [Tooltip("Start ambient SFX automatically")]
        public bool sfxPlayOnStart = true;

        private Coroutine sfxCoroutine;

        // Fight music (shared)
        [Header("Music")]
        [Tooltip("Optional fight music clip played when player is within this distance")]
        public AudioClip fightMusicClip;
        [Tooltip("Distance at which fight music should start (0 = use chaseDistance)")]
        public float fightMusicDistance = 0f;

        // Refcount to track enemies in fight range
        private static int s_fightMusicRefCount = 0;
        private bool _isInFightRange = false;

        // Expose whether any enemy currently has the player in fight range
        public static bool AnyEnemyInFightRange => s_fightMusicRefCount > 0;

        // Movement
        private GameObject player;
        private NavMeshAgent agent;
        public float chaseDistance = 10f;
        public float stopDistance = 2f; // distance at which enemy should stop approaching the player
        private Vector3 home;

        // Health
        public float health = 3;
        public Image healthBar;
        protected float maxHealth;

        // Attack / contact damage (editable per-enemy)
        [Header("Combat")]
        [Tooltip("Damage this enemy deals to the player (contact/attack)")]
        public int attackDamage = 1;

        // Flag used to keep the enemy standing still during the firing sequence.
        private bool _isFiring = false;

        /// <summary>
        /// Set the attack/contact damage for this enemy at runtime.
        /// </summary>
        public void SetAttackDamage(int damage)
        {
            attackDamage = damage;
        }

        /// <summary>
        /// Get the current attack/contact damage for this enemy.
        /// </summary>
        public int GetAttackDamage()
        {
            return attackDamage;
        }

        /// <summary>
        /// Modify attack damage by a signed delta (can be negative).
        /// </summary>
        public void ModifyAttackDamage(int delta)
        {
            attackDamage += delta;
            if (attackDamage < 0) attackDamage = 0;
        }

        void Start()
        {
            // Find player
            player = GameObject.FindGameObjectWithTag("Player");

            // NavMesh setup
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.stoppingDistance = stopDistance;
            }

            // Audio setup: prefer an existing AudioSource, otherwise add one
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // Ensure ambient audio uses 2D playback by default so it is audible regardless of distance.
            audioSource.spatialBlend = 0f;

            home = transform.position;

            // Health setup
            maxHealth = health;
            if (healthBar != null)
            {
                healthBar.fillAmount = health / maxHealth;
            }

            // Start simple ambient SFX if requested
            if (sfxPlayOnStart && ambientClips != null && ambientClips.Length > 0)
            {
                StartAmbientSfx();
            }

            // default fight music distance
            if (fightMusicDistance <= 0f)
                fightMusicDistance = chaseDistance;
        }

        private void OnEnable()
        {
            if (sfxPlayOnStart && ambientClips != null && ambientClips.Length > 0 && sfxCoroutine == null)
                StartAmbientSfx();
        }

        private void OnDisable()
        {
            if (_isInFightRange)
            {
                _isInFightRange = false;
                StopSharedFightMusicIfNeeded();
            }

            StopAmbientSfx();
        }

        private void OnDestroy()
        {
            if (_isInFightRange)
            {
                _isInFightRange = false;
                StopSharedFightMusicIfNeeded();
            }

            StopAmbientSfx();
        }

        protected virtual void Update()
        {
            // If any UI has requested a pause, stop movement and shooting logic here.
            if (s_uiPauseRefCount > 0)
            {
                if (agent != null)
                {
                    agent.isStopped = true;
                    if (agent.velocity.sqrMagnitude > 0f)
                        agent.velocity = Vector3.zero;
                }
                return;
            }

            if (player == null || agent == null) return;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            // fight music handling (per-enemy detection, shared audio)
            bool nowInRange = distance < Mathf.Max(0.0001f, fightMusicDistance);
            if (nowInRange && !_isInFightRange)
            {
                _isInFightRange = true;
                StartSharedFightMusicIfNeeded();
            }
            else if (!nowInRange && _isInFightRange)
            {
                _isInFightRange = false;
                StopSharedFightMusicIfNeeded();
            }

            // If currently in a firing sequence, keep the agent stopped and don't rotate/move.
            if (_isFiring)
            {
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                return;
            }

            if (distance < chaseDistance)
            {
                if (distance > stopDistance)
                {
                    agent.isStopped = false;
                    agent.SetDestination(player.transform.position);
                }
                else
                {
                    agent.isStopped = true;
                    if (agent.velocity.sqrMagnitude > 0f)
                        agent.velocity = Vector3.zero;
                }

                transform.LookAt(player.transform);

                if (Time.time >= nextFireTime)
                {
                    // If animator is absent, shoot immediately, otherwise play animation then beam/hitbox.
                    if (useDelayedHitbox)
                        StartCoroutine(ShootWithAnimationAndBeam(player.transform.position, animator == null));
                    else
                        Shoot(); // kept for compatibility; will log
                    nextFireTime = Time.time + fireRate;
                }
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(home);
            }
        }

        void Shoot()
        {
            if (player == null) return;

            if (shootSound != null)
            {
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(shootSound);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(shootSound, transform.position);
                }
            }

            if (useDelayedHitbox)
            {
                // Start coroutine used in Update instead; kept for safety
                StartCoroutine(ShootWithAnimationAndBeam(player.transform.position, animator == null));
                return;
            }

            // Physical projectile mode has been removed (bullet prefab not used).
            Debug.LogWarning("EnemyController: physical projectiles are disabled; enable useDelayedHitbox to attack.");
        }

        /// <summary>
        /// Play animation (if assigned and skipAnimation==false), wait until it finishes (or timeout),
        /// then show an in-game beam from the enemy/firePoint to the target position for hitboxActivationDelay seconds,
        /// and finally spawn an active hitbox at that position.
        /// If skipAnimation is true the method will skip animation waiting and proceed immediately.
        /// The beam is passed to the spawned hitbox so the hitbox can change the beam color on activation
        /// and destroy the beam when the hitbox expires.
        /// While firing the enemy will be stopped.
        /// </summary>
        private System.Collections.IEnumerator ShootWithAnimationAndBeam(Vector3 targetPosition, bool skipAnimation = false)
        {
            // mark firing state so Update() will keep the agent stopped and not rotate/move
            _isFiring = true;

            // remember previous agent settings so we can restore them later
            bool prevStopped = false;
            bool prevUpdatePos = true;
            bool prevUpdateRot = true;
            if (agent != null)
            {
                prevStopped = agent.isStopped;
                prevUpdatePos = agent.updatePosition;
                prevUpdateRot = agent.updateRotation;

                // fully stop agent while firing
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updatePosition = false;
                agent.updateRotation = false;
            }

            try
            {
                // trigger animation if available and not skipping
                if (!skipAnimation && animator != null && !string.IsNullOrEmpty(shootTriggerName))
                {
                    animator.SetTrigger(shootTriggerName);

                    float start = Time.time;
                    int startHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

                    while (Time.time - start < animationWaitTimeout)
                    {
                        var state = animator.GetCurrentAnimatorStateInfo(0);
                        if (state.shortNameHash != startHash)
                        {
                            // wait until state completes (normalizedTime >= 1)
                            while (state.normalizedTime < 1.0f && Time.time - start < animationWaitTimeout)
                            {
                                yield return null;
                                state = animator.GetCurrentAnimatorStateInfo(0);
                            }
                            break;
                        }
                        yield return null;
                    }
                }
                else
                {
                    // no animator or skipping: proceed immediately (still yield once so coroutine scheduling is consistent)
                    yield return null;
                }

                // play shoot sound if any (keep audio in sync with beam)
                if (shootSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(shootSound);
                }

                // create a visible beam in-game from firePoint (or enemy) to the target position
                GameObject beamGo = new GameObject("AttackBeam");
                LineRenderer lr = beamGo.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                Vector3 startPos = (firePoint != null) ? firePoint.position : transform.position;
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, targetPosition);
                lr.startWidth = beamWidth;
                lr.endWidth = beamWidth;
                lr.useWorldSpace = true;
                lr.numCapVertices = 6;
                lr.numCornerVertices = 6;
                Shader shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    lr.material = new Material(shader) { color = beamColor };
                }
                else
                {
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.material.SetColor("_Color", beamColor);
                }
                lr.startColor = beamColor;
                lr.endColor = beamColor;

                // show beam for hitboxActivationDelay seconds (warning period)
                float beamStart = Time.time;
                while (Time.time - beamStart < hitboxActivationDelay)
                {
                    if (lr != null)
                    {
                        startPos = (firePoint != null) ? firePoint.position : transform.position;
                        lr.SetPosition(0, startPos);
                        lr.SetPosition(1, targetPosition);
                    }
                    yield return null;
                }

                // spawn an active hitbox at the target position and hand it the beam reference
                GameObject go = new GameObject("DelayedHitbox");
                go.transform.position = targetPosition;
                var hitbox = go.AddComponent<DelayedHitbox>();
                hitbox.radius = Mathf.Max(0.1f, hitboxRadius);
                hitbox.activationDelay = 0f; // immediate activation after beam
                hitbox.lifetimeAfterActivation = Mathf.Max(0.1f, hitboxLifetime);
                // link damage to the enemy's attackDamage so it stays consistent
                hitbox.damage = Mathf.Max(1, attackDamage);
                hitbox.knockbackForce = hitboxKnockback;
                hitbox.verticalImpulse = hitboxVerticalImpulse;

                // pass beam to hitbox so it can change color on activation and be destroyed with the hitbox
                hitbox.warningBeam = lr;
                hitbox.beamActiveColor = beamActiveColor;
            }
            finally
            {
                // restore movement/rotation behavior
                if (agent != null)
                {
                    agent.updatePosition = prevUpdatePos;
                    agent.updateRotation = prevUpdateRot;
                    agent.isStopped = prevStopped;
                }

                _isFiring = false;
            }
        }

        private void SpawnDelayedHitboxAt(Vector3 worldPosition)
        {
            // kept for compatibility; prefer ShootWithAnimationAndBeam coroutine
            GameObject go = new GameObject("DelayedHitbox");
            go.transform.position = worldPosition;
            var hitbox = go.AddComponent<DelayedHitbox>();
            hitbox.radius = Mathf.Max(0.1f, hitboxRadius);
            hitbox.activationDelay = Mathf.Max(0f, hitboxActivationDelay);
            hitbox.lifetimeAfterActivation = Mathf.Max(0.1f, hitboxLifetime);
            hitbox.damage = Mathf.Max(1, attackDamage);
            hitbox.knockbackForce = hitboxKnockback;
            hitbox.verticalImpulse = hitboxVerticalImpulse;
        }

        // Health
        public virtual void TakeDamage(int damage)
        {
            float adjusted = damage * DamageMultiplier;
            int applied = Mathf.Max(1, Mathf.RoundToInt(adjusted));
            health -= applied;

            if (healthBar != null)
            {
                healthBar.fillAmount = health / maxHealth;
            }

            if (health <= 0)
            {
                StopAmbientSfx();

                if (deathSound != null)
                {
                    AudioSource.PlayClipAtPoint(deathSound, transform.position);
                }

                OnEnemyKilled?.Invoke(gameObject);
                OnEnemyDied?.Invoke();

                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("PlayerBullet"))
            {
                var pd = other.gameObject.GetComponent<ProjectileDamage>();
                int dmg = pd != null ? pd.damage : 1;
                TakeDamage(dmg);
                Destroy(other.gameObject);
            }

            // Optional: contact damage to player on collision — call player's TakeDamage using attackDamage
            if (other.gameObject.CompareTag("Player"))
            {
                var playerController = other.gameObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    playerController.TakeDamage(attackDamage);
                }
            }
        }

        // --- Simple ambient SFX helpers ---

        public void StartAmbientSfx()
        {
            if (sfxCoroutine != null) return;
            sfxCoroutine = StartCoroutine(AmbientSfxLoop());
        }

        public void StopAmbientSfx()
        {
            if (sfxCoroutine != null)
            {
                StopCoroutine(sfxCoroutine);
                sfxCoroutine = null;
            }
        }

        private System.Collections.IEnumerator AmbientSfxLoop()
        {
            while (true)
            {
                float wait = Mathf.Max(0f, UnityEngine.Random.Range(sfxMinInterval, sfxMaxInterval));
                yield return new WaitForSeconds(wait);

                if (ambientClips == null || ambientClips.Length == 0) continue;

                // Ensure we have a player reference
                if (player == null)
                {
                    player = GameObject.FindGameObjectWithTag("Player");
                    if (player == null) continue;
                }

                // Only play ambient SFX when the player is within the enemy's chase range
                float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
                if (distToPlayer > chaseDistance) continue;

                var clip = ambientClips[UnityEngine.Random.Range(0, ambientClips.Length)];
                if (clip == null) continue;

                float vol = Mathf.Clamp01(UnityEngine.Random.Range(sfxMinVolume, sfxMaxVolume));
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(clip, vol);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(clip, transform.position, vol);
                }
            }
        }

        // --- Shared fight music helpers ---
        private void StartSharedFightMusicIfNeeded()
        {
            if (fightMusicClip == null) return;

            // increment refcount (no direct control of player's BG music here)
            s_fightMusicRefCount++;
        }

        private void StopSharedFightMusicIfNeeded()
        {
            if (s_fightMusicRefCount <= 0) return;

            s_fightMusicRefCount = Mathf.Max(0, s_fightMusicRefCount - 1);
        }
    }

    /// <summary>
    /// Spawned at a world position. Initially inactive; after activationDelay it enables and deals damage
    /// to any player colliders inside its radius. Then it exists for lifetimeAfterActivation seconds and is destroyed.
    /// It can also be given a LineRenderer (warning beam). On activation it will change the beam color and
    /// keep the beam visible until the hitbox is destroyed. The hitbox now includes a visible sphere mesh
    /// so it is visible in Game view as well as Scene view.
    /// </summary>
    public class DelayedHitbox : MonoBehaviour
    {
        public float radius = 1.5f;
        public float activationDelay = 1.8f;
        public float lifetimeAfterActivation = 2.0f;
        public int damage = 2;
        public float knockbackForce = 6f;
        public float verticalImpulse = 2f;

        // Optional visual beam supplied by the spawner; will be recolored on activation and destroyed with the hitbox.
        [NonSerialized]
        public LineRenderer warningBeam;
        [NonSerialized]
        public Color beamActiveColor = Color.yellow;

        private SphereCollider _collider;
        private bool _active = false;

        // runtime visual for Game view
        private GameObject _visual;
        private MeshRenderer _visualRenderer;

        private void Awake()
        {
            // add a sphere collider set as trigger; we will enable it on activation
            _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
            _collider.radius = Mathf.Max(0.01f, radius);
            _collider.enabled = false;

            // Create a simple visible sphere (Game view and Scene view)
            _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _visual.name = "HitboxVisual";
            _visual.transform.SetParent(transform, false);
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localScale = Vector3.one * radius * 2f;
            // remove the primitive's collider (we already have our trigger)
            var primCol = _visual.GetComponent<Collider>();
            if (primCol != null) Destroy(primCol);

            _visualRenderer = _visual.GetComponent<MeshRenderer>();
            if (_visualRenderer != null)
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    _visualRenderer.material = new Material(spriteShader);
                }
                else
                {
                    _visualRenderer.material = new Material(Shader.Find("Standard"));
                }

                // initial warning color (semi-transparent). Try to source from warningBeam if available.
                Color warningColor = (warningBeam != null) ? warningBeam.startColor : new Color(1f, 0.8f, 0.2f, 0.25f);
                warningColor.a = 0.25f;
                if (_visualRenderer.material.HasProperty("_Color"))
                    _visualRenderer.material.SetColor("_Color", warningColor);
                else
                    _visualRenderer.material.color = warningColor;

                _visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _visualRenderer.receiveShadows = false;
            }
        }

        private void Start()
        {
            // schedule activation
            StartCoroutine(ActivationRoutine());
        }

        private System.Collections.IEnumerator ActivationRoutine()
        {
            // wait before enabling - gives the player a chance to move out
            yield return new WaitForSeconds(Mathf.Max(0f, activationDelay));

            // enable collider and immediately check overlaps (in case player is already inside)
            _active = true;
            if (_collider != null) _collider.enabled = true;

            // change beam color to active color if beam exists
            if (warningBeam != null)
            {
                try
                {
                    warningBeam.startColor = beamActiveColor;
                    warningBeam.endColor = beamActiveColor;
                    if (warningBeam.material != null)
                    {
                        if (warningBeam.material.HasProperty("_Color"))
                            warningBeam.material.SetColor("_Color", beamActiveColor);
                        else
                            warningBeam.material.color = beamActiveColor;
                    }
                }
                catch { /* ignore material/color issues in rare shader cases */ }
            }

            // Update visual color to active color
            if (_visualRenderer != null)
            {
                Color c = beamActiveColor;
                c.a = 0.40f;
                if (_visualRenderer.material.HasProperty("_Color"))
                    _visualRenderer.material.SetColor("_Color", c);
                else
                    _visualRenderer.material.color = c;

                _visual.transform.localScale = Vector3.one * radius * 2f;
            }

            // Check for any players already inside
            Collider[] inside = Physics.OverlapSphere(transform.position, _collider.radius);
            for (int i = 0; i < inside.Length; i++)
            {
                HandleHitCollider(inside[i]);
            }

            // keep active for lifetimeAfterActivation seconds
            yield return new WaitForSeconds(Mathf.Max(0f, lifetimeAfterActivation));

            // destroy attached beam if any
            if (warningBeam != null)
            {
                try
                {
                    if (warningBeam.gameObject != null)
                        Destroy(warningBeam.gameObject);
                }
                catch { }
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;
            HandleHitCollider(other);
        }

        private void HandleHitCollider(Collider other)
        {
            if (other == null) return;

            // Prefer tag "Player"
            if (!other.gameObject.CompareTag("Player")) return;

            var playerController = other.gameObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(damage);
            }

            // Try to apply knockback to player: prefer CharacterController, then Rigidbody, then transform nudge
            var cc = other.GetComponent<CharacterController>();
            Vector3 dir = (other.transform.position - transform.position).normalized;
            Vector3 horizontal = new Vector3(dir.x, 0f, dir.z).normalized * knockbackForce;
            Vector3 total = horizontal + Vector3.up * verticalImpulse;

            if (cc != null)
            {
                // move once to apply impulse (CharacterController handles collisions)
                cc.Move(total * Time.fixedDeltaTime);
            }
            else
            {
                var rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(total, ForceMode.VelocityChange);
                }
                else
                {
                    other.transform.position += total * 0.02f;
                }
            }

            // Optionally destroy hitbox immediately after hitting player once:
            // Destroy(gameObject);
        }

#if UNITY_EDITOR
        // draw the radius in scene view and in editor play-mode for convenience
        private void OnDrawGizmos()
        {
            Gizmos.color = _active ? new Color(1f, 0.1f, 0.1f, 0.35f) : new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawSphere(transform.position, radius);
        }
#endif
    }
}