using UnityEngine;
using UnityEngine.UI;

public class CubeSpawner : MonoBehaviour
{
    [Header("Prefabs & UI")]
    [SerializeField] private GameObject CubePrefab;    // 로컬이 만드는 큐브(태그는 Prefab에서 "Cube")
    [SerializeField] private GameObject OthersPrefab;  // 원격 '플레이어'는 Net에서 이걸로 생성
    [SerializeField] private Text objNumText;

    [Header("Random Spawn (Optional)")]
    [SerializeField] private BoxCollider groundColider;
    [SerializeField] private int spawnOnStart = 0; // 시작 시 랜덤 스폰(테스트용)

    private int totalCount = 0;

    private void Start()
    {
        if (spawnOnStart > 0 && groundColider && CubePrefab)
            CreateCube(spawnOnStart);
    }

    public static Vector3 RandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            1f,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    /// <summary>
    /// 버튼에서 호출: 로컬이 직접 생성하는 큐브들
    /// </summary>
    public void CreateCube(int count)
    {
        if (!CubePrefab)
        {
            Debug.LogWarning("[CubeSpawner] CubePrefab 미지정");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var startPos = groundColider ? RandomPointInBounds(groundColider.bounds) : Vector3.zero;

            // Prefab의 태그가 "Cube"여야 Net에서 자동 송신 대상이 됩니다.
            Instantiate(CubePrefab, startPos, Quaternion.identity);

            CountUp();
        }
    }

    // ===== Net.cs에서 호출하는 네트워크 스폰 API =====

    public GameObject SpawnNetworkOthers(Vector3 pos, Quaternion rot, string nameOverride)
    {
        GameObject go;
        if (!OthersPrefab)
        {
            Debug.LogWarning("[CubeSpawner] OthersPrefab 미지정, 기본 큐브로 대체합니다.");
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            go = Instantiate(OthersPrefab, pos, rot);
        }

        if (!string.IsNullOrEmpty(nameOverride))
            go.name = nameOverride;

        // 원격은 송신 대상에서 제외하려고 태그 제거 (Net에서 관리)
        go.tag = "Untagged";

        CountUp();
        return go;
    }

    public GameObject SpawnNetworkCube(Vector3 pos, Quaternion rot, string nameOverride)
    {
        GameObject go;
        if (!CubePrefab)
        {
            Debug.LogWarning("[CubeSpawner] CubePrefab 미지정, 기본 큐브로 대체합니다.");
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            go = Instantiate(CubePrefab, pos, rot);
        }

        if (!string.IsNullOrEmpty(nameOverride))
            go.name = nameOverride;

        // 원격은 송신 제외
        go.tag = "Untagged";

        CountUp();
        return go;
    }

    // ===== 카운터/UI =====
    private void CountUp()
    {
        totalCount++;
        if (objNumText) objNumText.text = "Object: " + totalCount;
    }
}
