using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed class ResolverResult
{
    public int Index { get; set; }
    public string Transport { get; set; } = "";
    public bool Dns { get; set; }
    public bool Handshake { get; set; }
    public bool Pass { get; set; }
    public int Passed { get; set; }
    public int Attempted { get; set; }
    public int Completed { get; set; }
    public int Edns { get; set; }
    public string[]? Checks { get; set; }
    public string[]? CompletedChecks {get;set;}
    public long ElapsedMS { get; set; }
    public long DnsElapsedMS {get;set;}
    public string Error { get; set; } = "";
}

internal static class ResolverWorker
{
    internal static async Task Run(object request, Func<ResolverResult,ValueTask> receive, CancellationToken stop)
    {
        var exe=Path.Combine(AppContext.BaseDirectory,"engines","rovunet-transports.exe");
        await using var verifiedBinary=new FileStream(exe,FileMode.Open,FileAccess.Read,FileShare.Read);
        byte[] digest=await SHA256.HashDataAsync(verifiedBinary,stop);
        if(!Convert.ToHexString(digest).Equals(EnginePins.Transport,StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");
        using var job=new ProcessJob();
        using var process=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--scan"}}) ?? throw new SafeError("TRANSPORT_START_FAILED");
        try
        {
            job.Assign(process);
            _=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(),stop);
            await process.StandardInput.FlushAsync(stop);
            using var cancel=stop.Register(()=>{try{process.StandardInput.Close();}catch(InvalidOperationException){}});
            while(await process.StandardOutput.ReadLineAsync(stop) is { } line)
            {
                if(line.Length>4096)throw new SafeError("INVALID_MESSAGE");
                var row=JsonSerializer.Deserialize<ResolverResult>(line) ?? throw new SafeError("INVALID_MESSAGE");
                await receive(row);
            }
            await process.WaitForExitAsync(stop);
            if(process.ExitCode!=0)throw new SafeError("DNS_SCAN_INVALID");
        }
        finally
        {
            process.StandardInput.Close();
            using var grace=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try{await process.WaitForExitAsync(grace.Token);}
            catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();}
        }
    }
}
