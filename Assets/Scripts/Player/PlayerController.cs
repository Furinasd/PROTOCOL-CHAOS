using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // 兼容原版的属性，供 PlayerCombatReceiver 判定
    public bool IsJumping => currentState == PlayerState.Jumping || (!cc.isGrounded && velocity.y != 0); // 只要不在地面且有位移，就视为广义跳跃中
    public bool IsDodging => currentState == PlayerState.Dashing;

    [Header("🎯 状态系统 (FSM)")]
    public Transform visualRoot; // 视觉表现根节点，用于不影响物理的抖动与位移

    public enum InputType { Dash, SwitchPolarity, Parry, Execute }
    private struct BufferedInput
    {
        public InputType type;
        public float timestamp;
    }
    private List<BufferedInput> inputBuffer = new List<BufferedInput>();
    private float inputBufferDuration = 0.2f; // 缓冲指令有效时长

    [Header("🏃 基础移动 (Movement)")]
    public float moveSpeed = 8f;
    private float currentMoveSpeedMultiplier = 1f; // 用于吸收伤害时的减速惩罚
    [HideInInspector] public float environmentalSpeedMultiplier = 1f; // 用于环境（如Puddle）的持续减速
    public float smoothRotationTime = 0.1f;
    private float currentVelocity;
    
    [Header("🦘 极致跳跃手感 (Advanced Jump)")]
    public float jumpHeight = 2.5f;
    public float gravity = -25f;
    public float fallMultiplier = 2.0f; // 下落时重力加倍，摆脱"气球感"
    private float lastJumpTime = -10f;
    public float LastJumpTime => lastJumpTime;
    
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
    public float LastDashTime => lastDashTime; // 暴露给战斗核心用于完美闪避判定

    // 底层组件
    private CharacterController cc;
    private Vector3 velocity; // 垂直方向的速度累加
    private Transform cam;    // 必须关联主相机，实现视角的绝对相对移动

    // 状态机枚举
    public enum PlayerState { Normal, Jumping, Dashing, Hitlag }
    private PlayerState currentState = PlayerState.Normal;
    public PlayerState GetState() => currentState;

    // ---------------------------------------------------------
    // 【环境系统】：维护当前所在的污染区列表，确保叠层逻辑正确
    // ---------------------------------------------------------
    private HashSet<ChaosPuddle> puddlesInside = new HashSet<ChaosPuddle>();
    public void RegisterPuddle(ChaosPuddle p) { 
        if (!puddlesInside.Contains(p)) puddlesInside.Add(p); 
        UpdateEnvironmentalConditions();
    }
    public void UnregisterPuddle(ChaosPuddle p) { 
        if (puddlesInside.Contains(p)) puddlesInside.Remove(p); 
        UpdateEnvironmentalConditions();
    }
    private void UpdateEnvironmentalConditions() {
        // 只要处在至少一个污染区，就维持减速。所有人离开后归 1。
        environmentalSpeedMultiplier = (puddlesInside.Count > 0) ? 0.5f : 1.0f;

        // 【新规】：同步给能量系统计时器
        PlayerEnergySystem energy = GetComponent<PlayerEnergySystem>();
        if (energy != null)
        {
            energy.SetInPuddle(puddlesInside.Count > 0);
        }
    }
    private PlayerState previousStateBeforeHitlag = PlayerState.Normal;
    private Coroutine hitlagCoroutine;

    // 击退系统变量
    private Vector3 externalImpact; // 外部冲击力
    public float knockbackResistance = 5f; // 阻力，数值越大停止越快

    [Header("🛡️ 软边界控制 (Arena Boundaries)")]
    public float arenaRadius = 20f;
    private bool isProcessingOutOfBounds = false;

    private void Start()
    {
        // 【新规：隐藏与锁定鼠标】
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        cc = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 【新规：软边界检测】
        CheckArenaBoundaries();

        // 【新规：重力与掉落检测】
        if (transform.position.y < -15f)
        {
            TriggerFallDeath();
            return;
        }

        // 核心状态机路由
        switch (currentState)
        {
            case PlayerState.Normal:
            case PlayerState.Jumping:
                CaptureInputs();        // 录入所有指令到缓冲队列
                ProcessInputBuffer();   // 尝试消耗并执行缓冲指令
                HandleMovement();       // 位移与朝向
                HandleJump();           // 跳跃与重力
                // HandleDash 逻辑已拆分至 ProcessInputBuffer
                break;
            case PlayerState.Dashing:
                CaptureInputs();        // 在 Dash 期间也录入指令，实现“预输入”
                break;
            case PlayerState.Hitlag:
                CaptureInputs();        // 在受击硬直期间录入，恢复瞬间反击
                ApplyGravityOnly();
                break;
        }

        // 在 Normal 状态下也需要应用外部力 (如推开)
        if (currentState != PlayerState.Hitlag && externalImpact.magnitude > 0.2f)
        {
            cc.Move(externalImpact * Time.deltaTime);
            externalImpact = Vector3.Lerp(externalImpact, Vector3.zero, knockbackResistance * Time.deltaTime);
        }
    }

    private void CheckArenaBoundaries()
    {
        // 仅检测水平距离 (X-Z)
        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        if (flatPos.magnitude > arenaRadius && !isProcessingOutOfBounds)
        {
            StartCoroutine(TriggerOutOfBoundsPunishment());
        }
    }

    private IEnumerator TriggerOutOfBoundsPunishment()
    {
        isProcessingOutOfBounds = true;
        Debug.Log("<color=red>⚠️ [Boundary] 极性紊乱！警告：您已脱离秩序核心区域！重开中...</color>");
        
        // 视觉反馈：强抖动 + 视角冲击
        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.TriggerDamageFeedback();

        // 强制进入顿帧状态增强死亡感
        EnterHitlag(0.4f);
        
        yield return new WaitForSecondsRealtime(0.5f);
        
        TriggerFallDeath();
    }

    private void TriggerFallDeath()
    {
        PlayerCombatReceiver receiver = GetComponent<PlayerCombatReceiver>();
        if (receiver != null) receiver.Die();
        else ResetLevel(); // Fallback
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
            cc.Move(moveDir * moveSpeed * currentMoveSpeedMultiplier * environmentalSpeedMultiplier * Time.deltaTime);
        }
    }

    /// <summary>
    /// 提供给外部的临时减速接口 (如同色吸收时的惩罚)
    /// </summary>
    public void ApplySlowdown(float duration, float multiplier)
    {
        StopCoroutine("SlowdownRoutine");
        StartCoroutine(SlowdownRoutine(duration, multiplier));
    }

    private IEnumerator SlowdownRoutine(float duration, float multiplier)
    {
        currentMoveSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        currentMoveSpeedMultiplier = 1f;
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
            lastJumpTime = Time.time;
            currentState = PlayerState.Jumping;
            Debug.Log($"<color=cyan>🦘 [Action] Player Jumped at {lastJumpTime:F2}</color>");
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
        
        // 应用外部冲击力 (Impact)
        if (externalImpact.magnitude > 0.2f)
        {
            cc.Move(externalImpact * Time.deltaTime);
            externalImpact = Vector3.Lerp(externalImpact, Vector3.zero, knockbackResistance * Time.deltaTime);
        }

        cc.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }

    /// <summary>
    /// 核心：应用外部击退力
    /// </summary>
    public void AddKnockback(Vector3 direction, float force)
    {
        direction.Normalize();
        if (direction.y < 0) direction.y = -direction.y; // 防止向地下击退
        externalImpact += direction * force;
    }
    #endregion

    #region 空间规避与状态控制 (Dash & States)
    private void CaptureInputs()
    {
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame) BufferInput(InputType.Dash);
        if (Mouse.current.rightButton.wasPressedThisFrame) BufferInput(InputType.SwitchPolarity);
        if (Mouse.current.leftButton.wasPressedThisFrame) BufferInput(InputType.Parry);
        if (Keyboard.current.fKey.wasPressedThisFrame) BufferInput(InputType.Execute);
        
        // 清理过期指令
        inputBuffer.RemoveAll(i => Time.time - i.timestamp > inputBufferDuration);
    }

    private void BufferInput(InputType type)
    {
        inputBuffer.Add(new BufferedInput { type = type, timestamp = Time.time });
    }

    private void ProcessInputBuffer()
    {
        if (inputBuffer.Count == 0 || currentState != PlayerState.Normal && currentState != PlayerState.Jumping) return;

        for (int i = 0; i < inputBuffer.Count; i++)
        {
            var input = inputBuffer[i];
            
            // 执行 Dash
            if (input.type == InputType.Dash && Time.time >= lastDashTime + dashCooldown)
            {
                inputBuffer.RemoveAt(i);
                StartCoroutine(DashRoutine());
                return;
            }
            
            // 其他指令 (Switch, Parry, Execute) 由对应的战斗脚本每帧查询 ConsumeBuffer
        }
    }

    /// <summary>
    /// 提供给外部战斗逻辑（PlayerCombatReceiver/PlayerPolarity）通过此接口消费预输入指令
    /// </summary>
    public bool ConsumeBuffer(InputType type)
    {
        for (int i = 0; i < inputBuffer.Count; i++)
        {
            if (inputBuffer[i].type == type)
            {
                inputBuffer.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void HandleDash()
    {
        // 该逻辑已废弃，统一通过 ProcessInputBuffer 呼叫
    }

    private IEnumerator DashRoutine()
    {
        currentState = PlayerState.Dashing;
        lastDashTime = Time.time;
        Debug.Log($"<color=cyan>💨 [Action] Player Dashed at {lastDashTime:F2}</color>");
        velocity.y = 0f; // 冲刺期间不受重力影响

        Vector3 dashDir = transform.forward; // 默认向前冲刺
        // 如果有输入，则朝输入方向冲刺
        float h = Keyboard.current.dKey.ReadValue() - Keyboard.current.aKey.ReadValue();
        float v = Keyboard.current.wKey.ReadValue() - Keyboard.current.sKey.ReadValue();
        if (h != 0 || v != 0)
        {
             dashDir = (Quaternion.Euler(0f, cam != null ? cam.eulerAngles.y : 0f, 0f) * new Vector3(h, 0, v)).normalized;
             transform.rotation = Quaternion.LookRotation(dashDir);
        }

        // 视效增强：形变 (Squash and Stretch)
        if (visualRoot != null)
        {
            Vector3 origScale = visualRoot.localScale;
            // 【修复屏幕被向下拉的抖动问题】不再压缩 Y 轴，只压扁 X 轴并拉长 Z 轴，防止由于高度变化导致的 Cinemachine 锁定点疯狂下坠
            visualRoot.DOScale(new Vector3(origScale.x * 0.7f, origScale.y * 1.0f, origScale.z * 1.5f), dashDuration * 0.3f)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => {
                    visualRoot.DOScale(origScale, dashDuration * 0.7f).SetEase(Ease.OutBounce);
                });
        }

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            // 增加安全检测：如果冲刺中途触发了边界吸回逻辑，立即中断位移
            if (isProcessingOutOfBounds) break;

            // 物理增强：阻力缓动 (Ease-Out) 曲线，替代原来的匀速冲刺，手感更干脆
            float t = elapsed / dashDuration;
            // speed 从 dashSpeed*1.5 快速衰减到 dashSpeed*0.2
            float currentSpeed = Mathf.Lerp(dashSpeed * 1.5f, dashSpeed * 0.2f, t * t); // 使用 t*t 产生类似 easeOutQuad 的减速感
            
            // 【修复玩家下坠与跳跃平滑度】不再强制施加 Vector3.down * 4f 这会导致陡坡异常下坠和相机剧烈抖动
            // 改为仅保留角色控制器所需的极微小贴地力(Vector3.down * 0.5f)即可保证 isGrounded
            cc.Move((dashDir * currentSpeed + Vector3.down * 0.5f) * Time.deltaTime);
            yield return null;
        }

        // 冲刺结束后的安全重置
        velocity.y = -2f; // 强制给一个向下贴地力，防止冲刺由于斜坡导致的“飞出”
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
    private void ResetLevel()
    {
        Debug.Log("<color=red>💀 [System] 坠入深渊/脱离核心区... 秩序重置。</color>");
        
        // 【核心修复】：SceneManager.LoadScene 不会自动重置 Time.timeScale。
        // 如果在顿帧(Hitlag)中途触发重置，会导致新场景卡在慢动作下，影响输入判定。
        Time.timeScale = 1f;
        DOTween.KillAll();
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    #endregion
}
