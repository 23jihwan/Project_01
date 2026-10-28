using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCollision : MonoBehaviour
{
    public ScoreManager scoreManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. 쓰레기와 충돌했을 때 -> 게임 오버
        if (other.CompareTag("Trash"))
        {
            if (scoreManager != null)
            {
                scoreManager.GameOver(); // 보내주신 ScoreManager 호출
            }
        }
        // 2. 먹이(고기)와 충돌했을 때 -> 점수 획득 후 먹이 삭제
        else if (other.CompareTag("Food"))
        {
            if (scoreManager != null)
            {
                scoreManager.AddScore(1); // 보내주신 ScoreManager 호출
            }
            Destroy(other.gameObject);
        }
    }
}