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
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
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
    public float groundingForce = 0.8f; // 平时贴地力，不干扰跳跃
    public float minGroundDot = 0.7f;    // 贴地判断最小 dot，避免边缘吸附

    [Header("重力翻转")]
    public float flipCooldown = 0.5f;
    public float slopeLimitValue = 65f;

    [Header("特效")]
    public ParticleSystem dustParticle;

    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private Vector3 horizontalMove;
    private float jumpIgnoreTimer;
    private float flipCooldownTimer;
    private bool isGrounded;
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

        controller.skinWidth = 0.08f;
        controller.minMoveDistance = 0f;
        controller.stepOffset = 0.35f;
        controller.slopeLimit = slopeLimitValue;

        originalCenter = controller.center;
        lerpedNormal = Vector3.up;

        CheckAnimatorParameters();
        ApplyVisualOffset();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        jumpIgnoreTimer -= Time.deltaTime;
        flipCooldownTimer -= Time.deltaTime;

        // 跳跃缓冲
        if (kb.spaceKey.wasPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        // 接地检测
        wasGroundedLastFrame = isGrounded;
        isGrounded = MyGroundCheck();

        // 落地触发动画
        if (isGrounded && !wasGroundedLastFrame && jumpIgnoreTimer <= 0)
        {
            verticalVelocity = isGravityFlipped ? 0.2f : -0.2f;
            if (animator && hasLandParam) animator.SetTrigger("Land");
        }

        // 处理重力
        HandleGravity(kb);

        // 跳跃执行判定
        if (jumpBufferCounter > 0 && coyoteCounter > 0)
            PerformJump();

        HandleMoveInput(kb);

        // 应用位移
        Vector3 finalVelocity = horizontalMove + Vector3.up * verticalVelocity;

        // --- 优化贴地吸附 ---
        if (isGrounded && jumpIgnoreTimer <= 0)
        {
            Vector3 rayOrigin = transform.position + (isGravityFlipped ? Vector3.down : Vector3.up) * 0.05f;
            Vector3 rayDir = isGravityFlipped ? Vector3.up : Vector3.down;
            float sphereRadius = controller.radius * 0.7f;
            float maxDist = groundCheckExtra + controller.skinWidth;

            if (Physics.SphereCast(rayOrigin, sphereRadius, rayDir, out RaycastHit hit, maxDist, groundLayer))
            {
                float dot = Vector3.Dot(hit.normal, (isGravityFlipped ? Vector3.down : Vector3.up));
                if (dot >= minGroundDot) // 只在水平足够时吸附
                {
                    // 不覆盖 verticalVelocity，保持跳跃
                    float snapAmount = Vector3.Dot((hit.point - transform.position), rayDir);
                    finalVelocity += rayDir * snapAmount / Time.deltaTime;

                    // 水平速度投影到坡面，避免挤压和抖动
                    horizontalMove = Vector3.ProjectOnPlane(horizontalMove, hit.normal);
                    horizontalMove = Vector3.MoveTowards(horizontalMove, Vector3.zero, friction * Time.deltaTime);
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

            // 平常贴地力
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
        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0)
        ).normalized;

        if (input.magnitude > 0.1f)
        {
            Vector3 camDir = tpCam.GetRotation() * new Vector3(input.x, 0, input.y);
            camDir.y = 0;
            camDir.Normalize();

            Vector3 moveTarget = Vector3.ProjectOnPlane(camDir, lerpedNormal).normalized;

            float targetSpeed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 targetVel = moveTarget * targetSpeed;

            horizontalMove = Vector3.MoveTowards(horizontalMove, targetVel, acceleration * Time.deltaTime);
        }
        else
        {
            horizontalMove = Vector3.MoveTowards(horizontalMove, Vector3.zero, friction * Time.deltaTime);
        }

        Vector3 lookDir = horizontalMove;
        if (lookDir.sqrMagnitude < 0.01f) lookDir = transform.forward;
        lookDir.y = 0;

        Vector3 upDir = isGravityFlipped ? Vector3.down : Vector3.up;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, upDir);
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
        if (animator)
        {
            animator.SetFloat("SpeedPercent", horizontalMove.magnitude / runSpeed, 0.05f, Time.deltaTime);
            animator.SetBool("isGrounded", isGrounded);
        }

        if (dustParticle != null)
        {
            bool isMovingFast = horizontalMove.magnitude >= (runSpeed - 1f) && isGrounded;
            if (isMovingFast && !dustParticle.isPlaying) dustParticle.Play();
            else if (!isMovingFast && dustParticle.isPlaying) dustParticle.Stop();
        }
    }

    private void CheckAnimatorParameters()
    {
        if (animator == null) return;
        foreach (AnimatorControllerParameter param in animator.parameters)
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