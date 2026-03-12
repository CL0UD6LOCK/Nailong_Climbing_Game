using UnityEngine;

public class FogHorizonController : MonoBehaviour
{
    [Header("设置 TOD 引用")]
    public MonoBehaviour todComponent; // 将你截图里的那个“全局参数面板”物体拖进来

    [Header("距离设置")]
    public float fogStartDistance = 50f;  // 近处多少米内是清晰的
    public float fogEndDistance = 500f;   // 远处多少米完全变雾（盖住地平线）

    void Start()
    {
        // 开启 Unity 全局雾开关
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
    }

    void Update()
    {
        if (todComponent != null)
        {
            // 利用反射或者直接访问（如果字段是public）来获取面板上的颜色
            // 假设你的插件字段名叫 FogLightColor
            // 注意：如果报错，请确认你插件代码里这个颜色的具体变量名
            Color targetColor = GetColorFromTOD("FogLightColor");

            // 同步颜色
            RenderSettings.fogColor = targetColor;
            
            // 同步距离
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
        }
    }

    // 这是一个万能方法，用来读取你那个插件里的颜色变量
    private Color GetColorFromTOD(string fieldName)
    {
        try {
            var field = todComponent.GetType().GetField(fieldName);
            if (field != null) return (Color)field.GetValue(todComponent);
            
            var prop = todComponent.GetType().GetProperty(fieldName);
            if (prop != null) return (Color)prop.GetValue(todComponent, null);
        } catch { }
        
        return Color.gray; // 没找到则返回灰色
    }
}