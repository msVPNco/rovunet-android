using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rovunet.Core;
namespace Rovunet.NetworkBroker;
internal static class MaintenanceGate
{
    public static void RequireOpen(string root)
    {
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant()))).ToLowerInvariant();
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Rovunet-Network-Maintenance",key,"operation.json");
        if(!File.Exists(path))return;
        try{
            TrustedInstallation29.ProtectedAcl(new FileInfo(path));if(new FileInfo(path).Length>4194304)throw new SafeError("BROKER_MAINTENANCE_ACTIVE");
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var doc=JsonDocument.Parse(file);var state=doc.RootElement;
            if(state.GetProperty("Schema").GetInt32()!=1||!string.Equals(state.GetProperty("Target").GetString(),root,StringComparison.OrdinalIgnoreCase)||state.GetProperty("Phase").GetString() is not("completed" or "rolled_back"))throw new SafeError("BROKER_MAINTENANCE_ACTIVE");
        }catch{throw new SafeError("BROKER_MAINTENANCE_ACTIVE");}
    }
}
