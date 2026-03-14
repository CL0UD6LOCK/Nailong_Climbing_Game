using UnityEngine;
using UnityEngine.InputSystem;

public class TPCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target;
    public Vector3 pivotOffset = new Vector3(0, 1.6f, 0);

    [Header("距离设置")]
    public float defaultDistance = 1.9f;
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

    void Start()
    {
        if (target == null) return;

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

    void LateUpdate()
    {
        if (!target || Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        var player = target.GetComponentInParent<PlayerController>();

        bool isFlipped = player != null && player.isGravityFlipped;
        bool targetGrounded = player != null && player.GetComponent<CharacterController>().isGrounded;

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
            CharacterController cc = player.GetComponent<CharacterController>();
            moveAmount = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        }

        // ===== 行走镜头晃动 =====
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

        // ===== 跳跃抬头 =====
        if (!isGrounded)
        {
            jumpOffset = Mathf.Lerp(jumpOffset, jumpTilt, deltaTime * jumpRecoverSpeed);
        }
        else
        {
            jumpOffset = Mathf.Lerp(jumpOffset, 0, deltaTime * jumpRecoverSpeed);
        }

        // ===== 落地冲击 =====
        if (!lastGrounded && isGrounded)
        {
            landingOffset = landingImpact;
        }

        landingOffset = Mathf.Lerp(landingOffset, 0, deltaTime * landingRecoverSpeed);

        lastGrounded = isGrounded;

        // ===== 相机效果叠加 =====
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
        var player = target.GetComponentInParent<PlayerController>();

        if (player != null && player.isGravityFlipped)
        {
            return Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, 180f);
        }

        return Quaternion.Euler(0, yaw, 0);
    }
}