using UnityEngine;

public class Jump : MonoBehaviour
{
    [SerializeField]
    public float jumForce = 1;
    private Rigidbody rb;

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
        Vector3 v = rb.linearVelocity;
        v.x = 0f;
        v.y = 0f;

        rb.linearVelocity = v;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(Vector3.up * jumForce, ForceMode.Impulse);
        isJumping = true;
    }
}
