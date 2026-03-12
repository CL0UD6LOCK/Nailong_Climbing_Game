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
    public float acceleration = 80f; 
    public float friction = 100f;    

    [Header("跳跃/物理")]
    public float jumpHeight = 2.8f; 
    public float gravity = -35f;
    public float fallMultiplier = 4.5f;    
    public float lowJumpMultiplier = 3.5f; 

    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("接地检测")]
    public bool isGravityFlipped = false;
    public LayerMask groundLayer;
    public float groundCheckExtra = 0.05f; 

    [Header("重力翻转")]
    public float flipSpeed = 12f;
    public float slopeLimitValue = 55f;

    [Header("特效")]
    public ParticleSystem dustParticle;
    [Range(0, 50)] public float dustRate = 15f;

    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private Vector3 horizontalMove;
    private float jumpIgnoreTimer;
    private float flipCooldownTimer;
    private bool isGrounded;

    // 内部变量：用于动态调整中心点防止 CharacterController 卡死
    private Vector3 originalCenter;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        tpCam = Camera.main.GetComponent<TPCamera>();

        if (transform.childCount > 0) meshTransform = transform.GetChild(0);

        controller.skinWidth = 0.08f; 
        controller.minMoveDistance = 0f;
        controller.stepOffset = 0.3f; 
        controller.slopeLimit = slopeLimitValue;
        
        // 记录初始中心点
        originalCenter = controller.center;

        ApplyVisualOffset();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        jumpIgnoreTimer -= Time.deltaTime;
        flipCooldownTimer -= Time.deltaTime;

        if (kb.spaceKey.wasPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        isGrounded = MyGroundCheck();

        // --- 核心优化：动态中心点微调 ---
        // 在起跳或弹起的瞬间，略微收缩碰撞体，防止 CharacterController 内部碰撞检测把初始速度抹除
        if (jumpIgnoreTimer > 0)
        {
            // 向重力反方向收缩 0.15f，确保脚部脱离地面，无瞬移感
            float offsetAmount = isGravityFlipped ? 0.15f : -0.15f;
            controller.center = originalCenter + new Vector3(0, offsetAmount, 0);
        }
        else
        {
            controller.center = originalCenter;
        }

        HandleGravity(kb);

        if (jumpBufferCounter > 0 && coyoteCounter > 0)
        {
            PerformJump();
        }

        HandleMoveInput(kb);

        Vector3 finalVelocity = horizontalMove + Vector3.up * verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);

        HandleVisuals(kb);
    }

    private void HandleGravity(Keyboard kb)
    {
        if (isGrounded && jumpIgnoreTimer <= 0)
        {
            coyoteCounter = coyoteTime;
            verticalVelocity = isGravityFlipped ? 1.5f : -1.5f; 
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
        verticalVelocity = Mathf.Clamp(verticalVelocity, -50f, 50f);
    }

    bool MyGroundCheck()
    {
        if (jumpIgnoreTimer > 0) return false;

        Vector3 dir = isGravityFlipped ? Vector3.up : Vector3.down;
        
        // 探测始终基于原始物理中心
        Vector3 offset = originalCenter;
        if (isGravityFlipped) offset.y = -offset.y;
        
        Vector3 origin = transform.position + offset;
        
        // 减小探测半径，防止侧面碰撞误判
        float checkRadius = controller.radius * 0.7f; 
        float dist = (controller.height / 2f) - controller.radius + groundCheckExtra;
        
        return Physics.SphereCast(origin, checkRadius, dir, out _, dist, groundLayer);
    }

    void PerformJump()
    {
        float jumpForce = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity = isGravityFlipped ? -jumpForce : jumpForce;

        // 保护时间，同时触发上面的碰撞体中心偏移
        jumpIgnoreTimer = 0.2f; 
        jumpBufferCounter = 0;
        coyoteCounter = 0;

        // 注意：这里移除了 controller.Move，解决瞬移感
        if (animator) animator.SetTrigger("Jump");
    }

    void HandleMoveInput(Keyboard kb)
    {
        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0)
        ).normalized;

        if (input.magnitude > 0.1f)
        {
            Vector3 targetDir = tpCam.GetRotation() * new Vector3(input.x, 0, input.y);
            float targetSpeed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 targetVel = targetDir * targetSpeed;

            horizontalMove = Vector3.MoveTowards(horizontalMove, targetVel, acceleration * Time.deltaTime);

            Vector3 upDir = isGravityFlipped ? Vector3.down : Vector3.up;
            Quaternion targetRot = Quaternion.LookRotation(targetDir, upDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
        else
        {
            horizontalMove = Vector3.MoveTowards(horizontalMove, Vector3.zero, friction * Time.deltaTime);
        }
    }

    void HandleVisuals(Keyboard kb)
    {
        float targetZ = isGravityFlipped ? 180f : 0f;
        Quaternion targetRotation = Quaternion.Euler(0, transform.eulerAngles.y, targetZ);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * flipSpeed);

        if (animator)
        {
            animator.SetFloat("SpeedPercent", horizontalMove.magnitude / runSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("isGrounded", isGrounded);
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
        jumpIgnoreTimer = 0.3f; // 翻转时也享受碰撞保护
        flipCooldownTimer = 0.5f;
        ApplyVisualOffset();
        
        Physics.SyncTransforms();
    }

    // --- 完整集成的 Bounce 函数 ---
    public void Bounce(float force)
    {
        // 反转下向下弹（负值），正常下向上弹（正值）
        verticalVelocity = isGravityFlipped ? -force : force;
        
        // 关键：必须给 jumpIgnoreTimer，否则下一帧会被 HandleGravity 的落地判定重置速度
        jumpIgnoreTimer = 0.2f; 
        coyoteCounter = 0;
        
        if (animator) animator.SetTrigger("Jump");
    }
}