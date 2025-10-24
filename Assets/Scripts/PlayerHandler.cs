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
        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        rb.MovePosition(rb.position + movement * playerSpeed * Time.deltaTime);
    }
}
