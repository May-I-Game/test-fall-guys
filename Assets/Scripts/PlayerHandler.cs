using UnityEngine;

public class PlayerHandler : MonoBehaviour
{
    Rigidbody rb;
    [SerializeField] private float playerSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // WSAD/화살표을 읽어 XZ 평면 이동 벡터 만듦
        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        // 리지드바디를 물리적으로 이동(충돌 고려)
        rb.MovePosition(rb.position + movement * playerSpeed * Time.deltaTime);
    }
}
