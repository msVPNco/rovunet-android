using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Agent;
public sealed partial class Runtime
{
    Process? tor25;
    string? torDirectory25;
    internal static string VerifiedTorRoot25()
    {
        string manifest=Path.Combine(AppContext.BaseDirectory,"engines","tor-manifest.json"),root=Path.Combine(AppContext.BaseDirectory,"engines","tor");
        if(!File.Exists(manifest))throw new SafeError("ENGINE_NOT_READY");
        using(var f=File.OpenRead(manifest))if(!Convert.ToHexString(SHA256.HashData(f)).Equals(EnginePins.TorManifest,StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");
        using var doc=JsonDocument.Parse(File.ReadAllText(manifest));var expected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in doc.RootElement.EnumerateArray()){
            Importer.Keys(item,"path","sha256");string relative=item.GetProperty("path").GetString()??"";
            string full=Path.GetFullPath(Path.Combine(root,relative));
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!expected.Add(full)||(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new SafeError("ENGINE_HASH_MISMATCH");
            using var file=File.OpenRead(full);if(!Convert.ToHexString(SHA256.HashData(file)).Equals(item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");
        }
        if(!expected.SetEquals(Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)))throw new SafeError("ENGINE_HASH_MISMATCH");
        return root;
    }
    static string Quote25(string path)=>'"'+path.Replace('\\','/')+'"';
    internal static string TorConfiguration25(Profile profile,int port,string root,string directory,int previousSocks=0)
    {
        var bridges=AlternativeProfiles.Bridges(profile.Raw);
        if(bridges.Length==0){var defaults=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tor","pluggable_transports","pt_config.json")))!;bridges=defaults["bridges"]!["snowflake"]!.AsArray().Select(n=>n!.ToString()).ToArray();}
        string plugin=Path.Combine(root,"tor","pluggable_transports","lyrebird.exe");
        return "ClientOnly 1\nAvoidDiskWrites 1\nRunAsDaemon 0\nSafeLogging 1\nLog notice stdout\nSocksPort 127.0.0.1:"+Importer.Port(port.ToString())+"\nDataDirectory "+Quote25(directory)+"\nGeoIPFile "+Quote25(Path.Combine(root,"data","geoip"))+"\nGeoIPv6File "+Quote25(Path.Combine(root,"data","geoip6"))+"\nUseBridges 1\nClientTransportPlugin meek_lite,obfs2,obfs3,obfs4,scramblesuit,webtunnel,snowflake exec "+Quote25(plugin)+"\n"+(previousSocks>0?"Socks5Proxy 127.0.0.1:"+Importer.Port(previousSocks.ToString())+"\n":"")+string.Concat(bridges.Select(line=>"Bridge "+line+"\n"));
    }
    async Task<int> StartTor25(Profile profile,CancellationToken ct,int previousSocks)
    {
        string root=VerifiedTorRoot25();Directory.CreateDirectory(PrivateStore.Root);
        using var current=Process.GetCurrentProcess();torDirectory25=Path.Combine(PrivateStore.Root,"tor-session-"+current.Id+"-"+Guid.NewGuid().ToString("N"));
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);var sid=WindowsIdentity.GetCurrent().User!;acl.SetOwner(sid);acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(torDirectory25).Create(acl);
        var reservation=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);reservation.Start();int port=((System.Net.IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        var info=new ProcessStartInfo(Path.Combine(root,"tor","tor.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
        info.ArgumentList.Add("-f");info.ArgumentList.Add("-");if(previousSocks>0)info.Environment["TOR_PT_PROXY"]="socks5://127.0.0.1:"+previousSocks;
        tor25=Process.Start(info)??throw new SafeError("CORE_START_FAILED");job!.Assign(tor25);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _=ObserveTor25(tor25.StandardOutput,ready);_=ObserveTor25(tor25.StandardError,ready);
        tor25.Exited+=(_,_)=>ready.TrySetException(new SafeError("TOR_BOOTSTRAP_FAILED"));tor25.EnableRaisingEvents=true;
        await tor25.StandardInput.WriteAsync(TorConfiguration25(profile,port,root,torDirectory25,previousSocks).AsMemory(),ct);tor25.StandardInput.Close();
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(180),ct);}catch(TimeoutException){throw new SafeError("TOR_BOOTSTRAP_TIMEOUT");}
        await WaitListener(port,tor25,ct);tor25.Exited+=OnCoreExit;return port;
    }
    static async Task ObserveTor25(StreamReader reader,TaskCompletionSource ready)
    {
        char[] buffer=new char[1024];var line=new System.Text.StringBuilder();bool oversized=false;
        try{int n;while((n=await reader.ReadAsync(buffer))>0)for(int i=0;i<n;i++){
            if(buffer[i]!='\n'){if(line.Length<1024)line.Append(buffer[i]);else oversized=true;continue;}
            if(!oversized&&line.ToString().Contains("Bootstrapped 100%",StringComparison.Ordinal))ready.TrySetResult();line.Clear();oversized=false;
        }}catch{ready.TrySetException(new SafeError("TOR_BOOTSTRAP_FAILED"));}
    }
    void StopTor25()
    {
        if(tor25!=null){tor25.Exited-=OnCoreExit;try{if(!tor25.HasExited){tor25.Kill(true);tor25.WaitForExit(3000);}}catch{}tor25.Dispose();tor25=null;}
        if(torDirectory25!=null){string full=Path.GetFullPath(torDirectory25);if(Path.GetDirectoryName(full)==Path.GetFullPath(PrivateStore.Root)&&Path.GetFileName(full).StartsWith("tor-session-",StringComparison.Ordinal))try{Directory.Delete(full,true);}catch{}torDirectory25=null;}
    }
}
