using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class EnginePaths
{
    public static string Root {get;set;}=Path.Combine(AppContext.BaseDirectory,"engines");
    public static string Require(string name)
    {
        if(name is not("xray" or "rovunet-transports" or "rovunet-supervisor" or "naive" or "slipstream-client" or "rovunet-slip-trust" or "rovunet-extra" or "rovunet-awg" or "rovunet-tun" or "tor" or "lyrebird"))throw new SafeError("ENGINE_NOT_READY");
        string relative=name switch{"tor"=>Path.Combine("tor","tor"),"lyrebird"=>Path.Combine("tor","pluggable_transports","lyrebird"),_=>name};string filename=Path.Combine(Root,relative+(OperatingSystem.IsWindows()?".exe":""));
        if(!File.Exists(filename))throw new SafeError("MAC_ENGINE_MISSING");
        try {
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"engine-manifest.json")));
            string rid=(OperatingSystem.IsMacOS()?"osx-":OperatingSystem.IsWindows()?"win-":"linux-")+RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            if(manifest.RootElement.GetProperty("rid").GetString()!=rid)throw new SafeError("ENGINE_ARCH_MISMATCH");
            string expected=manifest.RootElement.GetProperty("executables").GetProperty(name).GetString()??"";
            using var file=File.OpenRead(filename);
            if(!Convert.ToHexString(SHA256.HashData(file)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");
            string[] dependencies=name switch{"slipstream-client"=>["lib/libssl.3.dylib","lib/libcrypto.3.dylib"],"tor"=>OperatingSystem.IsWindows()?["data/geoip","data/geoip6","data/pt_config.json"]:["tor/libevent-2.1.7.dylib","data/geoip","data/geoip6","data/pt_config.json"],_=>[]};
            foreach(string relativeDependency in dependencies){string dependency=Path.Combine(Root,relativeDependency);if(!File.Exists(dependency))throw new SafeError("MAC_ENGINE_MISSING");string wanted=manifest.RootElement.GetProperty("files").GetProperty(relativeDependency).GetString()??"";using var input=File.OpenRead(dependency);if(!Convert.ToHexString(SHA256.HashData(input)).Equals(wanted,StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");}
            return filename;
        } catch(SafeError){throw;} catch{throw new SafeError("ENGINE_HASH_MISMATCH");}
    }
}

public static class EngineProcess
{
    [DllImport("libc")] static extern int kill(int pid,int signal);
    public static Process? Start(ProcessStartInfo original)
    {
        if(!OperatingSystem.IsMacOS())return Process.Start(original); // Explicit Windows development fixtures only.
        var wrapped=new ProcessStartInfo(EnginePaths.Require("rovunet-supervisor")) {
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=original.RedirectStandardInput,
            RedirectStandardOutput=original.RedirectStandardOutput,RedirectStandardError=original.RedirectStandardError,
            WorkingDirectory=original.WorkingDirectory
        };
        wrapped.ArgumentList.Add(original.FileName);
        if(original.Arguments.Length>0){if(original.Arguments!="run -c stdin:")throw new SafeError("INVALID_MESSAGE");foreach(string a in new[]{"run","-c","stdin:"})wrapped.ArgumentList.Add(a);}
        foreach(string a in original.ArgumentList)wrapped.ArgumentList.Add(a);
        foreach(var e in original.Environment)wrapped.Environment[e.Key]=e.Value;
        return Process.Start(wrapped);
    }
    public static void Terminate(Process process)
    {
        if(process.HasExited)return;
        if(OperatingSystem.IsMacOS()){_=kill(process.Id,15);if(process.WaitForExit(2500))return;}
        if(!process.HasExited)process.Kill(true);
    }
}

public sealed class ProcessJob : IDisposable
{
    readonly List<Process> children=[];
    public void Assign(Process process)=>children.Add(process);
    public void Dispose(){foreach(var process in children.AsEnumerable().Reverse())try{EngineProcess.Terminate(process);}catch{}children.Clear();}
}
