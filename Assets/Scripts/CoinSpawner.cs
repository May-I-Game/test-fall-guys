using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private BoxCollider groundColider;

    private void Start()
    {
        CreateCoin();
    }

    public static Vector3 RandomPointInBounds(Bounds bounds)
    {
        return new Vector3(Random.Range(bounds.min.x, bounds.max.x), 1, Random.Range(bounds.min.z, bounds.max.z));

    }

    public void CreateCoin(int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 startPos = RandomPointInBounds(groundColider.bounds);
            Instantiate(coinPrefab, startPos, Quaternion.identity);
        }
    }
}
