using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleTriggerAudio : MonoBehaviour
{
    [Header("音效素材")]
    public AudioClip triggerClip; // 触发时播放的单一音效

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // 基础配置
        audioSource.spatialBlend = 0f;    // 2D 音效
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查是否为玩家
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            if (triggerClip != null)
            {
                // 播放音效
                audioSource.PlayOneShot(triggerClip);
            }
        }
    }
}