using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace AmesGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBoss : EnemyController
    {
        [Header("Boss Reward")]
        public bool givesKeyOnDeath = true;

        [Header("Perk Toggles")]
        public bool hasSlam = false;
        public bool hasHandCannon = false;
        public bool hasForceField = false;

        [Header("Slam Settings")]
        public float slamRadius = 4f;
        public int slamDamage = 2;
        public float slamKnockback = 6f;
        public float slamCooldown = 6f;

        [Header("HandCannon Settings")]
        public GameObject handCannonProjectile;
        public Transform handCannonSpawn;
        public float handCannonProjectileSpeed = 12f;
        public float handCannonCooldown = 2f;
        public float handCannonRecoil = 3f;

        [Header("ForceField Settings")]
        public float forceFieldDuration = 3f;
        public float forceFieldCooldown = 10f;

        private float slamNext = 0f;
        private float handNext = 0f;
        private float fieldNext = 0f;

        private bool forceActive = false;

        protected override void Update()
        {
            base.Update();

            var player = GameObject.FindGameObjectWithTag("Player");
            float dist = player != null ? Vector3.Distance(transform.position, player.transform.position) : float.MaxValue;

            if (hasSlam && Time.time >= slamNext && dist <= 6f)
            {
                StartCoroutine(PerformSlam());
                slamNext = Time.time + slamCooldown;
            }

            if (hasHandCannon && Time.time >= handNext && dist <= 12f)
            {
                if (player != null)
                {
                    FireHandCannonAt(player.transform.position);
                    handNext = Time.time + handCannonCooldown;
                }
            }

            if (hasForceField && Time.time >= fieldNext)
            {
                StartCoroutine(ActivateForceField());
                fieldNext = Time.time + forceFieldCooldown;
            }
        }

        private IEnumerator PerformSlam()
        {
            yield return new WaitForSeconds(0.25f);

            Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius);

            foreach (var c in hits)
            {
                var p = c.GetComponentInParent<PlayerController>();
                if (p != null)
                {
                    p.TakeDamage(slamDamage);
                }
            }
        }

        private void FireHandCannonAt(Vector3 target)
        {
            if (handCannonProjectile == null || handCannonSpawn == null) return;

            var proj = Instantiate(handCannonProjectile, handCannonSpawn.position, handCannonSpawn.rotation);

            var rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (target - handCannonSpawn.position).normalized;
                rb.linearVelocity = dir * handCannonProjectileSpeed;
            }

            Destroy(proj, 6f);
        }

        private IEnumerator ActivateForceField()
        {
            forceActive = true;
            yield return new WaitForSeconds(forceFieldDuration);
            forceActive = false;
        }

        public override void TakeDamage(int damage)
        {
            if (forceActive) return;

            float prev = health;
            base.TakeDamage(damage);
            float after = health;

            // GIVE KEY WHEN BOSS DIES
            if (after <= 0 && givesKeyOnDeath)
            {
                GivePlayerKey();
            }
        }

        private void GivePlayerKey()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var keyHolder = player.GetComponent<KeyHolder>();
            if (keyHolder == null)
                keyHolder = player.AddComponent<KeyHolder>();

            keyHolder.HasKey = true;

            var ui = FindObjectOfType<PlayerUI>();
            if (ui != null)
                ui.SetHasKey(true);
        }
    }
}