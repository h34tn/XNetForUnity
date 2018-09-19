using UnityEngine;
using UnityEngine.UI;
using JoyZion.Network;

public class XNetDemo : MonoBehaviour
{
    public string ipAddr;
    public ushort port;
    public Text outputText;
    public Text inputText;

    private XNetAgent _xnetAgent;

    private void Awake()
    {
        _xnetAgent = new XNetAgent();
        _xnetAgent.connectEvent += _xnetAgent_connectEvent;
        _xnetAgent.disconnectEvent += _xnetAgent_disconnectEvent;
        _xnetAgent.recvedDataEvent += _xnetAgent_recvedDataEvent;
        _xnetAgent.errorEvent += _xnetAgent_errorEvent;
        _xnetAgent.Create(ipAddr, port);
    }

    private void Update()
    {
        _xnetAgent.Update();
    }

    private void _xnetAgent_errorEvent(short code)
    {
        Debug.LogError("Error : " + code);
    }

    private void _xnetAgent_recvedDataEvent(byte[] data, uint len)
    {
        outputText.text = System.Text.Encoding.ASCII.GetString(data, 0, (int)len);
    }

    private void _xnetAgent_disconnectEvent()
    {
        Debug.Log("Disconnect Event");
    }

    private void _xnetAgent_connectEvent()
    {
        Debug.Log("Connect Event");
    }

    private void OnDestroy()
    {
        _xnetAgent.Destroy();
    }

    public void Send()
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes(inputText.text);
        _xnetAgent.Send(data, (uint)data.Length);
    }
}
