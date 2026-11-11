using System.Net;
using System.Runtime.InteropServices;

namespace Rovunet.Platform;

// Read the resolver macOS actually publishes, including a deliberately configured
// local DNS stub. No authorization dialog, shell, network request or settings write.
public static class MacNetworkDns
{
    const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string SC="/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration";
    public static string[] Read()
    {
        if(!OperatingSystem.IsMacOS())return [];
        nint name=0,store=0,key=0,value=0,servers=0;
        try{
            name=CFStringCreateWithCString(0,"Rovunet network DNS",0x08000100);store=SCDynamicStoreCreate(0,name,0,0);if(store==0)return [];
            key=CFStringCreateWithCString(0,"State:/Network/Global/DNS",0x08000100);value=SCDynamicStoreCopyValue(store,key);
            if(value==0||CFGetTypeID(value)!=CFDictionaryGetTypeID())return [];
            servers=CFStringCreateWithCString(0,"ServerAddresses",0x08000100);nint array=CFDictionaryGetValue(value,servers);
            if(array==0||CFGetTypeID(array)!=CFArrayGetTypeID())return [];
            var result=new List<string>();
            for(nint i=0;i<CFArrayGetCount(array)&&result.Count<16;i++){
                nint text=CFArrayGetValueAtIndex(array,i);if(CFGetTypeID(text)!=CFStringGetTypeID())continue;
                byte[] bytes=new byte[256];if(!CFStringGetCString(text,bytes,bytes.Length,0x08000100))continue;
                int end=Array.IndexOf(bytes,(byte)0);if(end<0)continue;
                if(IPAddress.TryParse(System.Text.Encoding.UTF8.GetString(bytes,0,end),out var address)&&!address.Equals(IPAddress.Any)&&!address.Equals(IPAddress.IPv6Any))result.Add(new IPEndPoint(address,53).ToString());
            }
            return result.Distinct().ToArray();
        }catch{return [];}finally{foreach(nint item in new[]{servers,value,key,store,name})if(item!=0)CFRelease(item);}
    }
    [DllImport(SC)] static extern nint SCDynamicStoreCreate(nint allocator,nint name,nint callback,nint context);
    [DllImport(SC)] static extern nint SCDynamicStoreCopyValue(nint store,nint key);
    [DllImport(CF)] static extern nint CFStringCreateWithCString(nint allocator,[MarshalAs(UnmanagedType.LPUTF8Str)]string value,uint encoding);
    [DllImport(CF)] static extern nuint CFGetTypeID(nint value);
    [DllImport(CF)] static extern nuint CFDictionaryGetTypeID();
    [DllImport(CF)] static extern nuint CFArrayGetTypeID();
    [DllImport(CF)] static extern nuint CFStringGetTypeID();
    [DllImport(CF)] static extern nint CFDictionaryGetValue(nint dictionary,nint key);
    [DllImport(CF)] static extern nint CFArrayGetCount(nint array);
    [DllImport(CF)] static extern nint CFArrayGetValueAtIndex(nint array,nint index);
    [DllImport(CF)][return:MarshalAs(UnmanagedType.I1)] static extern bool CFStringGetCString(nint value,byte[] bytes,nint size,uint encoding);
    [DllImport(CF)] static extern void CFRelease(nint value);
}
