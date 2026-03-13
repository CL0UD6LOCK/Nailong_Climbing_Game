using UnityEngine;

public class TeleportPlayer : MonoBehaviour
{
    // 在编辑器里拖入目标位置（一个 Empty Object）
    public Transform destination; 

    private void OnTriggerEnter(Collider other)
    {
        // 检查碰撞体是否带有 "Player" 标签
        if (other.CompareTag("Player"))
        {
            // 如果玩家使用了 CharacterController，需要先禁用它再移动
            CharacterController cc = other.GetComponent<CharacterController>();
            
            if (cc != null) cc.enabled = false;

            other.transform.position = destination.position;

            if (cc != null) cc.enabled = true;
            
            Debug.Log("玩家已传送到：" + destination.position);
        }
    }
}