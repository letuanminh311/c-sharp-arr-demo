#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;

public unsafe class AndroidBridgeSource : IBridgeSource
{
    const string BridgeCls = "com.example.bridge_lib.ByteBridge";
    const string WriterCls = "com.example.bridge_lib.DemoWriter";
    const string ListenerCls = "com.example.bridge_lib.UpdateListener";

    // Java gọi hàm này trên thread của Java: KHÔNG gọi API Unity ở đây, chỉ tăng bộ đếm.
    class Proxy : AndroidJavaProxy
    {
        public int count;
        public Proxy() : base(ListenerCls) { }
        public void notifyUpdateShareMemory() { Interlocked.Increment(ref count); }
    }

    AndroidJavaClass _bridge, _writer;
    AndroidJavaObject _buffer;      // giữ tham chiếu để buffer không bị giải phóng
    Proxy _proxy;

    public IntPtr Address { get; private set; }
    public int Capacity { get; private set; }
    public int NotifyCount => _proxy == null ? 0 : Volatile.Read(ref _proxy.count);
    public string Name => "Android (JNI)";

    public bool Init()
    {
        _bridge = new AndroidJavaClass(BridgeCls);
        _writer = new AndroidJavaClass(WriterCls);
        _buffer = _bridge.CallStatic<AndroidJavaObject>("getBuffer");
        IntPtr raw = _buffer.GetRawObject();
        Address = (IntPtr)AndroidJNI.GetDirectBufferAddress(raw);
        Capacity = (int)AndroidJNI.GetDirectBufferCapacity(raw);
        _proxy = new Proxy();
        _bridge.CallStatic("setListener", _proxy);
        return Address != IntPtr.Zero && Capacity > 0;
    }

    public void StartWriter(int mode) { _writer.CallStatic("start", mode); }
    public void StopWriter() { _writer.CallStatic("stop"); }

    public byte[] GetCopyBaseline() { return _bridge.CallStatic<byte[]>("getDataShareMemory"); }
    public long JavaAddress() { return _bridge.CallStatic<long>("getAddressShareMemory"); }
    public int JavaLength() { return _bridge.CallStatic<int>("getLenghtShareMemory"); }
    public byte[] GetLastSent() { return _writer.CallStatic<byte[]>("getLastSent"); }

    public void Dispose()
    {
        try { _bridge?.CallStatic("setListener", (AndroidJavaProxy)null); } catch { }
        _buffer?.Dispose(); _bridge?.Dispose(); _writer?.Dispose();
    }
}
#endif