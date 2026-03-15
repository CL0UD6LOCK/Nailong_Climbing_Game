using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private string ipAddress = "127.0.0.1"; 

    void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            GUI.Label(new Rect(10, 10, 300, 30), "场景中缺少 NetworkManager 物体！");
            return;
        }

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            GUI.Box(new Rect(5, 5, 200, 180), "联机菜单");
            GUI.Label(new Rect(15, 30, 150, 20), "主机 IP (Radmin IP):");
            ipAddress = GUI.TextField(new Rect(15, 55, 170, 25), ipAddress);

            if (GUI.Button(new Rect(15, 90, 170, 30), "作为主机启动 (Host)"))
            {
                SetIpAddress(); 
                NetworkManager.Singleton.StartHost();
            }

            if (GUI.Button(new Rect(15, 130, 170, 30), "作为客户端加入 (Client)"))
            {
                SetIpAddress(); 
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "主机 (Host)" : "客户端 (Client)";
            GUI.Label(new Rect(10, 10, 200, 30), $"当前状态: {mode}");
            
            if (GUI.Button(new Rect(10, 40, 100, 30), "断开连接"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }

    void SetIpAddress()
{
    var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
    if (transport != null)
    {
        // 客户端连接用的地址（你输入的 26.x.x.x）
        transport.ConnectionData.Address = ipAddress;

        // 【新增这一行】主机监听地址必须是 0.0.0.0
        // 这代表让主机同时监听 127.0.0.1 和 Radmin 的 26.x.x.x
        transport.ConnectionData.ServerListenAddress = "0.0.0.0"; 
    }
}
}