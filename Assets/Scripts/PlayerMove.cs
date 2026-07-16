using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Tooltip("Movement speed in units per second.")]
    public float speed = 5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S
        Vector3 move = new Vector3(h, 0f, v).normalized;
        rb.MovePosition(rb.position + move * speed * Time.fixedDeltaTime);
    }
}
