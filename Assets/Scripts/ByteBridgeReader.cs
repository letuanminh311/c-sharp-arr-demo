using System;
using System.Runtime.InteropServices;
using System.Threading;

public unsafe class ByteBridgeReader
{
    const int Header = 16;
    readonly byte* _p; readonly int _cap;
    public ByteBridgeReader(IntPtr addr, int capacity) { _p = (byte*)addr; _cap = capacity; }

    public bool MagicOk => *(int*)_p == unchecked((int)0xCAFE0001);
    public int PeekSeq() => Volatile.Read(ref *(int*)(_p + 4));

    /// Đọc theo seqlock. Trả true nếu dữ liệu nhất quán.
    public bool TryRead(byte[] dst, out int len, out int seq, out uint crcHeader)
    {
        len = 0; crcHeader = 0;
        int s1 = Volatile.Read(ref *(int*)(_p + 4));
        seq = s1;
        if ((s1 & 1) != 0 || s1 == 0) return false;     // đang ghi hoặc chưa có data

        Thread.MemoryBarrier();
        len = *(int*)(_p + 8);
        crcHeader = *(uint*)(_p + 12);
        if (len < 0 || len > _cap - Header || len > dst.Length) return false;
        Marshal.Copy((IntPtr)(_p + Header), dst, 0, len);

        Thread.MemoryBarrier();
        int s2 = Volatile.Read(ref *(int*)(_p + 4));
        return s1 == s2;
    }
}