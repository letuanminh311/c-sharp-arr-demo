using UnityEngine;

public static class Crc32
{
    static readonly uint[] T = Build();
    static uint[] Build()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    public static uint Compute(byte[] d, int len)
    {
        uint c = 0xFFFFFFFFu;
        for (int i = 0; i < len; i++) c = T[(c ^ d[i]) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
