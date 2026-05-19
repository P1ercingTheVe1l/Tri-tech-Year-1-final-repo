using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AmesGame
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

        [Header("Perk list")]
        [Tooltip("Optional. If empty the player's PerkController will be found at runtime.")]
        public PerkController perkController;

        [Tooltip("Author UI slots in the editor (drag Image/TMP fields). The script will populate these slots with the player's chosen perks.")]
        public List<PerkDisplaySlot> presetSlots = new List<PerkDisplaySlot>();

        [System.Serializable]
        public class PerkDisplaySlot
        {
            [Tooltip("Icon Image for the slot. If left empty the script will attempt to find an Image under the label's parent or a child named 'Elements'.")]
            public Image iconImage;
            [Tooltip("Label text for the perk name + key (use TMP_Text)")]
            public TextMeshProUGUI labelText;
        }

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

            // Hide any preset slot visuals until the player presses Escape
            ClearPerkList();

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

            // Pause enemy movement/shooting while pause menu is open
            EnemyController.AddUiPause();

            PopulatePerkList();
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

            // Restore enemy movement/shooting
            EnemyController.RemoveUiPause();

            ClearPerkList();
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

        public void PopulatePerkList()
        {
            // Only support preset authored slots now. If none provided, do nothing.
            if (presetSlots == null || presetSlots.Count == 0) return;

            // Ensure we reference the player's PerkController if none assigned
            if (perkController == null)
            {
                if (playerController != null)
                {
                    perkController = playerController.GetComponent<PerkController>();
                }

                if (perkController == null)
                {
                    // Use the newer API to find a PerkController instance in the scene
                    perkController = UnityEngine.Object.FindFirstObjectByType<PerkController>();
                }
            }

            // gather chosen perks
            List<PerkController.PerkSlot> chosen = new List<PerkController.PerkSlot>();
            if (perkController != null)
            {
                foreach (var slot in perkController.perkSlots)
                {
                    if (slot == null || slot.perk == null) continue;
                    if (!slot.chosen) continue;
                    chosen.Add(slot);
                }
            }

            // populate preset slots, hide image if there's no perk for the slot
            for (int i = 0; i < presetSlots.Count; i++)
            {
                var display = presetSlots[i];
                if (display == null) continue;

                bool hasPerk = i < chosen.Count;

                if (!hasPerk)
                {
                    if (display.iconImage != null)
                        display.iconImage.gameObject.SetActive(false);
                    if (display.labelText != null)
                        display.labelText.text = string.Empty;
                    continue;
                }

                var slot = chosen[i];
                string perkName = string.IsNullOrEmpty(slot.perk.perkName) ? slot.perk.name : slot.perk.perkName;
                string keyText = slot.mode == PerkMode.Active ? $" [{slot.activationKey}]" : " (Passive)";
                string labelText = $"{perkName}{keyText}";

                if (display.labelText != null)
                    display.labelText.text = labelText;

                // icon: if iconImage assigned in inspector use it; otherwise try to find under label's parent or a child named 'Elements'
                Image img = display.iconImage;
                if (img == null && display.labelText != null)
                {
                    var parent = display.labelText.transform.parent;
                    if (parent != null)
                    {
                        var elements = parent.Find("Elements");
                        if (elements != null)
                        {
                            img = elements.GetComponentInChildren<Image>(true);
                        }

                        if (img == null)
                        {
                            img = parent.GetComponentInChildren<Image>(true);
                        }
                    }
                }

                if (img != null)
                {
                    if (slot.perk.icon != null)
                    {
                        img.gameObject.SetActive(true);
                        img.sprite = slot.perk.icon;
                        img.color = Color.white;
                        img.enabled = true;
                    }
                    else
                    {
                        img.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void ClearPerkList()
        {
            // reset presetSlots: hide and clear text
            if (presetSlots != null)
            {
                foreach (var display in presetSlots)
                {
                    if (display == null) continue;
                    if (display.iconImage != null)
                        display.iconImage.gameObject.SetActive(false);
                    if (display.labelText != null)
                        display.labelText.text = string.Empty;
                }
            }
        }
    }
}