using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject CubePrefab;
    [SerializeField] private BoxCollider groundColider;

    private void Start()
    {
        CreateCube();
    }

    public static Vector3 RandomPointInBounds(Bounds bounds)
    {
        // Ground�� ���� ������ ����
        return new Vector3(Random.Range(bounds.min.x, bounds.max.x), 1, Random.Range(bounds.min.z, bounds.max.z));

    }

    public void CreateCube(int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 startPos = RandomPointInBounds(groundColider.bounds);

            // �ش� ��ġ�� ������Ʈ ������ ����
            Instantiate(CubePrefab, startPos, Quaternion.identity);
        }
    }
}
