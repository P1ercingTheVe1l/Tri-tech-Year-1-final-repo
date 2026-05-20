using UnityEngine;
using UnityEngine.SceneManagement;

namespace TTY1
{
    public class WinTrigger : MonoBehaviour
    {
        [Header("Level Settings")]
        public string nextSceneName;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("WinTrigger: nextSceneName is not set.");
                return;
            }

            Debug.Log("Level Complete!");
            SceneManager.LoadScene(nextSceneName);
        }
    }
}