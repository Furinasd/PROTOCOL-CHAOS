using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class DemoPlayerController : MonoBehaviour
{
    // 兼容原版的属性，供 PlayerCombatReceiver 判定
    public bool IsJumping => currentState == PlayerState.Jumping || (velocity.y > 0 && !cc.isGrounded);
    public bool IsDodging => currentState == PlayerState.Dashing;

    [Header("🎯 状态系统 (FSM)")]

    [Header("🏃 基础移动 (Movement)")]
    public float moveSpeed = 8f;
    public float smoothRotationTime = 0.1f;
    private float currentVelocity;
    
    [Header("🦘 极致跳跃手感 (Advanced Jump)")]
    public float jumpHeight = 2.5f;
    public float gravity = -25f;
    public float fallMultiplier = 2.0f; // 下落时重力加倍，摆脱"气球感"
    
    // ACT 手感核心：容错机制
    private float coyoteTime = 0.15f;    
    private float coyoteTimeCounter;
    private float jumpBufferTime = 0.15f; 
    private float jumpBufferCounter;

    [Header("💨 空间规避 (Dash)")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f; // 你在原版也是类似于0.2~0.5s的判定时间
    public float dashCooldown = 0.5f;
    private float lastDashTime = -10f;

    // 底层组件
    private CharacterController cc;
    private Vector3 velocity; // 垂直方向的速度累加
    private Transform cam;    // 必须关联主相机，实现视角的绝对相对移动

    // 状态机枚举
    public enum PlayerState { Normal, Jumping, Dashing, Hitlag }
    public PlayerState currentState = PlayerState.Normal;
    private PlayerState previousStateBeforeHitlag = PlayerState.Normal;
    private Coroutine hitlagCoroutine;

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 核心状态机路由
        switch (currentState)
        {
            case PlayerState.Normal:
            case PlayerState.Jumping:
                HandleMovement();       // 位移与朝向
                HandleJump();           // 跳跃与重力
                HandleDash();           // 冲刺/侧滑
                break;
            case PlayerState.Dashing:
                // Dash 期间剥夺控制权，仅执行 Dash 位移
                break;
            case PlayerState.Hitlag:
                // 顿帧或硬直期间，冻结移动，仅应用重力
                ApplyGravityOnly();
                break;
        }
    }

    #region 移动与 3C 手感 (Movement & Gravity)
    private void HandleMovement()
    {
        float horizontal = Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue();
        float vertical = Keyboard.current.wKey.ReadValue() - Keyboard.current.sKey.ReadValue();
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            // 基于相机视角的移动转向
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (cam != null) targetAngle += cam.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentVelocity, smoothRotationTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            cc.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        // 土狼时间 (Coyote Time)：如果刚离开地面，依然给玩家保留短暂的跳跃权利
        if (cc.isGrounded) { coyoteTimeCounter = coyoteTime; }
        else { coyoteTimeCounter -= Time.deltaTime; }

        // 跳跃缓存 (Jump Buffer)：提前按下空格，落地瞬间自动起跳
        if (Keyboard.current.spaceKey.wasPressedThisFrame) { jumpBufferCounter = jumpBufferTime; }
        else { jumpBufferCounter -= Time.deltaTime; }

        // 垂直速度重置
        if (cc.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // 贴地力，防止下坡起飞
            if (currentState == PlayerState.Jumping) currentState = PlayerState.Normal;
        }

        // 触发跳跃
        if (coyoteTimeCounter > 0f && jumpBufferCounter > 0f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            currentState = PlayerState.Jumping;
        }

        // Mario 下落曲线：如果正在下落，或者提前松开跳跃键，重力加倍 (手感极其干净利落)
        float currentGravity = gravity;
        if (velocity.y < 0 || (velocity.y > 0 && !Keyboard.current.spaceKey.isPressed))
        {
            currentGravity *= fallMultiplier; 
        }

        velocity.y += currentGravity * Time.deltaTime;
        cc.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime); // 单独执行垂直位移
    }

    private void ApplyGravityOnly()
    {
        if (cc.isGrounded && velocity.y < 0) { velocity.y = -2f; }
        velocity.y += gravity * fallMultiplier * Time.deltaTime;
        cc.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }
    #endregion

    #region 空间规避与状态控制 (Dash & States)
    private void HandleDash()
    {
        // 左 Shift 键触发空间规避
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashRoutine());
        }
    }

    private IEnumerator DashRoutine()
    {
        currentState = PlayerState.Dashing;
        lastDashTime = Time.time;
        velocity.y = 0f; // 冲刺期间不受重力影响

        // 调整视觉缩放作为反馈
        transform.localScale = new Vector3(1f, 0.5f, 1f);

        Vector3 dashDir = transform.forward; // 默认向前冲刺
        // 如果有输入，则朝输入方向冲刺
        float h = Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue();
        float v = Keyboard.current.wKey.ReadValue() - Keyboard.current.sKey.ReadValue();
        if (h != 0 || v != 0)
        {
             dashDir = (Quaternion.Euler(0f, cam != null ? cam.eulerAngles.y : 0f, 0f) * new Vector3(h, 0, v)).normalized;
             transform.rotation = Quaternion.LookRotation(dashDir);
        }

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            cc.Move(dashDir * dashSpeed * Time.deltaTime);
            yield return null;
        }

        // 恢复视觉缩放
        transform.localScale = Vector3.one;
        currentState = PlayerState.Normal;
    }

    // 提供给外部(如被怪物击中或触发完美格挡)的接口
    public void EnterHitlag(float duration)
    {
        if (gameObject.activeInHierarchy)
        {
            if (hitlagCoroutine != null) StopCoroutine(hitlagCoroutine);
            hitlagCoroutine = StartCoroutine(HitlagRoutine(duration));
        }
    }

    private IEnumerator HitlagRoutine(float duration)
    {
        // 只有非 Hitlag 状态进入时才记录之前的状态，防止连续受击导致的逻辑回退错误
        if (currentState != PlayerState.Hitlag)
        {
            previousStateBeforeHitlag = currentState;
        }
        
        currentState = PlayerState.Hitlag;
        
        // 这里配合 Time.timeScale 做出极其夸张的顿帧停顿感
        yield return new WaitForSecondsRealtime(duration); 
        
        // 恢复之前的状态
        currentState = previousStateBeforeHitlag;
        hitlagCoroutine = null;
    }
    #endregion
}
