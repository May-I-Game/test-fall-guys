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
        // WSAD/ȭ��ǥ�� �о� XZ ��� �̵� ���� ����
        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        // ������ٵ� ���������� �̵�(�浹 ���)
        rb.MovePosition(rb.position + movement * playerSpeed * Time.deltaTime);
    }
}
