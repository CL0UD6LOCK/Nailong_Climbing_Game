using UnityEngine;
using UnityEngine.InputSystem;

public class TPCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target;
    public Vector3 pivotOffset = new Vector3(0, 1.6f, 0);

    [Header("距离设置")]
    public float defaultDistance = 2.0f; // 已按要求改为 2
    public float minDistance = 1.5f;     
    public float maxDistance = 2.4f;     
    public float zoomSpeed = 10f;        

    [Header("旋转设置")]
    public float mouseSensitivity = 0.12f;
    public float smoothTime = 0.02f;
    public Vector2 pitchMinMax = new Vector2(-20f, 70f);

    [Header("重力适配")]
    public float rotationLerpSpeed = 5f;

    [Header("镜头晃动")]
    public float bobAmount = 0.04f;
    public float bobSpeed = 11f;

    [Header("跳跃效果")]
    public float jumpTilt = 2f;
    public float jumpRecoverSpeed = 6f;

    [Header("落地冲击")]
    public float landingImpact = 0.12f;
    public float landingRecoverSpeed = 8f;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private Vector3 currentVelocity;
    private float currentZRotation = 0f;

    private Vector3 currentPivotPos;

    float bobTimer;
    float currentBob;

    float jumpOffset;
    float landingOffset;

    bool lastGrounded;

    // --- 联机适配新增方法 ---
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        // 只有当本地玩家生成并关联相机时，才锁定鼠标
        Cursor.lockState = CursorLockMode.Locked;

        currentDistance = defaultDistance;
        yaw = target.eulerAngles.y;
        pitch = 15f;

        var player = target.GetComponentInParent<PlayerController>();
        bool isFlipped = player != null && player.isGravityFlipped;

        currentPivotPos = isFlipped ?
            new Vector3(pivotOffset.x, -pivotOffset.y, pivotOffset.z) :
            pivotOffset;
    }

    void Start()
    {
        // 如果是联机模式，target 会在 OnNetworkSpawn 时通过 SetTarget 传入
        // 这里保留对单机测试的兼容
        if (target != null) 
        {
            SetTarget(target);
        }
    }

    void LateUpdate()
    {
        // 关键：如果没有目标（比如还没点 Host），不执行逻辑，不抢夺鼠标
        if (!target || Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        var player = target.GetComponentInParent<PlayerController>();

        bool isFlipped = player != null && player.isGravityFlipped;
        
        // 优化：直接从 PlayerController 获取接地状态，避免联机同步时的组件访问开销
        bool targetGrounded = player != null ? player.isGrounded : target.GetComponent<CharacterController>().isGrounded;

        float flipMult = isFlipped ? -1f : 1f;

        yaw += mouseDelta.x * mouseSensitivity * flipMult;
        pitch -= mouseDelta.y * mouseSensitivity * flipMult;

        if (isFlipped)
            pitch = Mathf.Clamp(pitch, -pitchMinMax.y, -pitchMinMax.x);
        else
            pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        defaultDistance = Mathf.Clamp(defaultDistance - scroll * zoomSpeed, minDistance, maxDistance);

        UpdateCameraLogic(Time.deltaTime, isFlipped, targetGrounded);
    }

    // 内部逻辑完全保留你原始的数学计算，不做任何改动
    private void UpdateCameraLogic(float deltaTime, bool isFlipped, bool isGrounded)
    {
        float targetZ = isFlipped ? 180f : 0f;
        currentZRotation = Mathf.LerpAngle(currentZRotation, targetZ, deltaTime * rotationLerpSpeed);

        Vector3 targetPivot = pivotOffset;
        if (isFlipped) targetPivot.y = -pivotOffset.y;

        currentPivotPos = Vector3.Lerp(currentPivotPos, targetPivot, deltaTime * rotationLerpSpeed);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, currentZRotation);

        currentDistance = Mathf.Lerp(currentDistance, defaultDistance, deltaTime * 15f);

        Vector3 focusPoint = target.position + (Quaternion.Euler(0, yaw, 0) * currentPivotPos);

        Vector3 finalPosition = focusPoint - (rotation * Vector3.forward * currentDistance);

        // ===== 行走检测 =====
        var player = target.GetComponentInParent<PlayerController>();
        float moveAmount = 0f;

        if (player != null)
        {
            // 使用你的原始逻辑：通过 CharacterController 的真实速度计算
            CharacterController cc = player.GetComponent<CharacterController>();
            moveAmount = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        }

        // ===== 行走镜头晃动 ===== (保持原样)
        if (isGrounded && moveAmount > 0.1f)
        {
            bobTimer += deltaTime * bobSpeed;
            currentBob = Mathf.Sin(bobTimer) * bobAmount;
        }
        else
        {
            bobTimer = 0;
            currentBob = Mathf.Lerp(currentBob, 0, deltaTime * 5f);
        }

        // ===== 跳跃抬头 ===== (保持原样)
        if (!isGrounded)
        {
            jumpOffset = Mathf.Lerp(jumpOffset, jumpTilt, deltaTime * jumpRecoverSpeed);
        }
        else
        {
            jumpOffset = Mathf.Lerp(jumpOffset, 0, deltaTime * jumpRecoverSpeed);
        }

        // ===== 落地冲击 ===== (保持原样)
        if (!lastGrounded && isGrounded)
        {
            landingOffset = landingImpact;
        }

        landingOffset = Mathf.Lerp(landingOffset, 0, deltaTime * landingRecoverSpeed);

        lastGrounded = isGrounded;

        // ===== 相机效果叠加 ===== (保持原样)
        Vector3 cameraEffectOffset =
            transform.up * currentBob +
            transform.up * landingOffset;

        finalPosition += cameraEffectOffset;

        if (isGrounded)
        {
            transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
        }
        else
        {
            transform.position = finalPosition;
            currentVelocity = Vector3.zero;
        }

        Quaternion jumpRotation = Quaternion.Euler(-jumpOffset, 0, 0);
        transform.rotation = rotation * jumpRotation;
    }

    public Quaternion GetRotation()
    {
        // 增加判空防止报错
        if (target == null) return Quaternion.identity;

        var player = target.GetComponentInParent<PlayerController>();

        if (player != null && player.isGravityFlipped)
        {
            return Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, 180f);
        }

        return Quaternion.Euler(0, yaw, 0);
    }
}