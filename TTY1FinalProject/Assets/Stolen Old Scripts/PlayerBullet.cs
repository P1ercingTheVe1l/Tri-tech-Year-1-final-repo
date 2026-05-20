using UnityEngine;

namespace TTY1
{
    public class PlayerBullet : MonoBehaviour
    {
        [Tooltip("Adjust if your prefab faces the wrong way")]
        public Vector3 rotationOffset;

        public void Initialize(PlayerController player)
        {
            if (player == null) return;

            // Get player facing direction
            Vector3 forward = player.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
                return;

            forward.Normalize();

            // Rotate bullet to face that direction
            transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(rotationOffset);
        }
    }
}