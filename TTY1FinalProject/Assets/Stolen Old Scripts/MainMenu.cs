using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string playSceneName;
    [SerializeField] private string creditsSceneName;
    [SerializeField] private string playHowToPlay;


    // Play button
    public void PlayGame()
    {
        if (!string.IsNullOrEmpty(playSceneName))
        {
            SceneManager.LoadScene(playSceneName);
        }
        else
        {
            Debug.LogWarning("Play scene not assigned!");
        }
    }

    // Credits button
    public void ShowCredits()
    {
        if (!string.IsNullOrEmpty(creditsSceneName))
        {
            SceneManager.LoadScene(creditsSceneName);
        }
        else
        {
            Debug.LogWarning("Credits scene not assigned!");
        }
    }

    // Quit button
    public void QuitGame()
    {
        Debug.Log("Quit Game");

        Application.Quit();
    }
    public void HowToPlay()
    {
        if (!string.IsNullOrEmpty(playHowToPlay))
        {
            SceneManager.LoadScene(playHowToPlay);
        }
        else
        {
            Debug.LogWarning("How To Play scene not assigned!");
        }
    }
}