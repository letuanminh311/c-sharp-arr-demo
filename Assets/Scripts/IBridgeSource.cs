using System;

public interface IBridgeSource : IDisposable
{
    bool Init();                  // true nếu lấy được địa chỉ
    IntPtr Address { get; }       // địa chỉ vùng nhớ (lấy 1 lần qua JNI)
    int Capacity { get; }
    int NotifyCount { get; }      // tăng mỗi lần Java báo có data mới
    void StartWriter(int mode);   // 0=64KB/5s, 1=1MB/5s, 2=stress
    void StopWriter();
    byte[] GetCopyBaseline();     // getDataShareMemory (có COPY, chỉ để đối chiếu)
    long JavaAddress();           // getAddressShareMemory
    int JavaLength();             // getLenghtShareMemory
    byte[] GetLastSent();         // 64 byte đầu Java vừa gửi
    string Name { get; }
}