using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PortalAudio : MonoBehaviour
{
    [Header("音效素材")]
    public AudioClip teleportClip;   // 角色穿过瞬间的声音

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // 关键设置：
        audioSource.spatialBlend = 0f;    // 2D 音效，音量无视距离
        audioSource.playOnAwake = false;  // 启动时不自动播放
        audioSource.loop = false;         // 瞬间音效不需要循环
    }

    // 当物体进入触发器时调用
    private void OnTriggerEnter(Collider other)
    {
        // 检查进入的是不是玩家
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            if (teleportClip != null)
            {
                // 瞬间播放一次传送音效
                audioSource.PlayOneShot(teleportClip, 1.0f);
            }
        }
    }
}