using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    private TPCamera tpCam;
    private CharacterController controller;
    private Animator animator;
    private Transform meshTransform;

    [Header("移动设置")]
    public float walkSpeed = 3.5f; 
    public float runSpeed = 5.3f;  
    public float rotationSpeed = 15f;
    public float acceleration = 30f;
    public float friction = 20f;

    [Header("跳跃/物理")]
    public float jumpHeight = 2.8f;
    public float gravity = -35f;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;

    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("接地检测")]
    public bool isGravityFlipped = false;
    public LayerMask groundLayer;
    public float groundCheckExtra = 0.12f;
    public float groundingForce = 0.8f;
    public float minGroundDot = 0.7f;

    [Header("重力翻转/坡度")]
    public float flipCooldown = 0.5f;
    public float slopeLimitValue = 65f;

    [Header("特效")]
    public ParticleSystem dustParticle;

    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    public Vector3 horizontalMove;
    private float jumpIgnoreTimer;
    private float flipCooldownTimer;
    public bool isGrounded;
    private bool wasGroundedLastFrame;

    private Vector3 lerpedNormal = Vector3.up;
    private Vector3 originalCenter;

    private bool hasLandParam;
    private bool hasJumpParam;

    // 修改点 1: 使用 OnNetworkSpawn 替代 Start 处理联机逻辑
public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        
        // 只有本地玩家（你自己）才去控制摄像机
        if (IsOwner)
        {
            if (Camera.main != null)
            {
                tpCam = Camera.main.GetComponent<TPCamera>();
                if (tpCam != null)
                {
                    // 【核心修改点】
                    // 在玩家的子物体里寻找名字叫 "CameraTarget" 的物体
                    Transform myTarget = transform.Find("CameraTraget");

                    if (myTarget != null)
                    {
                        // 找到了！把这个空物体交给相机脚本
                        tpCam.SetTarget(myTarget);
                    }
                    else
                    {
                        // 没找到（可能是名字写错了），先用玩家根节点保底
                        Debug.LogWarning("未能在 Prefab 中找到 CameraTarget，请检查名字大小写！");
                        tpCam.SetTarget(this.transform);
                    }
                }
            }
        }

        // --- 以下是原有的初始化逻辑，不要动 ---
        if (transform.childCount > 0)
            meshTransform = transform.GetChild(0);

        controller.skinWidth = 0.08f;
        controller.slopeLimit = slopeLimitValue; 
        
        originalCenter = controller.center;
        lerpedNormal = Vector3.up;

        CheckAnimatorParameters();
        ApplyVisualOffset();
    }

    void Update()
    {
        // 只有物体的拥有者（本地玩家）才能执行移动输入
        if (!IsOwner) return;

        if(controller.slopeLimit != slopeLimitValue) 
            controller.slopeLimit = slopeLimitValue;

        var kb = Keyboard.current;
        if (kb == null) return;

        jumpIgnoreTimer -= Time.deltaTime;
        flipCooldownTimer -= Time.deltaTime;

        if (kb.spaceKey.wasPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        wasGroundedLastFrame = isGrounded;
        isGrounded = MyGroundCheck();

        if (isGrounded && !wasGroundedLastFrame && jumpIgnoreTimer <= 0)
        {
            verticalVelocity = isGravityFlipped ? 0.2f : -0.2f;
            if (animator && hasLandParam) animator.SetTrigger("Land");
        }

        HandleGravity(kb);

        if (jumpBufferCounter > 0 && coyoteCounter > 0)
            PerformJump();

        HandleMoveInput(kb);

        Vector3 finalVelocity = horizontalMove + Vector3.up * verticalVelocity;

        if (isGrounded && jumpIgnoreTimer <= 0)
        {
            Vector3 rayOrigin = transform.position + (isGravityFlipped ? Vector3.down : Vector3.up) * 0.05f;
            Vector3 rayDir = isGravityFlipped ? Vector3.up : Vector3.down;
            float maxDist = groundCheckExtra + controller.skinWidth;

            if (Physics.SphereCast(rayOrigin, controller.radius * 0.7f, rayDir, out RaycastHit hit, maxDist, groundLayer))
            {
                float dot = Vector3.Dot(hit.normal, (isGravityFlipped ? Vector3.down : Vector3.up));
                bool falling = isGravityFlipped ? verticalVelocity > 0 : verticalVelocity < 0;

                if (dot >= minGroundDot && falling)
                {
                    float snapAmount = Vector3.Dot((hit.point - transform.position), rayDir);
                    snapAmount = Mathf.Clamp(snapAmount, -0.1f, 0.1f);
                    finalVelocity += rayDir * snapAmount * 15f;

                    if(horizontalMove.sqrMagnitude > 0.01f)
                        horizontalMove = Vector3.ProjectOnPlane(horizontalMove, hit.normal);
                }
            }
        }

        controller.Move(finalVelocity * Time.deltaTime);
        HandleVisuals();
    }

    private void HandleGravity(Keyboard kb)
    {
        if (isGrounded && jumpIgnoreTimer <= 0)
        {
            coyoteCounter = coyoteTime;
            verticalVelocity = isGravityFlipped ? groundingForce : -groundingForce;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
            float curGrav = isGravityFlipped ? -gravity : gravity;
            if (!isGravityFlipped)
            {
                if (verticalVelocity < 0) curGrav *= fallMultiplier;
                else if (verticalVelocity > 0 && !kb.spaceKey.isPressed) curGrav *= lowJumpMultiplier;
            }
            else
            {
                if (verticalVelocity > 0) curGrav *= fallMultiplier;
                else if (verticalVelocity < 0 && !kb.spaceKey.isPressed) curGrav *= lowJumpMultiplier;
            }
            verticalVelocity += curGrav * Time.deltaTime;
        }
        verticalVelocity = Mathf.Clamp(verticalVelocity, -40f, 40f);
    }

    bool MyGroundCheck()
    {
        if (jumpIgnoreTimer > 0) return false;
        Vector3 dir = isGravityFlipped ? Vector3.up : Vector3.down;
        Vector3 offset = originalCenter;
        if (isGravityFlipped) offset.y = -offset.y;

        Vector3 origin = transform.position + offset;
        float checkRadius = controller.radius * 0.65f;
        float dist = (controller.height / 2f) - controller.radius + groundCheckExtra;

        if (Physics.SphereCast(origin, checkRadius, dir, out RaycastHit hit, dist, groundLayer))
        {
            float angle = Vector3.Angle(isGravityFlipped ? Vector3.down : Vector3.up, hit.normal);
            if (angle < controller.slopeLimit)
                lerpedNormal = Vector3.Slerp(lerpedNormal, hit.normal, Time.deltaTime * 12f);
            else
                lerpedNormal = isGravityFlipped ? Vector3.down : Vector3.up;
            return true;
        }
        lerpedNormal = isGravityFlipped ? Vector3.down : Vector3.up;
        return false;
    }

    void HandleMoveInput(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        input.Normalize();

        if (input.magnitude > 0.1f)
        {
            // 只有当 tpCam 被正确赋值（仅本地 Owner）时才计算相机方向
            if (tpCam != null)
            {
                Vector3 camDir = tpCam.GetRotation() * new Vector3(input.x, 0, input.y);
                camDir.y = 0;
                camDir.Normalize();
                Vector3 moveTarget = Vector3.ProjectOnPlane(camDir, lerpedNormal).normalized;
                float targetSpeed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
                horizontalMove = Vector3.MoveTowards(horizontalMove, moveTarget * targetSpeed, acceleration * Time.deltaTime);
            }
        }
        else
        {
            horizontalMove = Vector3.MoveTowards(horizontalMove, Vector3.zero, friction * Time.deltaTime);
        }

        Vector3 lookDir = horizontalMove;
        if (lookDir.sqrMagnitude < 0.01f) lookDir = transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, isGravityFlipped ? Vector3.down : Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    void PerformJump()
    {
        float jumpForce = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity = isGravityFlipped ? -jumpForce : jumpForce;
        jumpIgnoreTimer = 0.15f;
        jumpBufferCounter = 0;
        coyoteCounter = 0;
        isGrounded = false;
        if (animator && hasJumpParam) animator.SetTrigger("Jump");
    }

    void HandleVisuals()
    {
        float currentFullSpeed = controller.velocity.magnitude;
        float horizontalSpeed = horizontalMove.magnitude;

        if (animator)
        {
            animator.SetFloat("SpeedPercent", horizontalSpeed / runSpeed, 0.05f, Time.deltaTime);
            animator.SetBool("isGrounded", isGrounded);
        }

        if (dustParticle != null)
        {
            var emission = dustParticle.emission;
            if (currentFullSpeed > 0.1f)
            {
                emission.enabled = true;
                float targetRate = currentFullSpeed > walkSpeed ? 
                    Mathf.Lerp(2.8f, 15f, Mathf.InverseLerp(walkSpeed, runSpeed, currentFullSpeed)) : 
                    Mathf.Lerp(0f, 2.8f, Mathf.InverseLerp(0.1f, walkSpeed, currentFullSpeed));
                emission.rateOverTime = targetRate;
                if (!dustParticle.isPlaying) dustParticle.Play();
            }
            else emission.enabled = false;
        }
    }

    private void CheckAnimatorParameters()
    {
        if (animator == null) return;
        foreach (var param in animator.parameters)
        {
            if (param.name == "Land") hasLandParam = true;
            if (param.name == "Jump") hasJumpParam = true;
        }
    }

    private void ApplyVisualOffset()
    {
        if (meshTransform != null)
        {
            float offsetY = isGravityFlipped ? controller.skinWidth : -controller.skinWidth;
            meshTransform.localPosition = new Vector3(0, offsetY, 0);
        }
    }

    public void FlipGravity()
    {
        if (flipCooldownTimer > 0) return;
        isGravityFlipped = !isGravityFlipped;
        verticalVelocity = 0;
        jumpIgnoreTimer = 0.25f;
        flipCooldownTimer = flipCooldown;
        lerpedNormal = isGravityFlipped ? Vector3.down : Vector3.up;
        ApplyVisualOffset();
        Physics.SyncTransforms();
    }

    public void Bounce(float force)
    {
        verticalVelocity = isGravityFlipped ? -force : force;
        jumpIgnoreTimer = 0.15f;
        coyoteCounter = 0;
        isGrounded = false;
        if (animator && hasJumpParam) animator.SetTrigger("Jump");
    }
}