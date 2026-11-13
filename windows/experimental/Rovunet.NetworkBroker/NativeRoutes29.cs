using System.Net;
using System.Runtime.InteropServices;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;
[StructLayout(LayoutKind.Explicit,Size=28)]internal struct SocketAddress29
{
    [FieldOffset(0)]public ushort Family;
    [FieldOffset(4)]public uint Address4;
    [FieldOffset(8)]public ulong Address6A;
    [FieldOffset(16)]public ulong Address6B;
    [FieldOffset(24)]public uint Scope;
    public static SocketAddress29 From(IPAddress ip){var value=new SocketAddress29{Family=(ushort)(ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?2:23)};var bytes=ip.GetAddressBytes();if(value.Family==2)value.Address4=BitConverter.ToUInt32(bytes);else{value.Address6A=BitConverter.ToUInt64(bytes,0);value.Address6B=BitConverter.ToUInt64(bytes,8);value.Scope=checked((uint)ip.ScopeId);}return value;}
    public IPAddress Ip()=>Family==2?new IPAddress(BitConverter.GetBytes(Address4)):new IPAddress(BitConverter.GetBytes(Address6A).Concat(BitConverter.GetBytes(Address6B)).ToArray(),Scope);
}
[StructLayout(LayoutKind.Explicit,Size=104)]internal struct RouteRow29
{
    [FieldOffset(0)]public ulong Luid;
    [FieldOffset(8)]public uint InterfaceIndex;
    [FieldOffset(12)]public SocketAddress29 Prefix;
    [FieldOffset(40)]public byte PrefixLength;
    [FieldOffset(44)]public SocketAddress29 NextHop;
    [FieldOffset(72)]public byte SitePrefixLength;
    [FieldOffset(76)]public uint ValidLifetime;
    [FieldOffset(80)]public uint PreferredLifetime;
    [FieldOffset(84)]public uint Metric;
    [FieldOffset(88)]public uint Protocol;
    [FieldOffset(92)]public byte Loopback;
    [FieldOffset(93)]public byte Autoconfigure;
    [FieldOffset(94)]public byte Publish;
    [FieldOffset(95)]public byte Immortal;
    [FieldOffset(96)]public uint Age;
    [FieldOffset(100)]public uint Origin;
}
internal sealed record OwnedRoute29(ulong Luid,uint InterfaceIndex,string Prefix,byte PrefixLength,string NextHop,uint Metric,uint Protocol)
{
    public RouteRow29 Row()=>new(){Luid=Luid,InterfaceIndex=InterfaceIndex,Prefix=SocketAddress29.From(IPAddress.Parse(Prefix)),PrefixLength=PrefixLength,NextHop=SocketAddress29.From(IPAddress.Parse(NextHop)),Metric=Metric,Protocol=Protocol,ValidLifetime=uint.MaxValue,PreferredLifetime=uint.MaxValue};
    public bool Same(RouteRow29 row)=>row.Luid==Luid&&row.Prefix.Ip().ToString()==Prefix&&row.PrefixLength==PrefixLength&&row.NextHop.Ip().ToString()==NextHop&&row.Metric==Metric&&row.Protocol==Protocol;
}
internal static class NativeRoutes29
{
    [DllImport("iphlpapi.dll")]static extern uint GetIpForwardTable2(ushort family,out IntPtr table);
    [DllImport("iphlpapi.dll")]static extern void FreeMibTable(IntPtr memory);
    [DllImport("iphlpapi.dll")]static extern uint GetBestRoute2(IntPtr luid,uint index,IntPtr source,ref SocketAddress29 destination,uint options,out RouteRow29 route,out SocketAddress29 bestSource);
    [DllImport("iphlpapi.dll")]static extern uint CreateIpForwardEntry2(ref RouteRow29 row);
    [DllImport("iphlpapi.dll")]static extern uint DeleteIpForwardEntry2(ref RouteRow29 row);
    [DllImport("iphlpapi.dll")]internal static extern uint ConvertInterfaceGuidToLuid(ref Guid guid,out ulong luid);
    public static RouteRow29[] Read()
    {
        if(GetIpForwardTable2(0,out var table)!=0)throw new SafeError("BROKER_ROUTE_READ_FAILED");
        try{int count=Marshal.ReadInt32(table);if(count is <0 or >32768)throw new SafeError("BROKER_ROUTE_READ_FAILED");return Enumerable.Range(0,count).Select(i=>Marshal.PtrToStructure<RouteRow29>(table+8+i*104)).ToArray();}finally{FreeMibTable(table);}
    }
    public static OwnedRoute29 Peer(IPAddress peer)
    {
        var destination=SocketAddress29.From(peer);if(GetBestRoute2(IntPtr.Zero,0,IntPtr.Zero,ref destination,0,out var best,out _)!=0||best.Luid==0)throw new SafeError("BROKER_PHYSICAL_ROUTE_MISSING");
        return new(best.Luid,best.InterfaceIndex,peer.ToString(),(byte)(peer.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?32:128),best.NextHop.Ip().ToString(),1,3);
    }
    public static OwnedRoute29[] Tunnel(ulong luid,uint index)=>[
        new(luid,index,"0.0.0.0",1,"0.0.0.0",1,3),new(luid,index,"128.0.0.0",1,"0.0.0.0",1,3),
        new(luid,index,"::",1,"::",1,3),new(luid,index,"8000::",1,"::",1,3)];
    public static bool Present(OwnedRoute29 expected)=>Read().Any(expected.Same);
    public static bool Conflicts(OwnedRoute29 route)=>Read().Any(r=>r.PrefixLength==route.PrefixLength&&r.Prefix.Ip().ToString()==route.Prefix&&!route.Same(r));
    public static void Add(OwnedRoute29 route){if(Present(route)||Conflicts(route))throw new SafeError("BROKER_ROUTE_OWNERSHIP_CONFLICT");var row=route.Row();if(CreateIpForwardEntry2(ref row)!=0||!Present(route))throw new SafeError("BROKER_ROUTE_APPLY_FAILED");}
    public static void Remove(OwnedRoute29 route){var entries=Read();var current=entries.Where(r=>r.Luid==route.Luid&&r.PrefixLength==route.PrefixLength&&r.Prefix.Ip().ToString()==route.Prefix).ToArray();if(current.Length==0)return;if(current.Length!=1||!route.Same(current[0]))throw new SafeError("BROKER_ROUTE_OWNERSHIP_CONFLICT");var row=current[0];if(DeleteIpForwardEntry2(ref row)!=0)throw new SafeError("BROKER_ROUTE_RESTORE_FAILED");}
}
