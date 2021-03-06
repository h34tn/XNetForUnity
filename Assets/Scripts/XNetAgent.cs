using System.Collections.Generic; // Using IntPtr
using System;
using System.Runtime.InteropServices; // Using DllImport

namespace JoyZion.Network
{
    public delegate void ConnectEventHandler();
    public delegate void DisconnectEventHandler();
    public delegate void RecvedDataHandler(byte[] data, UInt32 len);
    public delegate void ErrorHandler(Int16 code);

    enum XNetError
    {
        ERR_NONE = 0,
        ERR_CONNECT_FAIL = -1,
        ERR_RCV_FAIL = -2,
        ERR_SEND_FAIL = -3,
        ERR_RCVBUF_READ_FAIL = -4,
        ERR_RCVBUF_WRITE_FAIL = -5,
        ERR_SENDBUF_READ_FAIL = -6,
        ERR_SENDBUF_WRITE_FAIL = -7,
        ERR_PEER_CLOSED = -8,
        ERR_NO_CONNECTION = -9,
        ERR_CREAT_THREAD_FAIL = -10,
        ERR_INVALID_PARAM = -11,
        ERR_CONNECT_EXIST = -12
    };

    public class XNetAgent
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        const string XNetModuleName = "libXNet";
#elif UNITY_IOS
        const string XNetModuleName = "__Internal";
#else 
        const string XNetModuleName = "XNet";
#endif
        /**
         * Below are mapping to the dll export functions in the library
         * By default, C and C++ use cdecl - but marshalling uses stdcall to match the Windows API
         */
        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateConnection();

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyConnection(IntPtr obj);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Open(IntPtr obj, string hostName, UInt16 port, Int32 timeout);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Reopen(IntPtr obj);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Receive(IntPtr obj, out IntPtr buff, out UInt32 len);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Send(IntPtr obj, [MarshalAs(UnmanagedType.LPArray)] byte[] data, UInt32 len);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Close(IntPtr obj);

        private delegate void XConNotifyDelegate(IntPtr obj, Byte type, Int16 code);

        [DllImport(XNetModuleName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetXConNotifyCallback(IntPtr obj, [MarshalAs(UnmanagedType.FunctionPtr)]XConNotifyDelegate callback);

        private IntPtr _xconn;
        private const UInt32 RECV_BUF_SIZE = 102400;
        private byte[] _recvBuf = new byte[RECV_BUF_SIZE];

        private enum CallbackType
        {
            OnConnect = 1,
            OnDisconnect,
            OnReceive,
            OnError
        }
        private struct CallbackData
        {
            public CallbackType type;
            public Int16 code;
        }
        private Queue<CallbackData> _callbackQueue = new Queue<CallbackData>();

        private static Dictionary<IntPtr, XNetAgent> _agents = new Dictionary<IntPtr, XNetAgent>();

        public event ConnectEventHandler connectEvent;
        public event DisconnectEventHandler disconnectEvent;
        public event RecvedDataHandler recvedDataEvent;
        public event ErrorHandler errorEvent;

        public int Create(string host, UInt16 port)
        {
            // This holds the obj pointer for dll created object
            _xconn = CreateConnection();

            if (IntPtr.Zero != _xconn)
            {
                _agents[_xconn] = this;
                SetXConNotifyCallback(_xconn, new XConNotifyDelegate(OnXConNotify));
                return Open(_xconn, host, port, 5000);
            }
            else
                return -1;
        }

        [MonoPInvokeCallback(typeof(XConNotifyDelegate))]
        private static void OnXConNotify(IntPtr obj, Byte type, Int16 code)
        {
            if (_agents.ContainsKey(obj))
                _agents[obj]._callbackQueue.Enqueue(new CallbackData() { type = (CallbackType)type, code = code });
        }

        public void Update()
        {
            while (_callbackQueue.Count > 0)
            {
                CallbackData data = _callbackQueue.Dequeue();
                switch (data.type)
                {
                    case CallbackType.OnConnect:
                        if (connectEvent != null)
                            connectEvent();
                        break;
                    case CallbackType.OnReceive:
                        IntPtr ptr = IntPtr.Zero;
                        UInt32 len = RECV_BUF_SIZE;
                        while(Receive(_xconn, out ptr, out len) > 0)
                        {
                            Marshal.Copy(ptr, _recvBuf, 0, (int)len);
                            if (recvedDataEvent != null)
                                recvedDataEvent(_recvBuf, len);
                        }
                        break;
                    case CallbackType.OnDisconnect:
                        if (disconnectEvent != null)
                            disconnectEvent();
                        break;
                    case CallbackType.OnError:
                        if (errorEvent != null)
                            errorEvent(data.code);
                        break;
                }
            }
        }

        public bool Send(byte[] data, UInt32 len)
        {
            if (data.Length > 0 && IntPtr.Zero != _xconn)
            {
                if (Send(_xconn, data, len) > 0)
                    return true;
                else
                    return false;
            }
            return false;
        }

        public void Destroy()
        {
            if (IntPtr.Zero != _xconn)
            {
                Close(_xconn);
                if (_agents.ContainsKey(_xconn))
                    _agents.Remove(_xconn);
                // Destroy the object created by dll
                DestroyConnection(_xconn);
                _xconn = IntPtr.Zero;
            }
        }
    }
}
