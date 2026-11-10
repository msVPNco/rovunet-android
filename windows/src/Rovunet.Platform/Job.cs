using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Rovunet.Core;

namespace Rovunet.Platform;
public sealed class ProcessJob : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct BasicLimit { public long ProcessTime, JobTime; public uint Flags; public UIntPtr Min, Max; public uint ActiveProcesses; public UIntPtr Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Counters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimit { public BasicLimit Basic; public Counters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(SafeFileHandle job, int info, ref ExtendedLimit value, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    readonly SafeFileHandle handle;
    public ProcessJob()
    {
        handle = CreateJobObjectW(IntPtr.Zero, null); if (handle.IsInvalid) throw new SafeError("JOB_FAILED");
        var limit = new ExtendedLimit { Basic = new() { Flags = 0x2000 } };
        if (!SetInformationJobObject(handle, 9, ref limit, (uint)Marshal.SizeOf<ExtendedLimit>())) { handle.Dispose(); throw new SafeError("JOB_FAILED"); }
    }
    public void Assign(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new SafeError("JOB_FAILED"); }
    public void Dispose() => handle.Dispose();
}
