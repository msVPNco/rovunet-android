using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Rovunet.Core;

namespace Rovunet.Platform;
// Same-user unprivileged Agent IPC. A future elevated broker needs its own
// authenticated protocol and must not inherit this trust model by assumption.
public static class PipePeer28
{
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint processId);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint processId);
    public static void RequireClient(SafePipeHandle pipe,int expected){if(expected<=0||!GetNamedPipeClientProcessId(pipe,out uint actual)||actual!=expected)throw new SafeError("INVALID_MESSAGE");}
    public static void RequireServer(SafePipeHandle pipe,int expected){if(expected<=0||!GetNamedPipeServerProcessId(pipe,out uint actual)||actual!=expected)throw new SafeError("INVALID_MESSAGE");}
}
