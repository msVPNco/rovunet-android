using System.Security.Cryptography;
using System.Text;
namespace Rovunet.Core;

public sealed record BootFilterSpec(Guid Key,Guid Layer,uint Flags,string Phase);
public static class BootPolicyPlan
{
    public const uint Persistent=1,BootTime=2;
    public static readonly Guid[] Layers=[new("c38d57d1-05a7-4c33-904f-7fbceee60e82"),new("e1cd9fe7-f4b5-4273-96c0-592e487b8650"),new("4a72393b-319f-44bc-84c3-ba54dcb3b6b4"),new("a3b42c97-9f04-4672-b87e-cee9c483257f")];
    static Guid Key(Guid installation,string label)
    {
        if(installation==Guid.Empty)throw new SafeError("BROKER_BOOT_RECEIPT_INVALID");
        return new(SHA256.HashData(Encoding.UTF8.GetBytes("Rovunet/boot-baseline/v1/"+installation.ToString("N")+"/"+label)).AsSpan(0,16));
    }
    public static Guid Sublayer(Guid installation)=>Key(installation,"sublayer");
    public static BootFilterSpec[] Filters(Guid installation)=>Enumerable.Range(0,4).SelectMany(i=>new[]{
        new BootFilterSpec(Key(installation,"boot/"+i),Layers[i],BootTime,"boot"),
        new BootFilterSpec(Key(installation,"runtime/"+i),Layers[i],Persistent,"runtime")}).ToArray();
}
