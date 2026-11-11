using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Rovunet.Core;

namespace Rovunet.Platform;

// Only the ordinary-user GUI opens this legacy login-Keychain item. Existing
// service/account/key bytes remain unchanged. Children still use private pipes.
public sealed class KeychainVault : ISecretVault, IDisposable
{
    readonly SessionSecretVault session;
    readonly Func<bool> mayCreate;
    public KeychainVault(Func<bool> mayCreate){
        this.mayCreate=mayCreate;
        session=new(()=>ReadKey(false));
    }
    public byte[] GetOrCreateKey()=>session.GetOrCreateKey();
    public void Unlock()=>CryptographicOperations.ZeroMemory(session.Unlock(()=>ReadKey(true)));
    public void Dispose()=>session.Dispose();
    const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string Security="/System/Library/Frameworks/Security.framework/Security";
    static readonly nint cfHandle=OperatingSystem.IsMacOS()?NativeLibrary.Load(CF):0;
    static readonly nint secHandle=OperatingSystem.IsMacOS()?NativeLibrary.Load(Security):0;
    static nint Constant(string name)=>Marshal.ReadIntPtr(NativeLibrary.GetExport(name.StartsWith("kCF")?cfHandle:secHandle,name));
    static nint Dictionary()=>CFDictionaryCreateMutable(0,0,NativeLibrary.GetExport(cfHandle,"kCFTypeDictionaryKeyCallBacks"),NativeLibrary.GetExport(cfHandle,"kCFTypeDictionaryValueCallBacks"));
    static void Set(nint dictionary,string key,nint value)=>CFDictionarySetValue(dictionary,Constant(key),value);
    static void String(nint dictionary,string key,string value){nint text=CFStringCreateWithCString(0,value,0x08000100);try{Set(dictionary,key,text);}finally{Release(text);}}
    static void Data(nint dictionary,string key,byte[] bytes){nint data=CFDataCreate(0,bytes,bytes.Length);try{Set(dictionary,key,data);}finally{Release(data);}}
    static void Release(nint value){if(value!=0)CFRelease(value);}
    static nint Query(){nint query=Dictionary();Set(query,"kSecClass",Constant("kSecClassGenericPassword"));String(query,"kSecAttrService","org.rovunet.macos.development");String(query,"kSecAttrAccount","profile-encryption-v1");return query;}
    static byte[] Bytes(nint data,int max){if(data==0||CFGetTypeID(data)!=CFDataGetTypeID())return [];int count=checked((int)CFDataGetLength(data));if(count>max)throw new SafeError("KEYCHAIN_ACCESS_FAILED");byte[] bytes=new byte[count];Marshal.Copy(CFDataGetBytePtr(data),bytes,0,count);return bytes;}
    static nint CurrentApplicationAccess()
    {
        // Exactly one trusted app: the running GUI's signed identity. Never an
        // empty/null trusted-app ACL, and never the Agent or root TUN service.
        nint app=0,list=0,label=0,access=0;
        try{
            if(SecTrustedApplicationCreateFromPath(0,out app)!=0||app==0)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            list=CFArrayCreate(0,[app],1,NativeLibrary.GetExport(cfHandle,"kCFTypeArrayCallBacks"));
            label=CFStringCreateWithCString(0,"Rovunet saved configurations",0x08000100);
            if(list==0||SecAccessCreate(label,list,out access)!=0||access==0)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            return access;
        }catch{Release(access);throw;}finally{Release(label);Release(list);Release(app);}
    }
    // The on-disk item is a legacy login-keychain item, including on upgrades.
    // Use the matching Keychain Services interaction policy, restoring its previous
    // value on this same thread before returning. Never rewrite the user's ACL.
    static readonly object interactionGate=new();
    byte[] ReadKey(bool interactive)
    {
        if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_KEYCHAIN_REQUIRED");
        lock(interactionGate){
            if(interactive)return ReadOrCreateKey(true);
            if(SecKeychainGetUserInteractionAllowed(out bool wasAllowed)!=0)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            if(wasAllowed&&SecKeychainSetUserInteractionAllowed(false)!=0)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            try{return ReadOrCreateKey(false);}
            finally{if(wasAllowed)SecKeychainSetUserInteractionAllowed(true);}
        }
    }
    byte[] ReadOrCreateKey(bool interactive)
    {
        for(int attempt=0;attempt<2;attempt++){
            nint query=Query(),found=0;
            try{
                if(!interactive)Set(query,"kSecUseAuthenticationUI",Constant("kSecUseAuthenticationUIFail"));
                Set(query,"kSecReturnData",Constant("kCFBooleanTrue"));Set(query,"kSecMatchLimit",Constant("kSecMatchLimitOne"));
                int status=SecItemCopyMatching(query,out found);
                if(status==0){
                    byte[] key=Bytes(found,32);
                    if(key.Length==32)return key;
                    CryptographicOperations.ZeroMemory(key);throw new SafeError("PRIVATE_STORE_FAILED");
                }
                if(status!=-25300)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            }finally{Release(found);Release(query);}
            if(!mayCreate())throw new SafeError("MAC_STORED_KEY_MISSING");
            byte[] created=RandomNumberGenerator.GetBytes(32);nint attributes=Query(),access=0,added=0;
            try{
                access=CurrentApplicationAccess();Set(attributes,"kSecAttrAccess",access);Data(attributes,"kSecValueData",created);
                int status=SecItemAdd(attributes,out added);
                if(status==-25299){CryptographicOperations.ZeroMemory(created);continue;}
                if(status!=0)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
                return created;
            }catch{CryptographicOperations.ZeroMemory(created);throw;}finally{Release(added);Release(access);Release(attributes);}
        }
        throw new SafeError("KEYCHAIN_ACCESS_FAILED");
    }
    [DllImport(Security)] static extern int SecKeychainGetUserInteractionAllowed([MarshalAs(UnmanagedType.I1)]out bool allowed);
    [DllImport(Security)] static extern int SecKeychainSetUserInteractionAllowed([MarshalAs(UnmanagedType.I1)]bool allowed);
    [DllImport(CF)] static extern nint CFDictionaryCreateMutable(nint allocator,nint capacity,nint keys,nint values);
    [DllImport(CF)] static extern void CFDictionarySetValue(nint dictionary,nint key,nint value);
    [DllImport(CF)] static extern nuint CFGetTypeID(nint value);
    [DllImport(CF)] static extern nuint CFDataGetTypeID();
    [DllImport(CF)] static extern nint CFStringCreateWithCString(nint allocator,[MarshalAs(UnmanagedType.LPUTF8Str)]string value,uint encoding);
    [DllImport(CF)] static extern nint CFDataCreate(nint allocator,byte[] bytes,nint length);
    [DllImport(CF)] static extern nint CFDataGetLength(nint data);
    [DllImport(CF)] static extern nint CFDataGetBytePtr(nint data);
    [DllImport(CF)] static extern nint CFArrayCreate(nint allocator,nint[] values,nint count,nint callbacks);
    [DllImport(CF)] static extern void CFRelease(nint value);
    [DllImport(Security)] static extern int SecItemCopyMatching(nint query,out nint result);
    [DllImport(Security)] static extern int SecItemAdd(nint attributes,out nint result);
    [DllImport(Security)] static extern int SecTrustedApplicationCreateFromPath(nint path,out nint application);
    [DllImport(Security)] static extern int SecAccessCreate(nint descriptor,nint applications,out nint access);
}
