using System.Diagnostics;
using UnityEngine;

public class TestScreen : MonoBehaviour
{
    IBridgeSource _src; ByteBridgeReader _reader;
    byte[] _buf = new byte[1024 * 1024];
    int _lastNotify, _reads, _retries, _errors, _lastSeq, _lastLen;
    uint _crcHdr, _crcCalc; bool _ok = true;
    double _readMs, _copyMs; string _info = "";

    string _hexView = "", _hexSent = "";
    long _jAddr; int _jLen; string _jData = "";
    float _lastGetters = -10f;

    static string Hex(byte[] b, int n)
    {
        if (b == null) return "(null)";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n && i < b.Length; i++) sb.Append(b[i].ToString("X2")).Append(' ');
        return sb.ToString();
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _src = new AndroidBridgeSource();
#else
        _src = new MockBridgeSource();
#endif
        bool ok = _src.Init();
        _info = _src.Name + " | addr=0x" + _src.Address.ToInt64().ToString("X")
              + " cap=" + _src.Capacity + (ok ? "" : "  <-- LỖI địa chỉ");
        if (ok) _reader = new ByteBridgeReader(_src.Address, _src.Capacity);
        UnityEngine.Debug.Log("[Bridge] " + _info);
    }

    void Update()
    {
        if (_reader == null) return;
        int n = _src.NotifyCount;
        if (n == _lastNotify) return;          // chỉ đọc khi Java báo có data mới
        _lastNotify = n;

        var sw = Stopwatch.StartNew();
        int len = 0, seq = 0; uint crcH = 0; bool got = false;
        for (int i = 0; i < 5 && !got; i++)
        {
            got = _reader.TryRead(_buf, out len, out seq, out crcH);
            if (!got) _retries++;
        }
        if (!got) return;
        sw.Stop(); _readMs = sw.Elapsed.TotalMilliseconds;

        uint calc = Crc32.Compute(_buf, len);
        _ok = calc == crcH;                    // dữ liệu random: chỉ dùng CRC để kiểm chứng
        if (!_ok) _errors++;
        _reads++; _lastSeq = seq; _lastLen = len; _crcHdr = crcH; _crcCalc = calc;
        _hexView = Hex(_buf, 16);

        // Gọi JNI (có COPY) tối đa 2 lần/giây để STRESS không bị nặng
        if (Time.realtimeSinceStartup - _lastGetters > 0.5f)
        {
            _lastGetters = Time.realtimeSinceStartup;
            ReadThreeGetters();
        }
    }

    void ReadThreeGetters()
    {
        _hexSent = Hex(_src.GetLastSent(), 16);
        _jAddr = _src.JavaAddress();
        _jLen = _src.JavaLength();
        byte[] d = _src.GetCopyBaseline();
        _jData = "len=" + d.Length + "  " + Hex(d, 16);
    }

    void MeasureCopy()
    {
        var sw = Stopwatch.StartNew();
        byte[] b = _src.GetCopyBaseline();
        sw.Stop(); _copyMs = sw.Elapsed.TotalMilliseconds;
        bool same = b.Length == _lastLen;
        for (int i = 0; same && i < b.Length; i++) if (b[i] != _buf[i]) same = false;
        UnityEngine.Debug.Log("[Bridge] baseline copy len=" + b.Length + " same=" + same);
        ReadThreeGetters();
    }

    void OnGUI()
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
        var bt = new GUIStyle(GUI.skin.button) { fontSize = 26 };
        GUILayout.BeginArea(new Rect(20, 60, Screen.width - 40, Screen.height - 80));

        GUILayout.Label(_info, st);
        GUILayout.Label("magic ok = " + (_reader != null && _reader.MagicOk)
            + "   seq(poll) = " + (_reader?.PeekSeq() ?? 0), st);
        GUILayout.Label($"notify={_src?.NotifyCount}  reads={_reads}  retry={_retries}  LỖI={_errors}", st);
        GUILayout.Label($"seq={_lastSeq} len={_lastLen} crc {_crcHdr:X8}/{_crcCalc:X8} " + (_ok ? "KHỚP" : "SAI"), st);
        GUILayout.Label($"đọc view: {_readMs:F3} ms   copy baseline: {_copyMs:F3} ms", st);

        GUILayout.Label("--- Java gửi vs C# đọc ---", st);
        GUILayout.Label("Java gửi (getLastSent): " + _hexSent, st);
        GUILayout.Label("C# đọc (con trỏ)      : " + _hexView, st);
        GUILayout.Label("So sánh 16B đầu: " + (_hexSent == _hexView ? "GIỐNG" : "KHÁC"), st);

        long cAddr = _src != null ? _src.Address.ToInt64() : 0;
        GUILayout.Label("--- 3 getter của Java ---", st);
        GUILayout.Label("getAddressShareMemory: 0x" + _jAddr.ToString("X")
            + (_jAddr == 0 ? "  (reflection bị chặn)" : (_jAddr == cAddr ? "  = JNI  OK" : "  KHÁC JNI")), st);
        GUILayout.Label("C# lấy qua JNI       : 0x" + cAddr.ToString("X"), st);
        GUILayout.Label("getLenghtShareMemory : " + _jLen
            + (_jLen == _lastLen ? "  = len C# đọc OK" : "  KHÁC"), st);
        GUILayout.Label("getDataShareMemory   : " + _jData, st);

        if (GUILayout.Button("Start 64KB / 5s", bt)) _src.StartWriter(0);
        if (GUILayout.Button("Start 1MB / 5s", bt)) _src.StartWriter(1);
        if (GUILayout.Button("Start STRESS", bt)) _src.StartWriter(2);
        if (GUILayout.Button("Stop", bt)) _src.StopWriter();
        if (GUILayout.Button("Đối chiếu + đo baseline copy", bt)) MeasureCopy();
        GUILayout.EndArea();
    }

    void OnDestroy() { _src?.Dispose(); }
}