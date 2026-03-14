using UnityEngine;
using TMPro;

public class HeightDisplay : MonoBehaviour
{
    [Header("设置")]
    public Transform playerTransform; 
    public string unit = " m";
    public float heightOffset = 0.6f; // 补偿值

    private TextMeshProUGUI heightText;

    void Start()
    {
        heightText = GetComponent<TextMeshProUGUI>();

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (playerTransform != null && heightText != null)
        {
            // 获取 Y 轴高度并减去 0.6f
            float adjustedHeight = playerTransform.position.y - heightOffset;

            // 只显示数字和单位，保留一位小数
            heightText.text = adjustedHeight.ToString("F1") + unit;
        }
    }
}