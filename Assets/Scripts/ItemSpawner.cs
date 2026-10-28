using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public GameObject[] trashPrefabs; // 쓰레기 프리팹들
    public GameObject[] foodPrefabs;  // 먹이(고기) 프리팹들
    public float spawnY = 6f;
    public float paddingX = 0.5f;

    private float timer = 0f;
    private float elapsedTime = 0f;
    private float minX, maxX;

    void Start()
    {
        Camera mainCam = Camera.main;
        float dist = transform.position.z - mainCam.transform.position.z;
        minX = mainCam.ViewportToWorldPoint(new Vector3(0, 0, dist)).x + paddingX;
        maxX = mainCam.ViewportToWorldPoint(new Vector3(1, 0, dist)).x - paddingX;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        elapsedTime += Time.deltaTime;
        timer += Time.deltaTime;

        // 시간이 지날수록 생성 간격이 짧아짐 (1.2초 -> 0.4초)
        float spawnInterval = Mathf.Max(0.4f, 1.2f - (elapsedTime * 0.015f));

        if (timer >= spawnInterval)
        {
            SpawnItem();
            timer = 0f;
        }
    }

    void SpawnItem()
    {
        float randomX = Random.Range(minX, maxX);
        Vector3 spawnPos = new Vector3(randomX, spawnY, 0f);

        // 70% 확률로 쓰레기, 30% 확률로 먹이 생성
        GameObject targetPrefab = null;
        if (Random.value < 0.7f && trashPrefabs.Length > 0)
        {
            int idx = Random.Range(0, trashPrefabs.Length);
            targetPrefab = trashPrefabs[idx];
        }
        else if (foodPrefabs.Length > 0)
        {
            int idx = Random.Range(0, foodPrefabs.Length);
            targetPrefab = foodPrefabs[idx];
        }

        if (targetPrefab != null)
        {
            GameObject obj = Instantiate(targetPrefab, spawnPos, Quaternion.identity);
            FallingItem item = obj.GetComponent<FallingItem>();
            if (item != null)
            {
                float speedBonus = elapsedTime * 0.05f;
                item.Init(speedBonus);
            }
        }
    }
}