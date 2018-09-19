using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using System.Collections.Generic;

// Using IntPtr
using System;

// Using DllImport
using System.Runtime.InteropServices;

// Using Encoding
using System.Text;

/*
 * By default, C and C++ use cdecl - but marshalling uses stdcall to match the Windows API
 */

public class LibraryLoader : MonoBehaviour
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    const string XNetModuleName = "libXNet";
#else
    const string XNetModuleName = "XNet";
#endif

    public string ipAddr;
    public Text input;
    public Text log;

    /**
     * Below are mapping to the dll export functions in the library
     */
    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr CreateConnection();

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void DestroyConnection(IntPtr obj);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool Open(IntPtr obj, string hostName, ushort port);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool Reopen(IntPtr obj);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool Update(IntPtr obj);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Receive(IntPtr obj, out IntPtr buff, out UInt32 len);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool Send(IntPtr obj, [MarshalAs(UnmanagedType.LPArray)] byte[] data, uint len);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Close(IntPtr obj);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int QueryState(IntPtr obj);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int QueryError(IntPtr obj);

    private delegate void OnConnectDelegate(ushort code);
    private delegate void OnReceiveDelegate();

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetOnConnectCallback(IntPtr obj, [MarshalAs(UnmanagedType.FunctionPtr)]OnConnectDelegate callback);

    [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetOnReceiveCallback(IntPtr obj, [MarshalAs(UnmanagedType.FunctionPtr)]OnReceiveDelegate callback);

    IntPtr xConn;
    UInt32 len = 102400;
    byte[] arrBuf = new byte[102400];

    static bool hasRcvData = false;

    static LibraryLoader instance = null;

    //static Dictionary<IntPtr , LibraryLoader> 

    /*
     * TODO：由于Unity相关接口只能在主线程中处理，所以构造一个回调处理队列，针对OnConnect, OnDisConnect, OnReceive按FIFO在Update中进行处理
     */

    enum CallbackType
    {
        OnConnect,
        OnDisconnect,
        OnReceive,
        OnError
    }

    struct CallbackData
    {
        public CallbackType type;
        public UInt16 code;
    }

    private Queue<CallbackData> _queue = new Queue<CallbackData>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
    // This holds the obj pointer for dll created object
        xConn = CreateConnection();

        if (IntPtr.Zero != xConn)
        {
            Log("XConnection object is created!");

            SetOnConnectCallback(xConn, new OnConnectDelegate(OnConnect));
            SetOnReceiveCallback(xConn, new OnReceiveDelegate(OnReceive));

            if (Open(xConn, ipAddr, 1001))
            {
                Log("Create connection thread success!");

            }
            else
            {
                Log("Create connection thread fail!");
            }
        }
    }

    private static void OnConnect(UInt16 code)
    {
        //if (code == 0)
        //{
        //    instance.Log("connect success!");
        //}
        //else
        //{
        //    instance.Log(string.Format("connect fail, error code = {1}!", code));
        //}
        instance._queue.Enqueue(new CallbackData(){ type = CallbackType.OnConnect , code = code });
    }

    private void Update()
    {
        //Profiler.BeginSample("XConnection");
        //if (IntPtr.Zero != xConn)
        //{
        //    if (!Update(xConn))
        //    {
        //        Log(string.Format("Here is a error : {0}", QueryError(xConn)));
        //    }
        //}
        //Profiler.EndSample();

        //if (recvData.Count > 0)
        //{
        //    Log(string.Format("OnReceive[size = {0}] : {1}", recvData.Peek().Length, Encoding.ASCII.GetString(recvData.Dequeue())));
        //}

        while(_queue.Count > 0)
        {
            CallbackData data = _queue.Dequeue();
            switch(data.type)
            {
                case CallbackType.OnConnect:
                    if (data.code == 0)
                    {
                        instance.Log(string.Format("connect {0} success!" , ipAddr));
                    }
                    else
                    {
                        instance.Log(string.Format("connect fail, error code = {1}!", data.code));
                    }
                    break;
                case CallbackType.OnReceive:
                    IntPtr ptr = IntPtr.Zero;
                    if (Receive(xConn, out ptr, out len) > 0)
                    {
                        Marshal.Copy(ptr, arrBuf, 0, (int)len);
                        Log(string.Format("OnReceive[size = {0}] : {1}", len, Encoding.ASCII.GetString(arrBuf)));
                    }
                    break;
            }
        }

        //if(hasRcvData)
        //{
        //    IntPtr ptr = IntPtr.Zero;
        //    if (Receive(xConn, out ptr, out len) > 0)
        //    {
        //        Marshal.Copy(ptr, arrBuf, 0, (int)len);
        //        Log(string.Format("OnReceive[size = {0}] : {1}", len, Encoding.ASCII.GetString(arrBuf)));
        //    }
        //    hasRcvData = false;
        //}
    }

    public void Send()
    {
        if(!string.IsNullOrEmpty(input.text) && IntPtr.Zero != xConn)
        {
            byte[] data = Encoding.ASCII.GetBytes(input.text);
            if(!Send(xConn, data, (uint)data.Length))
            {
                Log("Send Failed!");
            }
        }
    }

    private static void OnReceive()
    {
        instance._queue.Enqueue(new CallbackData() { type = CallbackType.OnReceive });
    }

    void OnDestroy()
    {
        if(IntPtr.Zero != xConn)
        {
            Close(xConn);
            // Destroy the object created by dll
            DestroyConnection(xConn);
            xConn = IntPtr.Zero;
        }
    }

    void Log(string logStr)
    {
        log.text = logStr;
        //Debug.Log(logStr);
    }
}
