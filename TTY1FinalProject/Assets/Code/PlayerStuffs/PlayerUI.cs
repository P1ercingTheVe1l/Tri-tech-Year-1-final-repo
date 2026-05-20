using UnityEngine;
using TMPro;

namespace TTY1
{

    public class PlayerUI : MonoBehaviour
    {
        public static PlayerUI Instance { get; private set; }

        [Header("References")]
        [Tooltip("Optional TMP text used to display current / max health (e.g. \"75 / 100\").")]
        public TextMeshProUGUI healthText;

        [Tooltip("Optional crosshair at screen center")]
        public UnityEngine.UI.Image crosshair;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

  
        public Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

  
        public void SetHealth(int current, int max)
        {
            if (healthText == null) return;
            healthText.text = $"{current} / {max}";
        }

    
        public void SetHealthFraction(float fraction)
        {
            if (healthText == null) return;
            fraction = Mathf.Clamp01(fraction);
            int percent = Mathf.RoundToInt(fraction * 100f);
            healthText.text = $"{percent}%";
        }
    }
}