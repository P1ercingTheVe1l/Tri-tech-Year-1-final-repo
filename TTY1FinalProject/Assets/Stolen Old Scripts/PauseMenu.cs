using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace TTY1
{
    public class PauseMenu : MonoBehaviour
    {
        [Header("UI")]
        public GameObject pausePanel;
        public Button resumeButton;
        public Button mainMenuButton;

        [Header("Pause UI Text (optional)")]
        [Tooltip("Optional pause title text to show only when paused")]
        public TextMeshProUGUI pauseTitleText;
        [Tooltip("Legacy UI Text fallback if not using TMP")]
        public Text pauseTitleLegacy;

        [Header("Player Rotation")]
        [Tooltip("If assigned this MonoBehaviour will be disabled while paused to stop rotation (e.g. a look script).")]
        public MonoBehaviour playerLook;
        [Tooltip("Alternative: assign PlayerController to disable when paused (optional).")]
        public PlayerController playerController;

        [Header("Main Menu")]
        public string mainMenuSceneName = "MainMenu";

        private bool _isPaused = false;

        private void Awake()
        {
            // Ensure pause UI container is hidden at runtime start
            if (pausePanel != null)
                pausePanel.SetActive(false);

            // Hide buttons and title until pause is opened with Escape
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(false);

            if (mainMenuButton != null)
                mainMenuButton.gameObject.SetActive(false);

            if (pauseTitleText != null)
                pauseTitleText.gameObject.SetActive(false);

            if (pauseTitleLegacy != null)
                pauseTitleLegacy.gameObject.SetActive(false);

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Resume);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(OnMainMenuPressed);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isPaused) Resume();
                else Pause();
            }
        }

        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;

            if (pausePanel != null) pausePanel.SetActive(true);

            // show buttons and title when opening pause UI
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(true);

            if (mainMenuButton != null)
                mainMenuButton.gameObject.SetActive(true);

            if (pauseTitleText != null)
                pauseTitleText.gameObject.SetActive(true);

            if (pauseTitleLegacy != null)
                pauseTitleLegacy.gameObject.SetActive(true);

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerLook != null) playerLook.enabled = false;
            if (playerController != null) playerController.enabled = false;
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;

            if (pausePanel != null) pausePanel.SetActive(false);

            // hide buttons and title again
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(false);

            if (mainMenuButton != null)
                mainMenuButton.gameObject.SetActive(false);

            if (pauseTitleText != null)
                pauseTitleText.gameObject.SetActive(false);

            if (pauseTitleLegacy != null)
                pauseTitleLegacy.gameObject.SetActive(false);

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerLook != null) playerLook.enabled = true;
            if (playerController != null) playerController.enabled = true;
        }

        private void OnMainMenuPressed()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!string.IsNullOrEmpty(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
            else
                Debug.LogWarning("PauseMenu: mainMenuSceneName not set.");
        }
    }
}