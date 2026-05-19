using UnityEngine;
using UnityEngine.SceneManagement;
using AmesGame;

public class WinTrigger : MonoBehaviour
{
    [Header("Level Settings")]
    public string nextSceneName;

    [Header("Gate Settings")]
    public bool requireKey = true;
    public bool consumeKeyOnUse = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var keyHolder = other.GetComponent<KeyHolder>();

        if (requireKey)
        {
            if (keyHolder == null || !keyHolder.HasKey)
            {
                Debug.Log("Gate is locked! Defeat the boss first.");
                return;
            }
        }

        if (consumeKeyOnUse && keyHolder != null)
        {
            keyHolder.HasKey = false;

            var ui = FindObjectOfType<PlayerUI>();
            if (ui != null)
                ui.SetHasKey(false);
        }

        WinLevel();
    }

    private void WinLevel()
    {
        Debug.Log("Level Complete!");
        SceneManager.LoadScene(nextSceneName);
    }
}