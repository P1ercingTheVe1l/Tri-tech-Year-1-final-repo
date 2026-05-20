using UnityEngine;
using UnityEngine.SceneManagement;

namespace TTY1
{
    public class DeathScreenUI : MonoBehaviour
    {
        public GameObject deathScreenUI;

        void Start()
        {
            if (deathScreenUI != null)
                deathScreenUI.SetActive(false);
        }

        public void ShowDeathScreen()
        {
            if (deathScreenUI != null)
            {
                deathScreenUI.SetActive(true);
                Time.timeScale = 0f;
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}