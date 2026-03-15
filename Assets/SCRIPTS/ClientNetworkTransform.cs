using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // 关键：重写授权逻辑，让 Owner（也就是客户端本人）拥有修改坐标的权力
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}