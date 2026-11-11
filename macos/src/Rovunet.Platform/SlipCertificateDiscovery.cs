using System.Diagnostics;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class SlipCertificateDiscovery
{
    sealed class Reply
    {
        public string Certificate {get;set;}="";
        public string Error {get;set;}="";
    }

    public static async Task<string> Fetch(Profile profile,CancellationToken cancellation)
    {
        if(!TransportPlan.UsesSlip(profile.Raw))throw new SafeError("UNSUPPORTED_FORMAT");
        var fields=TransportPlan.Normalize(Importer.Fields(profile.Raw));
        string[] resolvers=fields[4].Split(',',StringSplitOptions.RemoveEmptyEntries)
            .Select(value=>TransportPlan.SlipResolver(value).Address).ToArray();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(50));
        string executable=EnginePaths.Require("rovunet-slip-trust");
        using var process=Process.Start(new ProcessStartInfo(executable){
            UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
        })??throw new SafeError("SLIP_CERT_DISCOVERY_FAILED");
        _=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            await Wire.Write(process.StandardInput.BaseStream,new{domain=fields[3],resolvers,timeoutMS=45000},deadline.Token);
            process.StandardInput.Close();
            var reply=await Wire.Read<Reply>(process.StandardOutput.BaseStream,deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            if(process.ExitCode!=0)throw new SafeError("SLIP_CERT_DISCOVERY_FAILED");
            if(reply.Error.Length>0)throw new SafeError(reply.Error is "SLIP_CERT_DISCOVERY_TIMEOUT" or "SLIP_CERT_INVALID"?reply.Error:"SLIP_CERT_DISCOVERY_FAILED");
            return SlipCertificate.ReadPublic(reply.Certificate);
        }
        catch(OperationCanceledException)when(!cancellation.IsCancellationRequested)
        {
            throw new SafeError("SLIP_CERT_DISCOVERY_TIMEOUT");
        }
        finally
        {
            if(!process.HasExited)EngineProcess.Terminate(process);
        }
    }
}
