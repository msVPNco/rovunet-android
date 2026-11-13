using System.Runtime.InteropServices;
using System.Text;
using Rovunet.Core;

namespace Rovunet.Platform;

// SystemConfiguration performs the privileged write through its authorization
// service. The GUI, Agent, engines and guardian remain ordinary user processes.
public sealed class MacProxyPreferences : IProxyPreferences
{
    const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",SC="/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration",SEC="/System/Library/Frameworks/Security.framework/Security";
    nint authorization,preferences;
    readonly bool isolated;
    // This managed preferences object never accepts paths from a service RPC.
    // The native test fixture supplies only its own temporary preferences file.
    internal MacProxyPreferences(nint isolatedPreferences)
    {
        if(isolatedPreferences==0)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");
        preferences=isolatedPreferences;isolated=true;
    }
    public MacProxyPreferences(byte[]? externalAuthorization=null)
    {
        if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        authorization=MacProxyAuthorization.Import(externalAuthorization??[]);
        nint name=String("Rovunet system proxy");try{preferences=SCPreferencesCreateWithAuthorization(0,name,0,authorization);if(preferences==0)throw new SafeError("MAC_PROXY_AUTH_DENIED");}catch{Dispose();throw;}finally{CFRelease(name);}
    }
    public void RefreshSnapshot()=>SCPreferencesSynchronize(preferences);
    public IDisposable Begin()
    {
        SCPreferencesSynchronize(preferences);
        if(!SCPreferencesLock(preferences,false))throw Error();
        return new Transaction(this);
    }
    sealed class Transaction(MacProxyPreferences owner):IDisposable{public void Dispose(){SCPreferencesUnlock(owner.preferences);SCPreferencesSynchronize(owner.preferences);}}
    public IReadOnlyList<string> Services()
    {
        var ids=new List<string>();nint current=SCNetworkSetCopyCurrent(preferences);if(current==0)return ids;
        try{nint services=SCNetworkSetCopyServices(current);if(services==0)return ids;try{
            for(nint i=0;i<CFArrayGetCount(services);i++){nint service=CFArrayGetValueAtIndex(services,i);if(!SCNetworkServiceGetEnabled(service))continue;
                nint type=String("Proxies"),protocol;try{protocol=SCNetworkServiceCopyProtocol(service,type);}finally{CFRelease(type);}if(protocol==0)continue;CFRelease(protocol);
                string id=Text(SCNetworkServiceGetServiceID(service));if(Guid.TryParse(id,out _))ids.Add(id);
            }
        }finally{CFRelease(services);}}finally{CFRelease(current);}return ids;
    }
    static nint ServicePath(string id){if(!Guid.TryParse(id,out _))throw new SafeError("MAC_PROXY_SETTINGS_INVALID");return String("/NetworkServices/"+id+"/Proxies");}
    public ProxyMap? Read(string service)
    {
        nint path=ServicePath(service);try{nint value=SCPreferencesPathGetValue(preferences,path);if(value==0)return null;
            nint data=CFPropertyListCreateData(0,value,100,0,out nint error);if(error!=0)CFRelease(error);if(data==0)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");
            try{int length=checked((int)CFDataGetLength(data));if(length>Importer.MaxBytes)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");byte[] bytes=new byte[length];Marshal.Copy(CFDataGetBytePtr(data),bytes,0,length);return ProxyMap.Parse(Encoding.UTF8.GetString(bytes));}finally{CFRelease(data);}
        }finally{CFRelease(path);}
    }
    public void Write(string service,ProxyMap? value)
    {
        nint path=ServicePath(service);try{
            if(value==null){if(SCPreferencesPathGetValue(preferences,path)!=0&&!SCPreferencesPathRemoveValue(preferences,path))throw Error();return;}
            byte[] bytes=Encoding.UTF8.GetBytes(value.Xml());nint data=CFDataCreate(0,bytes,bytes.Length);if(data==0)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");
            try{nint plist=CFPropertyListCreateWithData(0,data,0,0,out nint error);if(error!=0)CFRelease(error);if(plist==0)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");try{if(!SCPreferencesPathSetValue(preferences,path,plist))throw Error();}finally{CFRelease(plist);}}finally{CFRelease(data);}
        }finally{CFRelease(path);}
    }
    public void Commit(){if(!SCPreferencesCommitChanges(preferences)||!isolated&&!SCPreferencesApplyChanges(preferences))throw Error();}
    static SafeError Error()=>new(SCError() switch{1003=>"MAC_PROXY_AUTH_DENIED",1006 or 3002=>"MAC_PROXY_BUSY",_=>"MAC_PROXY_APPLY_FAILED"});
    public void Dispose(){if(preferences!=0){CFRelease(preferences);preferences=0;}if(authorization!=0){MacProxyAuthorization.Release(authorization);authorization=0;}}
    static nint String(string text)=>CFStringCreateWithCString(0,text,0x08000100);
    static string Text(nint value){if(value==0)return "";int size=checked((int)(CFStringGetLength(value)*4+1));byte[] bytes=new byte[size];if(!CFStringGetCString(value,bytes,size,0x08000100))throw new SafeError("MAC_PROXY_SETTINGS_INVALID");return Encoding.UTF8.GetString(bytes,0,Array.IndexOf(bytes,(byte)0));}
    [DllImport(SC)] static extern nint SCPreferencesCreateWithAuthorization(nint allocator,nint name,nint prefsId,nint authorization);
    [DllImport(SC)] static extern void SCPreferencesSynchronize(nint prefs);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesLock(nint prefs,[MarshalAs(UnmanagedType.I1)]bool wait);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesUnlock(nint prefs);
    [DllImport(SC)] static extern nint SCNetworkSetCopyCurrent(nint prefs);
    [DllImport(SC)] static extern nint SCNetworkSetCopyServices(nint set);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCNetworkServiceGetEnabled(nint service);
    [DllImport(SC)] static extern nint SCNetworkServiceGetServiceID(nint service);
    [DllImport(SC)] static extern nint SCNetworkServiceCopyProtocol(nint service,nint type);
    [DllImport(SC)] static extern nint SCPreferencesPathGetValue(nint prefs,nint path);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesPathSetValue(nint prefs,nint path,nint value);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesPathRemoveValue(nint prefs,nint path);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesCommitChanges(nint prefs);
    [DllImport(SC)][return:MarshalAs(UnmanagedType.I1)] static extern bool SCPreferencesApplyChanges(nint prefs);
    [DllImport(SC)] static extern int SCError();
    [DllImport(CF)] static extern nint CFArrayGetCount(nint array);
    [DllImport(CF)] static extern nint CFArrayGetValueAtIndex(nint array,nint index);
    [DllImport(CF)] static extern nint CFStringCreateWithCString(nint allocator,[MarshalAs(UnmanagedType.LPUTF8Str)]string value,uint encoding);
    [DllImport(CF)] static extern nint CFStringGetLength(nint text);
    [DllImport(CF)][return:MarshalAs(UnmanagedType.I1)] static extern bool CFStringGetCString(nint text,byte[] buffer,nint size,uint encoding);
    [DllImport(CF)] static extern nint CFPropertyListCreateData(nint allocator,nint plist,nint format,nuint options,out nint error);
    [DllImport(CF)] static extern nint CFPropertyListCreateWithData(nint allocator,nint data,nuint options,nint format,out nint error);
    [DllImport(CF)] static extern nint CFDataCreate(nint allocator,byte[] bytes,nint length);
    [DllImport(CF)] static extern nint CFDataGetLength(nint data);
    [DllImport(CF)] static extern nint CFDataGetBytePtr(nint data);
    [DllImport(CF)] static extern void CFRelease(nint value);
}
