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
    [Tooltip("冲刺时仅在贴地状态施加的轻微向下力，防止离地抖动；过大可能造成镜头下坠感。")]
    public float dashGroundStickForce = 0.05f;
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
    // 【环境系统 v2 - 主从架构】
    // 不再依赖不稳定的 OnTriggerExit，改由 PlayerController 自身
    // 低频（0.1s/次）轮询 PuddleManager.ActivePuddles，
    // 以径向距离 (Distance < puddle.Radius + 0.25f) 作为唯一事实依据。
    // ---------------------------------------------------------
    private float envPollInterval = 0.1f;   // 采样频率：每 0.1s 一次
    private float envPollTimer = 0f;
    private PlayerEnergySystem cachedEnergy;          // 缓存组件引用，避免重复 GetComponent
    private PlayerCombatReceiver cachedCombatReceiver;

    private void PollEnvironmentalConditions()
    {
        envPollTimer -= Time.deltaTime;
        if (envPollTimer > 0f) return;
        envPollTimer = envPollInterval;

        if (PuddleManager.Instance == null) return;

        var puddles = PuddleManager.Instance.ActivePuddles;
        bool isInsideAny = false;
        bool isInsideCore = false;
        float worstDamageRate = 0f;
        float worstSpeedMult = 1f;

        Vector3 myPos = transform.position;

        for (int i = 0; i < puddles.Count; i++)
        {
            ChaosPuddle p = puddles[i];
            // 防御性空检查（对象池回收偶发竞争）
            if (p == null || !p.isContaminated) continue;

            float dist = Vector3.Distance(
                new Vector3(myPos.x, p.transform.position.y, myPos.z), // 忽略 Y 轴高度差
                p.transform.position);

            if (dist < p.Radius + 0.25f) // 0.25m 容差，弥补低频采样间隙
            {
                isInsideAny = true;
                if (p.isCoreAnomaly) isInsideCore = true;

                // 取最严厉惩罚（叠加污染区时不叠乘，取最劣值）
                worstDamageRate = Mathf.Max(worstDamageRate, p.damagePerSecond);
                worstSpeedMult = Mathf.Min(worstSpeedMult, p.speedMultiplier);
            }
        }

        // 应用环境速度
        environmentalSpeedMultiplier = isInsideAny ? worstSpeedMult : 1.0f;

        // 应用持续伤害（已转换为本采样周期内的扣血量）
        if (isInsideAny && worstDamageRate > 0f)
        {
            if (cachedCombatReceiver == null) cachedCombatReceiver = GetComponent<PlayerCombatReceiver>();
            if (cachedCombatReceiver != null)
            {
                float dmg = worstDamageRate * envPollInterval;
                cachedCombatReceiver.ApplyEnvironmentalDamage(dmg);
                if (cachedCombatReceiver.currentHP <= 0f)
                    Debug.Log("<color=red>☠️ [Env] 污染区将玩家 HP 扣至 0</color>");

                // 🎵 播放环境伤害/灼烧音效 (0.1s轮询频次，音量压低)
                if (AudioManager.Instance != null && AudioManager.Instance.sfxPuddleHazard != null)
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.sfxPuddleHazard, 0.3f);
            }
        }

        // 同步给能量系统
        if (cachedEnergy == null) cachedEnergy = GetComponent<PlayerEnergySystem>();
        if (cachedEnergy != null) cachedEnergy.SetInPuddle(isInsideAny);

        // 同步核心异常区标记
        if (cachedCombatReceiver != null)
            cachedCombatReceiver.isStandingOnAnomalyCore = isInsideCore;
    }

    // 兼容层：ChaosPuddle 旧版可能残留的调用（重构后 Puddle 不再调用这两个接口）
    [System.Obsolete("主从架构 v2 中 Puddle 不再主动调用 RegisterPuddle，该函数保留仅作兼容过渡")]
    public void RegisterPuddle(ChaosPuddle p) { }
    [System.Obsolete("主从架构 v2 中 Puddle 不再主动调用 UnregisterPuddle，该函数保留仅作兼容过渡")]
    public void UnregisterPuddle(ChaosPuddle p) { }
    private PlayerState previousStateBeforeHitlag = PlayerState.Normal;
    private Coroutine hitlagCoroutine;

    // 击退系统变量
    private Vector3 externalImpact; // 外部冲击力
    public float knockbackResistance = 5f; // 阻力，数值越大停止越快

    [Header("🛡️ 软边界控制 (Arena Boundaries)")]
    public float arenaRadius = 20f;
    [SerializeField] private Transform arenaCenterOverride;
    [SerializeField] private float boundaryCheckDelay = 1.0f;
    [SerializeField] private float fallCheckDelay = 1.0f;
    private bool isProcessingOutOfBounds = false;
    private Vector3 initialCenterPosition;
    private float spawnTimestamp;

    private void Start()
    {
        // 【新规：隐藏与锁定鼠标】
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        spawnTimestamp = Time.time;
        initialCenterPosition = transform.position;

        cc = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        RefreshArenaCenterDuringStartup();

        // 软边界检测
        if (Time.time - spawnTimestamp >= boundaryCheckDelay)
        {
            CheckArenaBoundaries();
        }

        // 掉落检测
        if (Time.time - spawnTimestamp >= fallCheckDelay && transform.position.y < -15f)
        {
            TriggerFallDeath();
            return;
        }

        // 【环境感知 v2】低频轮询（每 0.1s 一次），替代 OnTrigger 事件驱动
        PollEnvironmentalConditions();

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
        // 仅检测水平距离 (X-Z)，以初始位置为准 (解决开始立刻死亡的Bug)
        Vector3 currentFlatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 centerFlatPos = new Vector3(initialCenterPosition.x, 0, initialCenterPosition.z);
        float distanceFromCenter = Vector3.Distance(currentFlatPos, centerFlatPos);

        if (distanceFromCenter > arenaRadius && !isProcessingOutOfBounds)
        {
            Debug.Log($"<color=orange>⚠️ [Boundary] OOB Trigger | pos={currentFlatPos} center={centerFlatPos} dist={distanceFromCenter:F2} radius={arenaRadius:F2}</color>");
            StartCoroutine(TriggerOutOfBoundsPunishment());
        }
    }

    private void RefreshArenaCenterDuringStartup()
    {
        if (arenaCenterOverride != null)
        {
            initialCenterPosition = arenaCenterOverride.position;
            return;
        }

        // 启动宽限期内，允许圆心跟随玩家，吸收同帧/跨帧的出身点重定位。
        if (Time.time - spawnTimestamp < boundaryCheckDelay)
        {
            initialCenterPosition = transform.position;
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

        // 【零 GC 优化】弃用 RemoveAll(lambda) —— 每次调用都会分配一个 Delegate 对象到托管堆
        // 改用反向 for 循环 + RemoveAt：无堆分配，且对于 0-3 长度的小列表性能更优
        float currentTime = Time.time;
        for (int i = inputBuffer.Count - 1; i >= 0; i--)
        {
            if (currentTime - inputBuffer[i].timestamp > inputBufferDuration)
            {
                inputBuffer.RemoveAt(i);
            }
        }
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

        // 🎵 播放冲刺音效
        if (AudioManager.Instance != null && AudioManager.Instance.sfxPlayerDash != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.sfxPlayerDash);

        velocity.y = 0f; // 冲刺期间不受重力影响

        // 【3C Day1】镜头 FOV 突破感：Ease.OutExpo 非线性爆发 → 缓慢回弹
        CameraController.Instance?.TriggerDashFOV();

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
                .OnComplete(() =>
                {
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

            // 仅在贴地状态施加极小贴地力，避免冲刺中途把角色硬压向下导致镜头俯冲卡顿。
            float stick = cc.isGrounded ? dashGroundStickForce : 0f;
            Vector3 dashMove = dashDir * currentSpeed;
            if (stick > 0f)
            {
                dashMove += Vector3.down * stick;
            }

            cc.Move(dashMove * Time.deltaTime);
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

        // 【3C Day1】防止 FOV Tween 残留导致新场景镜头漂移
        CameraController.Instance?.ResetFOV();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    #endregion
}
