using UnityEngine;
using UnityEngine.InputSystem;

public class TPCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target;        
    public Vector3 pivotOffset = new Vector3(0, 1.6f, 0); 

    [Header("距离设置")]
    public float defaultDistance = 4f; 
    public float minDistance = 1.5f;
    public float maxDistance = 10f;
    public float zoomSpeed = 10f;

    [Header("旋转设置")]
    public float mouseSensitivity = 0.12f; 
    public float smoothTime = 0.02f;      
    public Vector2 pitchMinMax = new Vector2(-20f, 70f); 

    [Header("重力适配")]
    public float rotationLerpSpeed = 5f; 

    private float yaw;
    private float pitch;
    private float currentDistance; 
    private Vector3 currentVelocity;
    private float currentZRotation = 0f;
    
    // 内部优化：用于平滑 Pivot 切换
    private Vector3 currentPivotPos;

    void Start()
    {
        if (target == null) return;
        Cursor.lockState = CursorLockMode.Locked;
        
        currentDistance = defaultDistance;
        
        yaw = target.eulerAngles.y; 
        pitch = 15f; 

        // 初始化 Pivot 位置防止第一帧跳变
        var player = target.GetComponentInParent<PlayerController>();
        bool isFlipped = player != null && player.isGravityFlipped;
        currentPivotPos = isFlipped ? new Vector3(pivotOffset.x, -pivotOffset.y, pivotOffset.z) : pivotOffset;
    }

    void LateUpdate()
    {
        if (!target || Mouse.current == null) return;

        // 1. 获取鼠标旋转输入
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        var player = target.GetComponentInParent<PlayerController>();
        
        // 关键判断：获取角色是否接地
        bool isFlipped = player != null && player.isGravityFlipped;
        bool targetGrounded = player != null && player.GetComponent<CharacterController>().isGrounded;

        float flipMult = isFlipped ? -1f : 1f;
        yaw += mouseDelta.x * mouseSensitivity * flipMult;
        pitch -= mouseDelta.y * mouseSensitivity * flipMult;
        
        if (isFlipped)
            pitch = Mathf.Clamp(pitch, -pitchMinMax.y, -pitchMinMax.x);
        else
            pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        // 2. 获取滚轮缩放输入
        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        defaultDistance = Mathf.Clamp(defaultDistance - scroll * zoomSpeed, minDistance, maxDistance);

        // 传入接地状态进行逻辑更新
        UpdateCameraLogic(Time.deltaTime, isFlipped, targetGrounded);
    }

    private void UpdateCameraLogic(float deltaTime, bool isFlipped, bool isGrounded)
    {
        // 3. Z轴翻转平滑插值
        float targetZ = isFlipped ? 180f : 0f;
        currentZRotation = Mathf.LerpAngle(currentZRotation, targetZ, deltaTime * rotationLerpSpeed);
        
        // 4. 计算动态 Pivot：平滑处理 y 轴偏移
        Vector3 targetPivot = pivotOffset;
        if (isFlipped) targetPivot.y = -pivotOffset.y; 
        currentPivotPos = Vector3.Lerp(currentPivotPos, targetPivot, deltaTime * rotationLerpSpeed);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, currentZRotation);
        
        // 5. 平滑处理距离变化
        currentDistance = Mathf.Lerp(currentDistance, defaultDistance, deltaTime * 15f);
        
        // 6. 核心点计算 (基于 yaw 旋转 Pivot)
        Vector3 focusPoint = target.position + (Quaternion.Euler(0, yaw, 0) * currentPivotPos);
        
        // 7. 计算最终位置
        Vector3 finalPosition = focusPoint - (rotation * Vector3.forward * currentDistance);

        // --- 核心修复：消除空中抖动 ---
        if (isGrounded)
        {
            // 在地面时，使用 SmoothDamp 过滤行走时小凸起带来的高频抖动
            transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
        }
        else
        {
            // 在空中时，相机必须 100% 紧贴目标，否则重力导致的非线性位移与插值算法会产生视觉拉扯
            transform.position = finalPosition;
            currentVelocity = Vector3.zero; // 重置平滑速度，防止着陆瞬间产生巨大的插值冲力
        }

        transform.rotation = rotation;
    }

    public Quaternion GetRotation()
    {
        var player = target.GetComponentInParent<PlayerController>();
        if (player != null && player.isGravityFlipped)
        {
            return Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, 180f);
        }
        return Quaternion.Euler(0, yaw, 0);
    }
}