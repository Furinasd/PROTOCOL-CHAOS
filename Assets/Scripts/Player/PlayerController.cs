using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public bool IsJumping { get; private set; }
    public bool IsDodging { get; private set; } // 下蹲或侧滑

    [Header("Movement Settings")]
    public float jumpForce = 7f;
    private Rigidbody rb;

    [Header("Dodge Settings")]
    public float dodgeDuration = 0.5f;
    private float dodgeTimer = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 跳跃 (Space) 规避下段横扫
        if (Input.GetKeyDown(KeyCode.Space) && !IsJumping && !IsDodging)
        {
            Jump();
        }

        // 下蹲/侧滑 (Shift) 规避上段重砸
        if (Input.GetKeyDown(KeyCode.LeftShift) && !IsJumping && !IsDodging)
        {
            StartDodge();
        }

        if (IsDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0f)
            {
                EndDodge();
            }
        }
    }

    private void Jump()
    {
        IsJumping = true;
        if (rb != null) rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void StartDodge()
    {
        IsDodging = true;
        dodgeTimer = dodgeDuration;
        // 缩小 Collider 高度以规避上段攻击
        transform.localScale = new Vector3(1, 0.5f, 1);
    }

    private void EndDodge()
    {
        IsDodging = false;
        transform.localScale = Vector3.one;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 简单的落地检测，实际项目中建议用射线检测
        if (collision.gameObject.CompareTag("Ground"))
        {
            IsJumping = false;
        }
    }
}
