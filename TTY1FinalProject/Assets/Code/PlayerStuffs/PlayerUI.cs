using UnityEngine;
using UnityEngine.UI;

namespace TTY1
{

    public class PlayerUI : MonoBehaviour
    {
        public static PlayerUI Instance { get; private set; }

        [Header("References")]
        public Image healthBar;         
        public Image crosshair;         

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

      
        public void SetHealthFraction(float fraction)
        {
            if (healthBar == null) return;
            healthBar.fillAmount = Mathf.Clamp01(fraction);
        }
    }
}