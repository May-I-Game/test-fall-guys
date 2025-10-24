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

        // 땅으로 레이캐스팅 검사
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);

        // 착지 순간 점프가 끝남
        if (isGrounded && !wasGrounded)
            isJumping = false;

        // 플레이어용 점프
        if (CompareTag("Player"))
        {
            if (!isJumping && isGrounded && Input.GetButtonDown("Jump"))
                JumpUp();

            return;
        }

        // 랜덤 큐브들용 점프
        if (!isJumping && isGrounded)
            JumpUp();

        if (isGrounded)
            isJumping = false;
    }

    private void JumpUp()
    {
        // x,y,z 속도, 각도 초기화하여 오브젝트 안정
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(Vector3.up * jumForce, ForceMode.Impulse);
        isJumping = true;
    }
}
