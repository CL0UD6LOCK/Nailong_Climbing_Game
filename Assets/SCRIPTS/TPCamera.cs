using UnityEngine;
using UnityEngine.InputSystem; // 必须引入 New Input System

public class TPCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target;        // 角色模型或空物体（推荐对齐头部）
    // 初始偏移：推荐 (0, 1.6, 0)，对齐角色眼睛高度
    public Vector3 pivotOffset = new Vector3(0, 1.6f, 0); 

    [Header("距离设置")]
    public float defaultDistance = 4f; 
    public float minDistance = 1.5f;
    public float maxDistance = 10f;
    public float zoomSpeed = 10f;

    [Header("旋转设置")]
    public float mouseSensitivity = 0.12f; // 推荐 0.1 左右
    public float smoothTime = 0.02f;      // 设为 0.02 可获得极高的跟随频率，消除拉伸感
    public Vector2 pitchMinMax = new Vector2(-20f, 70f); // 仰角/俯角限制

    [Header("遮挡探测")]
    public bool useCollision = true; 
    public LayerMask collisionLayers; // 勾选要产生碰撞的层（如 Default），不要勾选 Player！

    // 内部状态
    private float yaw;
    private float pitch;
    private float currentDistance;
    private Vector3 currentVelocity;

    void Start()
    {
        if (target == null) 
        {
            Debug.LogError("请在检查器中为 TPCamera 指定 Target！");
            return;
        }

        // 初始化鼠标状态和变量
        Cursor.lockState = CursorLockMode.Locked;
        currentDistance = defaultDistance;
        
        // 初始 Yaw 与目标一致，初始 Pitch 设为 15f（平视）
        yaw = target.eulerAngles.y; 
        pitch = 15f; 

        // 立即计算一次位置，防止开局跳动
        UpdateCameraLogic(1.0f); 
    }

    void LateUpdate()
    {
        if (!target || Mouse.current == null) return;

        // 1. 获取输入 (New Input System Delta 不需要乘以 Time.deltaTime)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        // 获取滚轮输入 (新系统 Scroll 需乘以 0.01f 对齐旧系统手感)
        float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        defaultDistance = Mathf.Clamp(defaultDistance - scroll * zoomSpeed, minDistance, maxDistance);

        // 执行跟随逻辑
        UpdateCameraLogic(Time.deltaTime);
    }

    private void UpdateCameraLogic(float deltaTime)
    {
        // 2. 计算理想旋转和中心点
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 focusPoint = target.position + pivotOffset;

        // 3. 计算理想位置（固定距离）
        Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * defaultDistance);

        // 4. 碰撞检测（避障）
        float finalDistance = defaultDistance;
        if (useCollision)
        {
            RaycastHit hit;
            Vector3 direction = desiredPosition - focusPoint;
            // 从角色射向相机
            if (Physics.Raycast(focusPoint, direction.normalized, out hit, defaultDistance, collisionLayers))
            {
                // 如果撞墙，距离缩短到碰撞点
                finalDistance = Mathf.Max(minDistance, hit.distance - 0.2f);
            }
        }

        // 5. 应用位置与旋转
        // 将距离变化的 Lerp 速度提高到 25f，使相机在避障时更“紧跟”
        currentDistance = Mathf.Lerp(currentDistance, finalDistance, deltaTime * 25f);
        Vector3 finalPosition = focusPoint - (rotation * Vector3.forward * currentDistance);

        // 使用极小的 smoothTime 获取极高频的定位更新
        transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
        transform.rotation = rotation;
    }

    // 供 PlayerController 调用，获取相机的水平朝向（去掉 Pitch）
    public Quaternion GetRotation()
    {
        return Quaternion.Euler(0, yaw, 0);
    }
}