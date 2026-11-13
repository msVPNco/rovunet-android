using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Rovunet.Core;

namespace Rovunet.Platform;

// The GUI retains one authorization session. Only an external form travels over
// inherited pipes to its own proxy guardian; it is never persisted or logged.
public sealed class MacProxyAuthorization : IDisposable
{
    const string Security="/System/Library/Frameworks/Security.framework/Security";
    readonly object gate=new();
    nint authorization;
    bool disposed;

    public byte[] Export()
    {
        lock(gate){
            ObjectDisposedException.ThrowIf(disposed,this);
            if(authorization==0)authorization=Import([]);
            byte[] form=new byte[32];
            if(AuthorizationMakeExternalForm(authorization,form)==0)return form;
            CryptographicOperations.ZeroMemory(form);
            throw new SafeError("MAC_PROXY_AUTH_DENIED");
        }
    }

    internal static nint Import(byte[] form)
    {
        if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        if(form.Length is not(0 or 32))throw new SafeError("INVALID_MESSAGE");
        int status=form.Length==0?AuthorizationCreate(0,0,0,out nint handle):
            AuthorizationCreateFromExternalForm(form,out handle);
        if(status!=0||handle==0)throw new SafeError("MAC_PROXY_AUTH_DENIED");
        return handle;
    }

    internal static void Release(nint handle){if(handle!=0)AuthorizationFree(handle,0);}
    public void Dispose(){lock(gate){disposed=true;Release(authorization);authorization=0;}}

    [DllImport(Security)] static extern int AuthorizationCreate(nint rights,nint environment,uint flags,out nint authorization);
    [DllImport(Security)] static extern int AuthorizationMakeExternalForm(nint authorization,[Out] byte[] form);
    [DllImport(Security)] static extern int AuthorizationCreateFromExternalForm(byte[] form,out nint authorization);
    [DllImport(Security)] static extern int AuthorizationFree(nint authorization,uint flags);
}
