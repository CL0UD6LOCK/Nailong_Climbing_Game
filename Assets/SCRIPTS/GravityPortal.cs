using UnityEngine;

public class GravityPortal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 查找玩家脚本
        PlayerController player = other.GetComponent<PlayerController>();
        
        if (player != null)
        {
            player.FlipGravity();
        }
    }
}