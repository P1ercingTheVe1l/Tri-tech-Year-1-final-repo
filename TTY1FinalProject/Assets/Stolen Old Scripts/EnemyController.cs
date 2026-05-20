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

        // Shooting
        public GameObject bulletPrefab;
        public Transform firePoint;
        public float fireRate = 2f;
        public float bulletSpeed = 15f;
        private float nextFireTime = 0f;

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
                    Shoot();
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
            if (bulletPrefab == null || firePoint == null || player == null) return;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

            Rigidbody rb = bullet.GetComponent<Rigidbody>();

            if (rb != null)
            {
                Vector3 direction = (player.transform.position - firePoint.position).normalized;
                rb.linearVelocity = direction * bulletSpeed;
            }

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
}