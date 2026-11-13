using System.Runtime.InteropServices;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;
internal static class NativeDns29
{
    [StructLayout(LayoutKind.Sequential)]struct Settings{public uint Version;public ulong Flags;public IntPtr Domain,Servers,Search;public uint Registration,RegisterName,LLMNR,QueryName;public IntPtr ProfileServers;}
    [DllImport("iphlpapi.dll")]static extern uint SetInterfaceDnsSettings(Guid id,ref Settings settings);
    [DllImport("dnsapi.dll")]static extern bool DnsFlushResolverCache();
    public static void Set(Guid ownedInterface,bool clear=false)
    {
        using var memory=new NativeMemory29();foreach(bool v6 in new[]{false,true}){var settings=new Settings{Version=1,Flags=2UL|(v6?1UL:0UL),Servers=memory.Text(clear?"":v6?"2606:4700:4700::1111":"1.1.1.1")};uint result=SetInterfaceDnsSettings(ownedInterface,ref settings);if(result!=0&&!(clear&&result is 1168 or 2))throw new SafeError(clear?"BROKER_DNS_RESTORE_FAILED":"BROKER_DNS_APPLY_FAILED");}DnsFlushResolverCache();
    }
}
