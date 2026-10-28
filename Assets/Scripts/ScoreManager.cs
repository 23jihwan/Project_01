using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro 사용시 필수

public class ScoreManager : MonoBehaviour
{
    public int score;
    public GameObject resultPanel;
    public TMP_Text scoreText;
    public TMP_Text bestText;
    public TMP_Text hudText;
    public GameObject startPanel;
    public GameObject newBestText;

    void Start()
    {
        Time.timeScale = 0f;
        if (startPanel != null) startPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (newBestText != null) newBestText.SetActive(false);
    }

    public void StartGame()
    {
        if (startPanel != null) startPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void AddScore(int amount)
    {
        score = Mathf.Max(0, score + amount);
        if (hudText != null) hudText.text = score.ToString();
    }

    public void GameOver()
    {
        string key = "Best_" + SceneManager.GetActiveScene().name;
        int best = PlayerPrefs.GetInt(key, 0);

        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt(key, best);
            PlayerPrefs.Save();
            if (newBestText != null) newBestText.SetActive(true);
        }

        if (scoreText != null) scoreText.text = "SCORE " + score;
        if (bestText != null) bestText.text = "BEST " + best;
        if (resultPanel != null) resultPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}