using System;
using System.Runtime.InteropServices;
using System.Threading;

public unsafe class MockBridgeSource : IBridgeSource
{
    const int Header = 16, Max = 1024 * 1024;
    IntPtr _mem; Thread _t; volatile bool _run; int _notify; int _seq;
    volatile byte[] _last = new byte[0];

    public IntPtr Address => _mem;
    public int Capacity => Header + Max;
    public int NotifyCount => Volatile.Read(ref _notify);
    public string Name => "Mock (PC)";

    public bool Init()
    {
        _mem = Marshal.AllocHGlobal(Header + Max);
        byte* p = (byte*)_mem;
        for (int i = 0; i < Header; i++) p[i] = 0;
        *(int*)p = unchecked((int)0xCAFE0001);
        return true;
    }

    public void StartWriter(int mode)
    {
        StopWriter();
        int size = mode == 0 ? 64 * 1024 : Max;
        int sleep = mode == 2 ? 0 : 5000;
        _run = true;
        _t = new Thread(() =>
        {
            var data = new byte[size];
            var rnd = new Random();
            while (_run)
            {
                rnd.NextBytes(data);                                 // dữ liệu ngẫu nhiên
                var last = new byte[Math.Min(size, 64)];
                Array.Copy(data, last, last.Length);
                _last = last;

                uint crc = Crc32.Compute(data, size);
                byte* p = (byte*)_mem;
                _seq++; Volatile.Write(ref *(int*)(p + 4), _seq);    // lẻ: đang ghi
                Thread.MemoryBarrier();
                *(int*)(p + 8) = size; *(uint*)(p + 12) = crc;
                Marshal.Copy(data, 0, _mem + Header, size);
                Thread.MemoryBarrier();
                _seq++; Volatile.Write(ref *(int*)(p + 4), _seq);    // chẵn: xong
                Interlocked.Increment(ref _notify);
                if (sleep > 0) Thread.Sleep(sleep);
            }
        })
        { IsBackground = true };
        _t.Start();
    }

    public void StopWriter() { _run = false; _t?.Join(500); _t = null; }

    public byte[] GetCopyBaseline()
    {
        byte* p = (byte*)_mem; int len = *(int*)(p + 8);
        var b = new byte[len]; Marshal.Copy(_mem + Header, b, 0, len); return b;
    }

    public long JavaAddress() { return _mem.ToInt64(); }
    public int JavaLength() { byte* p = (byte*)_mem; return *(int*)(p + 8); }
    public byte[] GetLastSent() { return _last; }

    public void Dispose() { StopWriter(); if (_mem != IntPtr.Zero) Marshal.FreeHGlobal(_mem); _mem = IntPtr.Zero; }
}