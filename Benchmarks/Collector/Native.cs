using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class Native
{
    public static Dictionary<int, int> Parents()
    {
        var result = new Dictionary<int, int>();
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) return result;
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (Process32FirstW(snapshot, ref entry)) do { result[(int)entry.Pid] = (int)entry.ParentPid; } while (Process32NextW(snapshot, ref entry));
        return result;
    }
    public static long? PrivateWorkingSet(IntPtr process)
    {
        var memory = new MemoryCounters { Size = (uint)Marshal.SizeOf<MemoryCounters>() };
        return GetProcessMemoryInfo(process, ref memory, memory.Size) && memory.PrivateWorkingSet != UIntPtr.Zero ? checked((long)memory.PrivateWorkingSet) : null;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr DefaultHeap; public uint Module, Threads, ParentPid; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCounters
    {
        public uint Size, PageFaults;
        public UIntPtr PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool, PeakNonPagedPool, NonPagedPool, Pagefile, PeakPagefile, PrivateUsage, PrivateWorkingSet;
        public ulong SharedCommit;
    }
    [DllImport("kernel32.dll")] private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);
}
