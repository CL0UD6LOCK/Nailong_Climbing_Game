using UnityEngine;

public class Trampoline : MonoBehaviour
{
    public float bounceForce = 25f; // 弹起的力量，比普通跳跃高一点

    private void OnTriggerEnter(Collider other)
    {
        // 寻找进入触发器的物体身上有没有 PlayerController
        PlayerController player = other.GetComponent<PlayerController>();

        if (player != null)
        {
            // 直接调用玩家脚本里的弹跳方法
            player.Bounce(bounceForce);
            
            // 可选：播放蹦床自己的动画或粒子
            // GetComponent<Animator>().SetTrigger("Bounce");
        }
    }
}