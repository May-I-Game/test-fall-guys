using UnityEngine;
using UnityEngine.EventSystems;

public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject CubePrefab;
    [SerializeField] private BoxCollider groundColider;

    private void Start()
    {
        CreateCube(5);
    }

    public static Vector3 RandomPointInBounds(Bounds bounds)
    {
        // Ground의 범위 내에서 스폰
        return new Vector3(Random.Range(bounds.min.x, bounds.max.x), 1, Random.Range(bounds.min.z, bounds.max.z));
    }

    public void CreateCube(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 startPos = RandomPointInBounds(groundColider.bounds);

            // 해당 위치에 오브젝트 프리팹 생성
            Instantiate(CubePrefab, startPos, Quaternion.identity);
        }
    }
}
