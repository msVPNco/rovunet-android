using System.Security.Cryptography;
using Rovunet.Core;

namespace Rovunet.Platform;

// Only the GUI opens the login Keychain. Children receive a copy through their
// inherited anonymous pipe; no key is put in an argument, environment or file.
public sealed class SessionSecretVault(Func<byte[]> acquire) : ISecretVault, IDisposable
{
    readonly object gate=new();
    byte[]? key;
    bool denied,disposed;
    public byte[] GetOrCreateKey()=>Read(acquire,false);
    public byte[] Unlock(Func<byte[]> userApprovedRead)=>Read(userApprovedRead,true);
    byte[] Read(Func<byte[]> fetch,bool explicitRequest)
    {
        lock(gate){
            ObjectDisposedException.ThrowIf(disposed,this);
            if(denied&&!explicitRequest)throw new SafeError("KEYCHAIN_ACCESS_FAILED");
            if(key==null){
                try{key=fetch();if(key.Length!=32)throw new SafeError("KEYCHAIN_ACCESS_FAILED");denied=false;}
                catch{if(key!=null)CryptographicOperations.ZeroMemory(key);key=null;denied=true;throw;}
            }
            return key.ToArray(); // Callers erase their own copy after use.
        }
    }
    public void Dispose(){lock(gate){disposed=true;if(key!=null)CryptographicOperations.ZeroMemory(key);key=null;}}
}

public sealed class PipeSecretVault : ISecretVault, IDisposable
{
    readonly byte[] key;
    bool disposed;
    public PipeSecretVault(byte[] supplied){if(supplied.Length!=32)throw new SafeError("INVALID_MESSAGE");key=supplied.ToArray();}
    public byte[] GetOrCreateKey(){ObjectDisposedException.ThrowIf(disposed,this);return key.ToArray();}
    public void Dispose(){disposed=true;CryptographicOperations.ZeroMemory(key);}
}
