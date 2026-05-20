using UnityEngine;
using UnityEngine.InputSystem;

namespace TTY1
{
    public class RaycastShoot : MonoBehaviour
    {
        public InputActionReference shootAction;

        public bool projectileShoot = true;
        public GameObject prefab;
        public Transform spawnPosition;
        public float shootSpeed = 60f;
        public float bulletLifetime = 10f;

        [Tooltip("Minimum time between shots (seconds).")]
        public float shotCooldown = 0.2f;

        private float _cooldownMultiplier = 1f;
        private float _lastShotTime = -999f;

        [Tooltip("Damage dealt by this weapon / projectile")]
        public int damage = 1;

        public bool canShoot = true;

        public float bulletSpeedMultiplier = 1f;

        // -- Shoot SFX --
        [Header("Audio")]
        [Tooltip("Optional one-shot shoot SFX played when firing")]
        public AudioClip shootSfx;
        [Range(0f, 1f)]
        public float shootSfxVolume = 1f;
        private AudioSource _audioSource;

        private void OnEnable()
        {
            if (shootAction != null && shootAction.action != null)
                shootAction.action.Enable();
        }

        private void OnDisable()
        {
            if (shootAction != null && shootAction.action != null)
                shootAction.action.Disable();
        }

        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }

            _audioSource.spatialBlend = 0f;
        }

        private void Update()
        {
            if (!canShoot)
                return;

            if (shootAction == null || shootAction.action == null || !shootAction.action.IsPressed())
                return;

            float cooldown = shotCooldown * _cooldownMultiplier;

            if (Time.time >= _lastShotTime + cooldown)
            {
                _lastShotTime = Time.time;
                ShootOnce();
            }
        }

        private void ShootOnce()
        {
            if (shootSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(shootSfx, Mathf.Clamp01(shootSfxVolume));
            }
            else if (shootSfx != null)
            {
                AudioSource.PlayClipAtPoint(shootSfx, transform.position, Mathf.Clamp01(shootSfxVolume));
            }

            if (spawnPosition == null)
            {
                Debug.LogWarning("RaycastShoot: spawnPosition not assigned.");
                return;
            }

            // Use weapon/spawn forward for both instant hits and spawned projectiles.
            Ray ray = new Ray(spawnPosition.position, spawnPosition.forward);
            const float rayDistance = 100f;
            RaycastHit hit;

            // Instant (ray) damage when configured
            if (!projectileShoot)
            {
                if (Physics.Raycast(ray, out hit, rayDistance))
                {
                    var enemy = hit.collider?.GetComponent<EnemyController>() ?? hit.collider?.GetComponentInParent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(damage);
                        return;
                    }

                    var enemyBoss = hit.collider?.GetComponent<EnemyBoss>() ?? hit.collider?.GetComponentInParent<EnemyBoss>();
                    if (enemyBoss != null)
                    {
                        enemyBoss.TakeDamage(damage);
                        return;
                    }
                }

                // no projectile to spawn in this mode
                return;
            }

            // Projectile mode: always fire forward from spawnPosition (no crosshair/aim point)
            if (prefab == null)
            {
                Debug.LogWarning("RaycastShoot: prefab not assigned.");
                return;
            }

            GameObject bullet = Instantiate(prefab, spawnPosition.position, spawnPosition.rotation);

            var bulletScript = bullet.GetComponent<PlayerBullet>();
            if (bulletScript != null)
            {
                var playerController = GetComponent<PlayerController>();
                bulletScript.Initialize(playerController);
            }

            var rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = spawnPosition.forward * shootSpeed * bulletSpeedMultiplier;

            var pd = bullet.GetComponent<ProjectileDamage>() ?? bullet.AddComponent<ProjectileDamage>();
            pd.damage = damage;

            Destroy(bullet, bulletLifetime);
        }

        // Exposed helper to modify cooldown temporarily
        public void AddCooldownMultiplier(float multiplier, float seconds)
        {
            StartCoroutine(CooldownMultiplierRoutine(multiplier, seconds));
        }

        private System.Collections.IEnumerator CooldownMultiplierRoutine(float multiplier, float seconds)
        {
            _cooldownMultiplier *= multiplier;
            yield return new WaitForSeconds(seconds);
            _cooldownMultiplier /= multiplier;
        }
    }
}