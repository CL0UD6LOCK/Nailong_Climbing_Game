using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Netcode; 
using AHD2TimeOfDay; 

public class NetTODController : NetworkBehaviour 
{
    [Header("核心引用")]
    public TODGlobalParameters todGlobalParameters; 
    public Light MainLight; 
    public LensFlareComponentSRP LensFlare;

    [Header("时间设置")]
    [SerializeField] public bool isTimeFlow = true;
    [Tooltip("时间流逝速度，数值越大过得越快（你之前的设置是0.3）")]
    public float timeSpeed = 0.3f; 

    // 同步变量：仅服务端可写，所有人可读
    public NetworkVariable<float> syncedTime = new NetworkVariable<float>(
        6f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector3 _starquat; 
    private Vector3 _endquat; 
    private Vector3 _fakeMainlightDirection; 
    
    private static readonly int AHD2_MainlightColor = Shader.PropertyToID("AHD2_MainlightColor");
    private static readonly int AHD2_MainlightIntensity = Shader.PropertyToID("AHD2_MainlightIntensity");
    private static readonly int AHD2_FakeMainlightDirection = Shader.PropertyToID("AHD2_FakeMainlightDirection");
    private static readonly int TodTimeRatio = Shader.PropertyToID("_todTimeRatio");
    private static readonly int IblBrdfLut = Shader.PropertyToID("_iblBrdfLut");
    private static readonly int AHD2_FoglightColor = Shader.PropertyToID("AHD2_FoglightColor");
    private static readonly int AHD2_HGCoefficient = Shader.PropertyToID("AHD2_HGCoefficient");

    public override void OnNetworkSpawn()
    {
        if (todGlobalParameters == null) return;
        
        todGlobalParameters.Initailize();
        foreach (var tod in todGlobalParameters.timeOfDays)
        {
            tod.Initialize();
        }

        // 初始化时间对齐
        if (IsServer) 
        {
            syncedTime.Value = todGlobalParameters.CurrentTime;
        }
        else 
        {
            todGlobalParameters.CurrentTime = syncedTime.Value;
        }

        todGlobalParameters.FixTimeOfDay();
        InitialLight();
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            // 只有主机（Server）负责计算时间增加
            if (isTimeFlow && Application.isPlaying)
            {
                // 手动计算时间增加，确保 0-24 循环
                float nextTime = syncedTime.Value + (Time.deltaTime * timeSpeed);
                if (nextTime >= 24f) nextTime = 0f;
                
                syncedTime.Value = nextTime;
            }
        }

        // 所有人（Host 和 Client）都同步到这个时间
        todGlobalParameters.CurrentTime = syncedTime.Value;

        // 手动调用同步逻辑，不使用插件自带的 BaseUpdate 累加时间，防止冲突
        // 但我们需要插件内部去更新色彩插值等逻辑
        UpdatePluginInternals(); 

        SetGlobalParameters();
        RotateLight();
    }

    /// <summary>
    /// 强制驱动插件内部的颜色、比例等逻辑更新
    /// </summary>
    void UpdatePluginInternals()
    {
        // 这一步确保插件内部的动画比例（todElapsedTimeRatio）根据我们同步的 CurrentTime 更新
        todGlobalParameters.FixTimeOfDay();
        // 这里的 BaseUpdate 我们调用它主要是为了让它更新色彩插值，
        // 如果插件内部 BaseUpdate 会强制加时间，我们上面赋值的 CurrentTime 会覆盖它
        todGlobalParameters.BaseUpdate(); 
    }

    void InitialLight()
    {
        _starquat = new Vector3(-90, 0, 0); 
        _endquat = new Vector3(270, 0, 0); 
    }

    void RotateLight()
    {
        Vector3 quat = Vector3.Lerp(_starquat, _endquat, todGlobalParameters.CurrentTime / 24);
        _fakeMainlightDirection = -(Quaternion.Euler(quat) * Vector3.forward).normalized;
    }

    public void SetGlobalParameters()
    {
        if (MainLight == null) return;
        
        MainLight.color = todGlobalParameters.MainlightColor;
        MainLight.intensity = todGlobalParameters.MainlightIntensity;
        MainLight.transform.rotation = todGlobalParameters.MainlightDirection;

        if (LensFlare)
        {
            LensFlare.enabled = todGlobalParameters._dayOrNight == 0;
            LensFlare.transform.rotation = Quaternion.LookRotation(-_fakeMainlightDirection);
        }

        Shader.SetGlobalColor(AHD2_MainlightColor, todGlobalParameters.MainlightColor);
        Shader.SetGlobalFloat(AHD2_MainlightIntensity, todGlobalParameters.MainlightIntensity);
        Shader.SetGlobalVector(AHD2_FakeMainlightDirection, new Vector4(_fakeMainlightDirection.x, _fakeMainlightDirection.y, _fakeMainlightDirection.z, todGlobalParameters._dayOrNight));
        Shader.SetGlobalFloat(TodTimeRatio, todGlobalParameters.todElapsedTimeRatio);
        Shader.SetGlobalTexture(IblBrdfLut, todGlobalParameters.IblBrdfLut);
        Shader.SetGlobalVector(AHD2_FoglightColor, new Vector4(todGlobalParameters.FogLightColor.r, todGlobalParameters.FogLightColor.g, todGlobalParameters.FogLightColor.b, todGlobalParameters.FogLightIntensity));
        Shader.SetGlobalFloat(AHD2_HGCoefficient, todGlobalParameters.HGCoefficient);
    }
}