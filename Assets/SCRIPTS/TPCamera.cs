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
    private float currentDistance; // 实际当前的距离（用于平滑缩放）
    private Vector3 currentVelocity;
    private float currentZRotation = 0f;

    void Start()
    {
        if (target == null) return;
        Cursor.lockState = CursorLockMode.Locked;
        
        // 初始化距离
        currentDistance = defaultDistance;
        
        yaw = target.eulerAngles.y; 
        pitch = 15f; 
    }

    void LateUpdate()
    {
        if (!target || Mouse.current == null) return;

        // 1. 获取鼠标旋转输入
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        var player = target.GetComponentInParent<PlayerController>();
        bool isFlipped = player != null && player.isGravityFlipped;

        // 旋转逻辑随重力翻转
        float flipMult = isFlipped ? -1f : 1f;
        yaw += mouseDelta.x * mouseSensitivity * flipMult;
        pitch -= mouseDelta.y * mouseSensitivity * flipMult;
        
        // 俯仰角镜像限制
        if (isFlipped)
            pitch = Mathf.Clamp(pitch, -pitchMinMax.y, -pitchMinMax.x);
        else
            pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        // 2. 获取滚轮缩放输入 (新版加回)
        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        // 修改 defaultDistance 作为目标缩放值
        defaultDistance = Mathf.Clamp(defaultDistance - scroll * zoomSpeed, minDistance, maxDistance);

        UpdateCameraLogic(Time.deltaTime, isFlipped);
    }

    private void UpdateCameraLogic(float deltaTime, bool isFlipped)
    {
        // 3. Z轴翻转平滑插值
        float targetZ = isFlipped ? 180f : 0f;
        currentZRotation = Mathf.LerpAngle(currentZRotation, targetZ, deltaTime * rotationLerpSpeed);
        
        // 4. 计算动态 Pivot：解决倒立时视角偏低问题
        Vector3 dynamicPivot = pivotOffset;
        if (isFlipped) dynamicPivot.y = -pivotOffset.y; 

        Quaternion rotation = Quaternion.Euler(pitch, yaw, currentZRotation);
        
        // 5. 平滑处理距离变化 (让缩放更有弹性)
        currentDistance = Mathf.Lerp(currentDistance, defaultDistance, deltaTime * 15f);
        
        // 6. 核心点计算
        // 基于水平偏航计算焦点偏移，防止模型自转导致相机乱晃
        Vector3 focusPoint = target.position + (Quaternion.Euler(0, yaw, 0) * dynamicPivot);
        
        // 使用 currentDistance 代替 defaultDistance 实现平滑过渡
        Vector3 finalPosition = focusPoint - (rotation * Vector3.forward * currentDistance);

        // 7. 应用位置与旋转
        transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
        transform.rotation = rotation;
    }

    public Quaternion GetRotation()
    {
        var player = target.GetComponentInParent<PlayerController>();
        if (player != null && player.isGravityFlipped)
        {
            // 补偿相机翻转带来的输入反向
            return Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, 180f);
        }
        return Quaternion.Euler(0, yaw, 0);
    }
}