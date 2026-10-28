using UnityEngine;

public class FallingItem : MonoBehaviour
{
    public float baseSpeed = 4f;
    private float currentSpeed;

    public void Init(float speedBonus)
    {
        currentSpeed = baseSpeed + speedBonus;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        // 아래로 낙하 및 회전
        transform.Translate(Vector3.down * currentSpeed * Time.deltaTime, Space.World);
        transform.Rotate(0, 0, -90f * Time.deltaTime);

        // 화면 바닥 밖으로 떨어지면 파괴
        Camera mainCam = Camera.main;
        float bottomY = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0)).y - 1f;

        if (transform.position.y < bottomY)
        {
            Destroy(gameObject);
        }
    }
}