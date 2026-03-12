using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private TPCamera tpCam;
    private CharacterController controller;
    private Animator animator;

    [Header("移动设置")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 15f; 

    [Header("丝滑跳跃设置")]
    public float jumpHeight = 2.5f;
    public float gravity = -35f;        
    public float fallMultiplier = 4f;   
    public float lowJumpMultiplier = 3f; 
    public float coyoteTime = 0.2f;      
    public float jumpBufferTime = 0.2f;  

    [Header("特效设置")]
    public ParticleSystem dustParticle; 
    public float dustRate = 50f; // 跑动时的发射速率

    // 内部物理变量
    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private Vector3 currentMoveDir; 

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        tpCam = Camera.main.GetComponent<TPCamera>();

        if (!tpCam) Debug.LogError("请确保主相机挂载了 TPCamera 脚本！");
        controller.skinWidth = 0.08f;

        // --- 初始化粒子逻辑 ---
        if (dustParticle != null)
        {
            var main = dustParticle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            var emission = dustParticle.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            
            if (!dustParticle.isPlaying) 
            {
                dustParticle.Play(); 
            }
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // --- 1. 跳跃输入缓冲 ---
        if (kb.spaceKey.wasPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        // --- 2. 地面检测与土狼时间 ---
        if (controller.isGrounded)
        {
            coyoteCounter = coyoteTime;
            if (verticalVelocity < 0) verticalVelocity = -2f; 
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // --- 3. 执行跳跃核心逻辑 ---
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            PerformJump(jumpHeight);
        }

        // --- 4. 处理水平移动 ---
        HandleMovementInput(kb);

        // --- 5. 应用重力和位移 ---
        ApplyPhysics(kb);

        // --- 6. 更新动画与粒子特效 ---
        HandleVisuals(kb);
    }

    // 执行跳跃的内部逻辑
    private void PerformJump(float height)
    {
        verticalVelocity = Mathf.Sqrt(height * -2f * gravity);
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    // --- 新增：给蹦床调用的公开方法 ---
    public void Bounce(float bounceForce)
    {
        // 直接设置垂直速度，让玩家弹起
        verticalVelocity = bounceForce;
        
        // 重置土狼时间，防止空中二段跳（如果不需要可以删掉这行）
        coyoteCounter = 0f;

        // 触发动画机里的 Jump 触发器（如果有）
        if (animator != null)
        {
            animator.SetTrigger("Jump"); 
        }
    }

    void HandleMovementInput(Keyboard kb)
    {
        Vector2 moveInput = Vector2.zero;
        if (kb.wKey.isPressed) moveInput.y = 1;
        if (kb.sKey.isPressed) moveInput.y = -1;
        if (kb.aKey.isPressed) moveInput.x = -1;
        if (kb.dKey.isPressed) moveInput.x = 1;
        
        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        currentMoveDir = Vector3.zero;

        if (inputDir.magnitude > 0.1f)
        {
            currentMoveDir = tpCam.GetRotation() * inputDir;
            float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            controller.Move(currentMoveDir * speed * Time.deltaTime);

            Quaternion targetRot = Quaternion.LookRotation(currentMoveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    void ApplyPhysics(Keyboard kb)
    {
        float currentGravity = gravity;
        if (verticalVelocity < 0) currentGravity *= fallMultiplier;
        else if (verticalVelocity > 0 && !kb.spaceKey.isPressed) currentGravity *= lowJumpMultiplier;

        verticalVelocity += currentGravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    void HandleVisuals(Keyboard kb)
    {
        bool isMoving = currentMoveDir.magnitude > 0.1f;
        bool isRunning = isMoving && kb.leftShiftKey.isPressed;

        if (animator)
        {
            float animSpeed = isMoving ? (isRunning ? 1.0f : 0.5f) : 0f;
            animator.SetFloat("SpeedPercent", animSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("isGrounded", controller.isGrounded);
        }

        if (dustParticle != null)
        {
            var emission = dustParticle.emission;
            if (isRunning && controller.isGrounded)
            {
                emission.rateOverTime = dustRate;
            }
            else
            {
                emission.rateOverTime = 0f;
            }
        }
    }
}