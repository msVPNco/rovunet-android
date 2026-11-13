using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;
internal static class LocalListener29
{
    static SafeError Rejected(string check){var error=new SafeError("BROKER_LISTENER_REJECTED");error.Data["check"]=check;return error;}
    [DllImport("iphlpapi.dll")]static extern uint GetExtendedTcpTable(IntPtr table,ref uint bytes,bool ordered,uint family,int kind,uint reserved);
    [DllImport("ntdll.dll")]static extern int NtQueryInformationProcess(IntPtr process,int info,IntPtr result,int length,out int returned);
    public static int[] Owners(int port)
    {
        uint bytes=0;uint status=GetExtendedTcpTable(IntPtr.Zero,ref bytes,false,2,3,0);if(status!=122||bytes is <4 or >4*1024*1024)throw Rejected("listener-table-size");
        var memory=Marshal.AllocHGlobal((int)bytes);try{if(GetExtendedTcpTable(memory,ref bytes,false,2,3,0)!=0)throw new SafeError("BROKER_LISTENER_REJECTED");int count=Marshal.ReadInt32(memory);if(count<0||4L+count*24L>bytes)throw new SafeError("BROKER_LISTENER_REJECTED");var owners=new List<int>();for(int i=0;i<count;i++){var row=memory+4+i*24;uint address=(uint)Marshal.ReadInt32(row,4),rawPort=(uint)Marshal.ReadInt32(row,8);if(address==0x0100007f&&BinaryPrimitives.ReverseEndianness((ushort)rawPort)==port)owners.Add(Marshal.ReadInt32(row,20));}return owners.Distinct().ToArray();}finally{Marshal.FreeHGlobal(memory);}
    }
    static int Parent(Process process){var p=Marshal.AllocHGlobal(48);try{if(NtQueryInformationProcess(process.Handle,0,p,48,out int size)!=0||size!=48)throw Rejected("parent-query");return checked((int)Marshal.ReadIntPtr(p,40));}finally{Marshal.FreeHGlobal(p);}}
    public static int Require(int port,string root,string owner)
    {
        var ids=Owners(port);if(ids.Length!=1)throw Rejected("listener-count");using var native=Process.GetProcessById(ids[0]);if(!string.Equals(native.MainModule?.FileName,Path.Combine(root,"engines","xray.exe"),StringComparison.OrdinalIgnoreCase)||TrustedInstallation29.Sid(native)!=owner)throw Rejected("engine-identity");
        using var parent=Process.GetProcessById(Parent(native));if(TrustedInstallation29.Sid(parent)!=owner)throw Rejected("parent-sid");string image=parent.MainModule?.FileName??"";
        if(image.Equals(Path.Combine(root,"Rovunet.NetworkLab.exe"),StringComparison.OrdinalIgnoreCase))return ids[0];
        if(!image.Equals(Path.Combine(root,"Rovunet.Agent.exe"),StringComparison.OrdinalIgnoreCase))throw Rejected("parent-image");
        using var gui=Process.GetProcessById(Parent(parent));string controller=gui.MainModule?.FileName??"";
        // This assembly is VM-only and RequireLab has already validated the
        // machine-bound marker. A protected lab controller can exercise the
        // real Agent without enabling networking in the consumer UI.
        if(!(controller.Equals(Path.Combine(root,"Rovunet.exe"),StringComparison.OrdinalIgnoreCase)||controller.Equals(Path.Combine(root,"Rovunet.NetworkLab.exe"),StringComparison.OrdinalIgnoreCase))||TrustedInstallation29.Sid(gui)!=owner)throw new SafeError("BROKER_LISTENER_REJECTED");
        return ids[0];
    }
}
