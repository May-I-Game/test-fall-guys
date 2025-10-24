using UnityEngine;

public class Jump : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb;

    public float jumForce = 1;
    public LayerMask groundLayer = 0;
    public float groundCheckDistance = 0.1f;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isJumping;
    

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        wasGrounded = isGrounded;

        // ������ ����ĳ���� �˻�
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);

        // ���� ���� ������ ����
        if (isGrounded && !wasGrounded)
            isJumping = false;

        // �÷��̾�� ����
        if (CompareTag("Player"))
        {
            if (!isJumping && isGrounded && Input.GetButtonDown("Jump"))
                JumpUp();

            return;
        }

        // ���� ť���� ����
        if (!isJumping && isGrounded)
            JumpUp();

        if (isGrounded)
            isJumping = false;
    }

    private void JumpUp()
    {
        // x,y,z �ӵ�, ���� �ʱ�ȭ�Ͽ� ������Ʈ ����
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(Vector3.up * jumForce, ForceMode.Impulse);
        isJumping = true;
    }
}
