using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
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
    public float slopeLimitValue = 65f; // 确保面板上也是65

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

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        tpCam = Camera.main.GetComponent<TPCamera>();

        if (transform.childCount > 0)
            meshTransform = transform.GetChild(0);

        // 初始化 CharacterController
        controller.skinWidth = 0.08f;
        controller.slopeLimit = slopeLimitValue; 
        
        originalCenter = controller.center;
        lerpedNormal = Vector3.up;

        CheckAnimatorParameters();
        ApplyVisualOffset();
    }

    void Update()
    {
        // 关键修复：每一帧确保 CharacterController 的斜度限制与变量一致
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

        // -------- 贴地逻辑 --------
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

                    // 修复：只在有输入时才根据法线投影，否则容易导致速度归零太快
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
            if (angle < controller.slopeLimit) // 这里会受 slopeLimitValue 影响
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
            Vector3 camDir = tpCam.GetRotation() * new Vector3(input.x, 0, input.y);
            camDir.y = 0;
            camDir.Normalize();
            Vector3 moveTarget = Vector3.ProjectOnPlane(camDir, lerpedNormal).normalized;
            float targetSpeed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            horizontalMove = Vector3.MoveTowards(horizontalMove, moveTarget * targetSpeed, acceleration * Time.deltaTime);
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
        // 获取 CharacterController 的实际物理速度模长（包含 X, Y, Z）
        float currentFullSpeed = controller.velocity.magnitude;
        float horizontalSpeed = horizontalMove.magnitude;

        if (animator)
        {
            animator.SetFloat("SpeedPercent", horizontalSpeed / runSpeed, 0.05f, Time.deltaTime);
            animator.SetBool("isGrounded", isGrounded);
        }

        // -------- 粒子逻辑：全方位速度感应 + 平滑过渡 --------
        if (dustParticle != null)
        {
            var emission = dustParticle.emission;

            if (currentFullSpeed > 0.1f) // 只要有位移
            {
                emission.enabled = true;

                float targetRate;
                if (currentFullSpeed > walkSpeed)
                {
                    // 速度在 walkSpeed 到 runSpeed 之间，平滑线性插值到 15f
                    // InverseLerp 计算当前速度在 [walkSpeed, runSpeed] 区间的百分比
                    float t = Mathf.InverseLerp(walkSpeed, runSpeed, currentFullSpeed);
                    targetRate = Mathf.Lerp(2.8f, 15f, t);
                }
                else
                {
                    // 速度在 0 到 walkSpeed 之间，平滑插值到 2.8f
                    float t = Mathf.InverseLerp(0.1f, walkSpeed, currentFullSpeed);
                    targetRate = Mathf.Lerp(0f, 2.8f, t);
                }

                emission.rateOverTime = targetRate;

                if (!dustParticle.isPlaying) dustParticle.Play();
            }
            else
            {
                // 完全静止
                emission.enabled = false;
            }
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