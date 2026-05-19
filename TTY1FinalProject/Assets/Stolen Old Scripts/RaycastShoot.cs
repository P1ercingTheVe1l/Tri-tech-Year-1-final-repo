using AmesGame;
using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastShoot : MonoBehaviour
{
    public InputActionReference shootAction;

    public bool projectileShoot = true;
    public GameObject prefab;
    public Transform spawnPosition;
    public float shootSpeed = 60f;
    public float bulletLifetime = 10;

    [Tooltip("Minimum time between shots (seconds).")]
    public float shotCooldown = 0.2f;

    private float _cooldownMultiplier = 1f;
    private float _lastShotTime = -999f;

    [Tooltip("Damage dealt by this weapon / projectile")]
    public int damage = 1;

    public bool canShoot = true;

    // New event for perks to modify bullets
    public event System.Action<GameObject> OnBulletSpawned;

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
        shootAction.action.Enable();
    }

    private void OnDisable()
    {
        shootAction.action.Disable();
    }

    private void Start()
    {
        // prefer an existing AudioSource on the player object; otherwise add one
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        // play SFX as 2D by default so it's audible regardless of listener position
        _audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (!canShoot)
            return;

        if (!shootAction.action.IsPressed())
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

        RaycastHit hit;

        Vector2 screenPoint = Vector2.zero;
        var playerUI = FindObjectOfType<PlayerUI>();
        if (playerUI != null)
            screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Ray ray = Camera.main.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(ray, out hit, 10) && !projectileShoot)
        {
            var enemy = hit.collider?.GetComponent<EnemyController>();
            if (enemy != null)
                enemy.TakeDamage(damage);
            var enemyBoss = hit.collider?.GetComponent<EnemyBoss>();
            if (enemyBoss != null)
                enemyBoss.TakeDamage(damage);
        }
        else
        {
            Vector3 dest = hit.point;
            if (hit.collider == null)
                dest = Camera.main.transform.position + Camera.main.transform.forward * shootSpeed;

            GameObject bullet = Instantiate(prefab, spawnPosition.position, Quaternion.identity);

            var bulletScript = bullet.GetComponent<PlayerBullet>();
            if (bulletScript != null)
            {
                var playerController = GetComponent<PlayerController>();
                bulletScript.Initialize(playerController);
            }

            Vector3 velocity = (dest - spawnPosition.position).normalized;

            var rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = velocity * shootSpeed * bulletSpeedMultiplier;

            var pd = bullet.GetComponent<ProjectileDamage>() ?? bullet.AddComponent<ProjectileDamage>();
            pd.damage = damage;

            OnBulletSpawned?.Invoke(bullet);

            Destroy(bullet, bulletLifetime);
        }
    }

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