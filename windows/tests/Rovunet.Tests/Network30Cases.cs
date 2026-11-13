using Rovunet.Core;

internal static class Network30Cases
{
    public static IEnumerable<(string,Action)> All()
    {
        var ethernet=new NetworkAttachment30("ethernet","Ethernet",false,"Up",["10.0.2.15/24"],["10.0.2.2"]);
        var tun=new NetworkAttachment30(TunPlan29.AdapterGuid.ToString("B"),TunPlan29.Adapter,false,"Up",[TunPlan29.Gateway4+"/30"],[]);
        string Baseline()=>NetworkEpoch30.Fingerprint([ethernet]);
        void Check(bool value){if(!value)throw new Exception();}
        yield return ("network30-local-socks-udp-default-stays-off",()=>{
            using var json=System.Text.Json.JsonDocument.Parse(CoreLinks.Runtime(Importer.Single("socks5://127.0.0.1:18883#Fixture"),10809,10808));
            Check(!json.RootElement.GetProperty("inbounds")[1].GetProperty("settings").GetProperty("udp").GetBoolean());
        });
        yield return ("network30-local-socks-udp-explicit-opt-in",()=>{
            using var json=System.Text.Json.JsonDocument.Parse(CoreLinks.Runtime(Importer.Single("socks5://127.0.0.1:18883#Fixture"),10809,10808,localSocksUdp:true));
            Check(json.RootElement.GetProperty("inbounds")[1].GetProperty("settings").GetProperty("udp").GetBoolean());
        });
        yield return ("network30-udp-does-not-trust-spoofed-protocol-field",()=>{
            var spoofed=new Profile{Protocol="SOCKS5",Raw="vless://11111111-1111-1111-1111-111111111111@127.0.0.1:443?security=tls&type=ws&sni=fixture.test&path=%2F#Fixture"};
            try{LocalProxyUdp30.Require(spoofed,true);throw new Exception("Unverified UDP enabled");}catch(SafeError error){Check(error.Code=="UDP_UPSTREAM_NOT_VERIFIED");}
        });
        yield return ("network30-own-tun-start-stop-keeps-upstream",()=>Check(Baseline()==NetworkEpoch30.Fingerprint([tun,ethernet])));
        yield return ("network30-other-vpn-still-changes-network",()=>Check(Baseline()!=NetworkEpoch30.Fingerprint([ethernet,tun with{Name="Other VPN"}])));
        yield return ("network30-same-name-foreign-guid-is-not-owned",()=>Check(Baseline()!=NetworkEpoch30.Fingerprint([ethernet,tun with{Id=Guid.NewGuid().ToString("B")}])));
        yield return ("network30-physical-address-or-gateway-or-link-change-detected",()=>{
            foreach(var next in new[]{ethernet with{Addresses=["10.0.3.15/24"]},ethernet with{Gateways=["10.0.2.1"]},ethernet with{State="Down"}})Check(Baseline()!=NetworkEpoch30.Fingerprint([next]));
        });
        yield return ("network30-address-enumeration-order-does-not-disconnect",()=>{
            var first=ethernet with{Addresses=["10.0.2.15/24","2001:db8::1/64"],Gateways=["10.0.2.2","fe80::2%6"]};
            Check(NetworkEpoch30.Fingerprint([first])==NetworkEpoch30.Fingerprint([first with{Addresses=first.Addresses.Reverse().ToArray(),Gateways=first.Gateways.Reverse().ToArray()}]));
        });
    }
}
