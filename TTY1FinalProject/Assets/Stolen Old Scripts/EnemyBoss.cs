using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TTY1
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBoss : EnemyController
    {
        [Header("Shooting")]
        [Tooltip("Optional projectile prefab. If not assigned the boss will perform an instant ray attack.")]
        public GameObject projectilePrefab;
        [Tooltip("Spawn transform for projectiles")]
        public Transform projectileSpawn;
        [Tooltip("Time between shots (seconds)")]
        public float shootCooldown = 2f;
        [Tooltip("Maximum distance at which the boss will try to attack the player")]
        public float shootRange = 15f;
        [Tooltip("Damage dealt by the boss' attack")]
        public int damage = 2;
        [Tooltip("Horizontal knockback force applied to the player on hit")]
        public float knockbackForce = 6f;
        [Tooltip("Optional vertical impulse applied to the player on hit")]
        public float verticalImpulse = 2f;

        private float _nextShootTime;

        protected override void Update()
        {
            base.Update();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > shootRange) return;

            if (Time.time >= _nextShootTime)
            {
                _nextShootTime = Time.time + shootCooldown;
                PerformShootAt(player);
            }
        }

        private void PerformShootAt(GameObject player)
        {
            if (projectilePrefab != null && projectileSpawn != null)
            {
                var proj = Instantiate(projectilePrefab, projectileSpawn.position, Quaternion.identity);
                var rb = proj.GetComponent<Rigidbody>();
                Vector3 dir = (player.transform.position - projectileSpawn.position).normalized;
                if (rb != null)
                {
                    rb.linearVelocity = dir * 12f;
                }

                var pd = proj.GetComponent<ProjectileDamage>() ?? proj.AddComponent<ProjectileDamage>();
                pd.damage = damage;

                // Attach knockback handler if not already present
                var knock = proj.GetComponent<ProjectileKnockback>();
                if (knock == null)
                {
                    var added = proj.AddComponent<ProjectileKnockback>();
                    added.Initialize(knockbackForce, verticalImpulse);
                }
            }
            else
            {
                // Instant hit (raycast) fallback
                Vector3 origin = transform.position + Vector3.up * 1.0f;
                Vector3 dir = (player.transform.position - origin).normalized;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, shootRange))
                {
                    if (hit.collider != null && hit.collider.gameObject == player)
                    {
                        ApplyDamageAndKnockback(player, dir);
                    }
                }
            }
        }

        private void ApplyDamageAndKnockback(GameObject player, Vector3 direction)
        {
            // Damage
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.TakeDamage(damage);

            // Try to apply knockback: prefer CharacterController.Move, then Rigidbody.AddForce, then transform nudge
            Vector3 horizontalImpulse = new Vector3(direction.x, 0f, direction.z).normalized * knockbackForce;
            Vector3 totalImpulse = horizontalImpulse + Vector3.up * verticalImpulse;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                // Move once to apply an immediate impulse. CharacterController handles collisions.
                cc.Move(totalImpulse * Time.fixedDeltaTime);
                return;
            }

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(totalImpulse, ForceMode.VelocityChange);
                return;
            }

            // Fallback: small transform position change
            player.transform.position += totalImpulse * 0.02f;
        }

        public override void TakeDamage(int dmg)
        {
            base.TakeDamage(dmg);
            // No perk behavior on boss damage; keep default health logic from EnemyController.
        }
    }

    // Auxiliary component used by instantiated projectiles to apply knockback on hit.
    public class ProjectileKnockback : MonoBehaviour
    {
        private float _knockbackForce;
        private float _verticalImpulse;
        private float _lifetime = 6f;

        public void Initialize(float knockbackForce, float verticalImpulse)
        {
            _knockbackForce = knockbackForce;
            _verticalImpulse = verticalImpulse;
            Destroy(gameObject, _lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var target = collision.gameObject;
            var pc = target.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Apply damage if ProjectileDamage exists
                var pd = GetComponent<ProjectileDamage>();
                if (pd != null)
                    pc.TakeDamage(pd.damage);

                // Apply knockback similar to EnemyBoss.ApplyDamageAndKnockback
                Vector3 dir = (target.transform.position - transform.position).normalized;
                Vector3 horizontal = new Vector3(dir.x, 0f, dir.z).normalized * _knockbackForce;
                Vector3 total = horizontal + Vector3.up * _verticalImpulse;

                var cc = target.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.Move(total * Time.fixedDeltaTime);
                }
                else
                {
                    var rb = target.GetComponent<Rigidbody>();
                    if (rb != null)
                        rb.AddForce(total, ForceMode.VelocityChange);
                    else
                        target.transform.position += total * 0.02f;
                }
            }

            // Destroy projectile on hit
            Destroy(gameObject);
        }
    }
}