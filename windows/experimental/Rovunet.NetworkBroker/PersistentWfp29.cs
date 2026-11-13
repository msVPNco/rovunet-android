// Native layouts/constants follow the Windows SDK and Xray-core a4a3dff5
// proxy/tun/tun_windows_wfp.go (MPL-2.0). This implementation owns a distinct
// sublayer and persistent filter keys; Xray's dynamic session is not reused.
using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;
[StructLayout(LayoutKind.Sequential)]internal struct WfpBlob29{public uint Length;public IntPtr Data;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpValue29{public uint Type;public UIntPtr Value;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpDisplay29{public IntPtr Name,Description;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpSession29{public Guid Key;public WfpDisplay29 Display;public uint Flags,Wait,Pid;public IntPtr Sid,User;public int Kernel;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpSublayer29{public Guid Key;public WfpDisplay29 Display;public uint Flags;public IntPtr Provider;public WfpBlob29 Data;public ushort Weight;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpCondition29{public Guid Field;public uint Match;public WfpValue29 Value;}
[StructLayout(LayoutKind.Sequential)]internal struct WfpAction29{public uint Type;public Guid FilterType;}
[StructLayout(LayoutKind.Explicit,Size=200)]internal struct WfpFilter29
{
    [FieldOffset(0)]public Guid Key;[FieldOffset(16)]public WfpDisplay29 Display;[FieldOffset(32)]public uint Flags;
    [FieldOffset(40)]public IntPtr Provider;[FieldOffset(48)]public WfpBlob29 Data;[FieldOffset(64)]public Guid Layer;
    [FieldOffset(80)]public Guid Sublayer;[FieldOffset(96)]public WfpValue29 Weight;[FieldOffset(112)]public uint Count;
    [FieldOffset(120)]public IntPtr Conditions;[FieldOffset(128)]public WfpAction29 Action;[FieldOffset(152)]public Guid Context;
    [FieldOffset(168)]public IntPtr Reserved;[FieldOffset(176)]public ulong Id;[FieldOffset(184)]public WfpValue29 EffectiveWeight;
}
internal sealed class NativeMemory29:IDisposable
{
    readonly List<IntPtr> allocated=[];
    public IntPtr Bytes(byte[] bytes){var p=Marshal.AllocHGlobal(bytes.Length);allocated.Add(p);Marshal.Copy(bytes,0,p,bytes.Length);return p;}
    public IntPtr Text(string value){var p=Marshal.StringToHGlobalUni(value);allocated.Add(p);return p;}
    public IntPtr Value<T>(T value)where T:struct{var p=Marshal.AllocHGlobal(Marshal.SizeOf<T>());allocated.Add(p);Marshal.StructureToPtr(value,p,false);return p;}
    public IntPtr Array<T>(IReadOnlyList<T> values)where T:struct{if(values.Count==0)return IntPtr.Zero;int size=Marshal.SizeOf<T>();var p=Marshal.AllocHGlobal(checked(size*values.Count));allocated.Add(p);for(int i=0;i<values.Count;i++)Marshal.StructureToPtr(values[i],p+i*size,false);return p;}
    public void Dispose(){foreach(var p in allocated)Marshal.FreeHGlobal(p);}
}
internal sealed class PersistentWfp29:IDisposable
{
    internal static readonly Guid Sublayer=new("8d2fd6ca-99db-4675-90ef-57e9d073f4df");
    static readonly Guid[] Layers=[new("c38d57d1-05a7-4c33-904f-7fbceee60e82"),new("e1cd9fe7-f4b5-4273-96c0-592e487b8650"),new("4a72393b-319f-44bc-84c3-ba54dcb3b6b4"),new("a3b42c97-9f04-4672-b87e-cee9c483257f")];
    static readonly Guid Flags=new("632ce23b-5167-435c-86d7-e903684aa80c"),LocalInterface=new("4cd62a49-59c3-4969-b7f3-bda5d32890a4"),NextInterface=new("93ae8f5b-7f6f-4719-98c8-14e97429ef04"),ArrivalInterface=new("618a9b6d-386b-4136-ad6e-b51587cfb1cd"),Protocol=new("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7"),LocalPort=new("0c1ba1af-5765-453f-af22-a8f791ac775b"),RemotePort=new("c35a604d-d22b-4e1a-91b4-68f674ee674b"),RemoteAddress=new("b235ae9a-1d64-49b8-a44c-5ff3d9095045"),AppId=new("d78e1e87-8644-4ea5-9437-d809ecefc971");
    internal static Dictionary<string,Guid> SdkIdentifiers()=>new(){["Connect4"]=Layers[0],["Recv4"]=Layers[1],["Connect6"]=Layers[2],["Recv6"]=Layers[3],["Flags"]=Flags,["LocalInterface"]=LocalInterface,["NextInterface"]=NextInterface,["ArrivalInterface"]=ArrivalInterface,["Protocol"]=Protocol,["LocalPort"]=LocalPort,["RemotePort"]=RemotePort,["RemoteAddress"]=RemoteAddress,["AppId"]=AppId};
    const uint NotFound=0x80320003,SublayerNotFound=0x80320007,AlreadyExists=0x80320009;
    IntPtr engine;
    readonly bool dynamicSession;
    [DllImport("fwpuclnt.dll")]static extern uint FwpmEngineOpen0(IntPtr server,uint auth,IntPtr identity,ref WfpSession29 session,out IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmEngineClose0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionBegin0(IntPtr engine,uint flags);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionCommit0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionAbort0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmSubLayerAdd0(IntPtr engine,ref WfpSublayer29 layer,IntPtr security);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmSubLayerGetByKey0(IntPtr engine,ref Guid key,out IntPtr layer);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmSubLayerDeleteByKey0(IntPtr engine,ref Guid key);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterAdd0(IntPtr engine,ref WfpFilter29 filter,IntPtr security,out ulong id);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterGetByKey0(IntPtr engine,ref Guid key,out IntPtr filter);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterDeleteByKey0(IntPtr engine,ref Guid key);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterCreateEnumHandle0(IntPtr engine,IntPtr template,out IntPtr handle);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterEnum0(IntPtr engine,IntPtr handle,uint requested,out IntPtr entries,out uint returned);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterDestroyEnumHandle0(IntPtr engine,IntPtr handle);
    [DllImport("fwpuclnt.dll",CharSet=CharSet.Unicode)]static extern uint FwpmGetAppIdFromFileName0(string path,out IntPtr appId);
    [DllImport("fwpuclnt.dll")]static extern void FwpmFreeMemory0(ref IntPtr memory);
    static void Require(uint status,string? check=null){if(status!=0){var error=new SafeError("BROKER_WFP_FAILED");error.Data["nativeStatus"]=status;if(check!=null)error.Data["check"]=check;throw error;}}
    public PersistentWfp29(bool dynamicSession=false){this.dynamicSession=dynamicSession;var session=new WfpSession29{Flags=dynamicSession?1u:0u,Wait=5000};Require(FwpmEngineOpen0(IntPtr.Zero,10,IntPtr.Zero,ref session,out engine));}
    public static Guid Key(Guid session,string label)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("Rovunet/network/v1/"+session.ToString("N")+"/"+label)).AsSpan(0,16));
    public static string[] Labels(int engines,IPAddress[] peers,bool withTun)
    {
        var labels=new List<string>();for(int layer=0;layer<4;layer++){
            labels.AddRange(new[]{"loopback/"+layer,"dhcp/"+layer,"block/"+layer});
            if(layer>=2)foreach(int type in new[]{130,131,132,133,134,135,136,137,143})labels.Add("nd/"+layer+"/"+type);
            if(withTun)labels.Add("tun/"+layer);
            for(int app=0;app<engines;app++)for(int peer=0;peer<peers.Length;peer++)if((layer<2)==(peers[peer].AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork))labels.Add("peer/"+layer+"/"+app+"/"+peer);
        }return labels.ToArray();
    }
    static WfpCondition29 Scalar(Guid field,uint type,ulong value,uint match=0)=>new(){Field=field,Match=match,Value=new(){Type=type,Value=(UIntPtr)value}};
    static WfpCondition29 Pointer(Guid field,uint type,IntPtr value)=>new(){Field=field,Value=new(){Type=type,Value=(UIntPtr)(nuint)value}};
    void EnsureSublayer(NativeMemory29 memory)
    {
        var key=Sublayer;uint result=FwpmSubLayerGetByKey0(engine,ref key,out var ptr);
        if(result==0){try{var found=Marshal.PtrToStructure<WfpSublayer29>(ptr);if(found.Flags!=1||Marshal.PtrToStringUni(found.Display.Name)!="Rovunet network protection")throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");return;}finally{FwpmFreeMemory0(ref ptr);}}
        var layer=new WfpSublayer29{Key=Sublayer,Display=new(){Name=memory.Text("Rovunet network protection")},Flags=1,Weight=0xfffe};Require(FwpmSubLayerAdd0(engine,ref layer,IntPtr.Zero));
    }
    IntPtr App(NativeMemory29 memory,string path){Require(FwpmGetAppIdFromFileName0(path,out var p));try{var blob=Marshal.PtrToStructure<WfpBlob29>(p);if(blob.Length is 0 or >32768)throw new SafeError("BROKER_WFP_FAILED");var bytes=new byte[blob.Length];Marshal.Copy(blob.Data,bytes,0,bytes.Length);return memory.Value(new WfpBlob29{Length=blob.Length,Data=memory.Bytes(bytes)});}finally{FwpmFreeMemory0(ref p);}}
    WfpCondition29 Address(NativeMemory29 memory,IPAddress ip){var bytes=ip.GetAddressBytes();if(bytes.Length==16)return Pointer(RemoteAddress,11,memory.Bytes(bytes));return Pointer(RemoteAddress,0x100,memory.Value(new V4Mask29{Address=BinaryPrimitives.ReadUInt32BigEndian(bytes),Mask=uint.MaxValue}));}
    [StructLayout(LayoutKind.Sequential)]struct V4Mask29{public uint Address,Mask;}
    public void Install(Guid session,string[] applications,IPAddress[] peers,ulong tunLuid)
    {
        using var memory=new NativeMemory29();Require(FwpmTransactionBegin0(engine,0));bool committed=false;
        try{
            EnsureSublayer(memory);var appIds=applications.Select(path=>App(memory,path)).ToArray();
            void Add(int layer,string label,uint action,byte weight,params WfpCondition29[] conditions){
                var key=Key(session,label);uint exists=FwpmFilterGetByKey0(engine,ref key,out var prior);
                if(exists==0){try{if(Marshal.PtrToStructure<WfpFilter29>(prior).Sublayer!=Sublayer)throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");return;}finally{FwpmFreeMemory0(ref prior);}}
                if(exists!=NotFound)Require(exists);
                // UINT8 is a 0..15 weight-range index, not an arbitrary numeric
                // weight. Use an explicit UINT64 for the intended ordering.
                var filter=new WfpFilter29{Key=key,Display=new(){Name=memory.Text("Rovunet network "+label.Split('/')[0])},Flags=dynamicSession?0u:1u,Layer=Layers[layer],Sublayer=Sublayer,Weight=new(){Type=4,Value=(UIntPtr)(nuint)memory.Value((ulong)weight)},Count=(uint)conditions.Length,Conditions=memory.Array(conditions),Action=new(){Type=action}};
                Require(FwpmFilterAdd0(engine,ref filter,IntPtr.Zero,out _),"filter-"+label);
            }
            for(int layer=0;layer<4;layer++){
                Add(layer,"loopback/"+layer,0x1002,20,Scalar(Flags,3,1,6));
                Add(layer,"dhcp/"+layer,0x1002,20,Scalar(Protocol,1,17),Scalar(LocalPort,2,layer<2?68u:546u),Scalar(RemotePort,2,layer<2?67u:547u));
                if(layer>=2)foreach(int type in new[]{130,131,132,133,134,135,136,137,143})Add(layer,"nd/"+layer+"/"+type,0x1002,20,Scalar(Protocol,1,58),Scalar(LocalPort,2,(uint)type),Scalar(RemotePort,2,0));
                if(tunLuid!=0){var luid=memory.Value(tunLuid);Add(layer,"tun/"+layer,0x1002,18,Pointer(LocalInterface,4,luid),Pointer(layer%2==0?NextInterface:ArrivalInterface,4,luid));}
                for(int app=0;app<appIds.Length;app++)for(int peer=0;peer<peers.Length;peer++)if((layer<2)==(peers[peer].AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork))Add(layer,"peer/"+layer+"/"+app+"/"+peer,0x1002,19,Pointer(AppId,12,appIds[app]),Address(memory,peers[peer]));
                Add(layer,"block/"+layer,0x1001,0);
            }
            Require(FwpmTransactionCommit0(engine));committed=true;
        }finally{if(!committed)FwpmTransactionAbort0(engine);}
    }
    public bool AllPresent(Guid[] keys)
    {
        foreach(var entry in keys){var key=entry;uint result=FwpmFilterGetByKey0(engine,ref key,out var ptr);if(result==NotFound)return false;Require(result);try{if(Marshal.PtrToStructure<WfpFilter29>(ptr).Sublayer!=Sublayer)throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");}finally{FwpmFreeMemory0(ref ptr);}}return keys.Length>0;
    }
    public Guid[] OwnedKeys()
    {
        Require(FwpmFilterCreateEnumHandle0(engine,IntPtr.Zero,out var cursor));var keys=new List<Guid>();
        try{int seen=0;while(true){Require(FwpmFilterEnum0(engine,cursor,256,out var entries,out uint count));try{
            if(count>256||(seen+=checked((int)count))>65536)throw new SafeError("BROKER_WFP_ENUMERATION_LIMIT");
            for(int i=0;i<count;i++){var filter=Marshal.PtrToStructure<WfpFilter29>(Marshal.ReadIntPtr(entries,i*IntPtr.Size));if(filter.Sublayer==Sublayer)keys.Add(filter.Key);}
            if(count<256)return keys.ToArray();
        }finally{if(entries!=IntPtr.Zero)FwpmFreeMemory0(ref entries);}}}finally{FwpmFilterDestroyEnumHandle0(engine,cursor);}
    }
    public void Remove(Guid[] keys)
    {
        Require(FwpmTransactionBegin0(engine,0));bool committed=false;
        try{foreach(var item in keys){var key=item;uint status=FwpmFilterGetByKey0(engine,ref key,out var ptr);if(status==NotFound)continue;Require(status);try{if(Marshal.PtrToStructure<WfpFilter29>(ptr).Sublayer!=Sublayer)throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");}finally{FwpmFreeMemory0(ref ptr);}Require(FwpmFilterDeleteByKey0(engine,ref key));}Require(FwpmTransactionCommit0(engine));committed=true;}finally{if(!committed)FwpmTransactionAbort0(engine);}
    }
    public void Dispose(){if(engine!=IntPtr.Zero){FwpmEngineClose0(engine);engine=IntPtr.Zero;}} // Baseline persists; dynamic TUN permits expire with this handle/process.
    static bool MatchesBoot(WfpFilter29 filter,Guid sublayer,BootFilterSpec spec)=>filter.Key==spec.Key&&filter.Sublayer==sublayer&&filter.Layer==spec.Layer&&filter.Flags==spec.Flags&&filter.Count==0&&filter.Action.Type==0x1001;
    public bool BootRuntimePresent(Guid installation)
    {
        foreach(var spec in BootPolicyPlan.Filters(installation).Where(s=>s.Phase=="runtime")){
            var key=spec.Key;uint result=FwpmFilterGetByKey0(engine,ref key,out var ptr);if(result==NotFound)return false;Require(result);
            try{if(!MatchesBoot(Marshal.PtrToStructure<WfpFilter29>(ptr),BootPolicyPlan.Sublayer(installation),spec))throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");}finally{FwpmFreeMemory0(ref ptr);}
        }return true;
    }
    public void BootBaseline(Guid installation,bool remove,bool runtimeOnly=false)
    {
        if(dynamicSession)throw new SafeError("BROKER_BOOT_RECEIPT_INVALID");
        Guid sublayer=BootPolicyPlan.Sublayer(installation);var specs=BootPolicyPlan.Filters(installation).Where(s=>!runtimeOnly||s.Phase=="runtime");
        using var memory=new NativeMemory29();Require(FwpmTransactionBegin0(engine,0));bool committed=false;
        try {
            uint found=FwpmSubLayerGetByKey0(engine,ref sublayer,out var existing);
            if(found==0){try{var layer=Marshal.PtrToStructure<WfpSublayer29>(existing);if(layer.Key!=sublayer||layer.Flags!=1||Marshal.PtrToStringUni(layer.Display.Name)!="Rovunet boot baseline")throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");}finally{FwpmFreeMemory0(ref existing);}}
            else if(found==SublayerNotFound&&!remove){var layer=new WfpSublayer29{Key=sublayer,Display=new(){Name=memory.Text("Rovunet boot baseline")},Flags=1,Weight=0xfffd};Require(FwpmSubLayerAdd0(engine,ref layer,IntPtr.Zero));}
            else if(found!=SublayerNotFound)Require(found);
            foreach(var spec in specs){
                var key=spec.Key;uint status=FwpmFilterGetByKey0(engine,ref key,out var ptr);
                if(status==0){try{if(!MatchesBoot(Marshal.PtrToStructure<WfpFilter29>(ptr),sublayer,spec))throw new SafeError("BROKER_WFP_OWNERSHIP_CONFLICT");}finally{FwpmFreeMemory0(ref ptr);}if(remove)Require(FwpmFilterDeleteByKey0(engine,ref key));continue;}
                if(status!=NotFound)Require(status);if(remove)continue;
                var filter=new WfpFilter29{Key=spec.Key,Display=new(){Name=memory.Text("Rovunet "+spec.Phase+" baseline")},Flags=spec.Flags,Layer=spec.Layer,Sublayer=sublayer,Weight=new(){Type=4,Value=(UIntPtr)(nuint)memory.Value(0UL)},Action=new(){Type=0x1001}};
                Require(FwpmFilterAdd0(engine,ref filter,IntPtr.Zero,out _),"boot-baseline");
            }
            if(remove&&!runtimeOnly){uint deleted=FwpmSubLayerDeleteByKey0(engine,ref sublayer);if(deleted!=SublayerNotFound)Require(deleted);}
            Require(FwpmTransactionCommit0(engine));committed=true;
        }finally{if(!committed)FwpmTransactionAbort0(engine);}
    }
}
