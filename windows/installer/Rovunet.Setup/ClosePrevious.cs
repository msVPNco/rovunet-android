using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace Rovunet.Setup;
internal static class ClosePrevious
{
    delegate bool EnumWindow(IntPtr hwnd,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback,IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int length);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
    internal static async Task Run(string destination)
    {
        if(!InstallTransaction.IsUpgrade(destination))return;
        string target=Path.GetFullPath(Path.Combine(destination,"Rovunet.exe"));
        foreach(var process in Process.GetProcessesByName("Rovunet"))using(process){
            string executable;try{executable=process.MainModule?.FileName??"";}catch{throw new InstallProblem("appRunning");}
            if(!Path.GetFullPath(executable).Equals(target,StringComparison.OrdinalIgnoreCase))continue;
            // Query-end-session reaches WPF SessionEnding for cooperative,
            // bounded owned-Agent recovery and exit. No forced process kill.
            var windows=new List<IntPtr>();EnumWindows((hwnd,_)=>{GetWindowThreadProcessId(hwnd,out uint id);if(id==process.Id)windows.Add(hwnd);return true;},IntPtr.Zero);
            foreach(var hwnd in windows)SendMessageTimeout(hwnd,0x11,IntPtr.Zero,new IntPtr(1),2,4000,out _);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try{await process.WaitForExitAsync(deadline.Token);}catch(OperationCanceledException){throw new InstallProblem("appRunning");}
        }
    }
}

