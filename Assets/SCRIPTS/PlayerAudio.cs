using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private AudioSource audioSource;
    private CharacterController controller;

    [Header("音效素材")]
    public AudioClip[] footstepClips;
    public AudioClip landClip;
    public AudioClip bedBounceClip; // 蹦床音效

    [Header("节奏设置")]
    public float walkInterval = 0.8f;
    public float runInterval = 0.7f;

    [Header("随机化设置")]
    [Range(0, 0.2f)] public float pitchRandomness = 0.08f;
    [Range(0, 0.2f)] public float volumeRandomness = 0.05f;

    [Header("落地限制")]
    public float landCooldown = 0.5f;

    [Header("蹦床限制")]
    public float bedCooldown = 0.3f;

    private float stepTimer;
    private bool lastFrameGrounded;

    private float lastLandTime = -10f;
    private float lastBedTime = -10f;

    private int bedLayer;

    void Start()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        controller = GetComponent<CharacterController>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f;

        // 获取BED层
        bedLayer = LayerMask.NameToLayer("BED");
    }

    void Update()
    {
        HandleFootsteps();
        HandleLanding();

        lastFrameGrounded = playerController.isGrounded;
    }

    private void HandleFootsteps()
    {
        float currentSpeed = controller.velocity.magnitude;

        if (playerController.isGrounded && currentSpeed > 0.2f)
        {
            float lerpFactor = Mathf.InverseLerp(
                playerController.walkSpeed,
                playerController.runSpeed,
                currentSpeed
            );

            float currentInterval = Mathf.Lerp(walkInterval, runInterval, lerpFactor);

            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0f)
            {
                PlayRandomFootstep(currentSpeed);
                stepTimer = currentInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    private void HandleLanding()
    {
        if (playerController.isGrounded && !lastFrameGrounded)
        {
            if (Time.time - lastLandTime < landCooldown)
                return;

            lastLandTime = Time.time;

            if (landClip != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(landClip, 0.7f);
            }
        }
    }

    private void PlayRandomFootstep(float speed)
    {
        if (footstepClips.Length == 0) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];

        bool isRunning = speed > (playerController.walkSpeed + playerController.runSpeed) * 0.5f;

        float basePitch;
        float baseVolume;

        if (isRunning)
        {
            basePitch = 0.3f;
            baseVolume = 0.26f;
        }
        else
        {
            basePitch = 0.3f;
            baseVolume = 0.22f;
        }

        float randomPitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);
        float randomVolume = baseVolume - Random.Range(0, volumeRandomness);

        audioSource.pitch = randomPitch;
        audioSource.PlayOneShot(clip, randomVolume);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 检测蹦床层
        if (hit.gameObject.layer == bedLayer)
        {
            if (Time.time - lastBedTime < bedCooldown)
                return;

            lastBedTime = Time.time;

            if (bedBounceClip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(bedBounceClip, 0.8f);
            }
        }
    }
    void OnTriggerEnter(Collider other)
{
    if (other.gameObject.layer == bedLayer)
    {
        if (Time.time - lastBedTime < bedCooldown)
            return;

        lastBedTime = Time.time;

        if (bedBounceClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(bedBounceClip, 0.8f);
        }
    }
}
}