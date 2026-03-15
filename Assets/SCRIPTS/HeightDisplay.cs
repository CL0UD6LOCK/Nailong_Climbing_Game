using UnityEngine;
using TMPro;
using Unity.Netcode; // 必须引用

public class HeightDisplay : MonoBehaviour
{
    [Header("设置")]
    public string unit = " m";
    public float heightOffset = 0.6f; 

    private TextMeshProUGUI heightText;
    private Transform localPlayerTransform; // 专门存“我”的坐标

    void Start()
    {
        heightText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        // 如果还没找到自己的玩家，就一直找
        if (localPlayerTransform == null)
        {
            FindLocalPlayer();
            return;
        }

        if (heightText != null)
        {
            float adjustedHeight = localPlayerTransform.position.y - heightOffset;
            heightText.text = adjustedHeight.ToString("F1") + unit;
        }
    }

    void FindLocalPlayer()
    {
        // 遍历所有带 NetworkObject 的玩家
        foreach (var playerObj in GameObject.FindGameObjectsWithTag("Player"))
        {
            var nObj = playerObj.GetComponent<NetworkObject>();
            // 关键判断：只有当这个玩家是当前客户端控制的“自己”时
            if (nObj != null && nObj.IsOwner)
            {
                localPlayerTransform = playerObj.transform;
                break;
            }
        }
    }
}