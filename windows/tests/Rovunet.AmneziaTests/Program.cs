using System.Text;
using System.Text.Json.Nodes;
using Rovunet.Core;

int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
void Reject(Action action,string name){try{action();}catch(SafeError){count++;return;}throw new Exception("accepted "+name);}
string key=Convert.ToBase64String(Enumerable.Range(0,32).Select(x=>(byte)x).ToArray());
string basic=$"[Interface]\nPrivateKey={key}\nAddress=10.77.0.2/32\nDNS=1.1.1.1\n[Peer]\nPublicKey={key}\nEndpoint=server.example.invalid:51820\nAllowedIPs=0.0.0.0/0, ::/0\nPersistentKeepalive=25";
string Add(string options)=>basic.Replace("[Peer]",options+"\n[Peer]");
var plain=Importer.Single(basic);Check(plain.Protocol=="WireGuard","standard WG retained");
string raw=Add("Jc=3\nJmin=10\nJmax=50\nH1=123\nI1=<b 0x0102>");
var p=Importer.Single(raw);Check(p.Protocol=="AmneziaWG"&&p.CanConnect,"AWG admitted");
var outbound=Importer.Connection(p).Outbound;var settings=outbound["settings"]!;
Check(settings["amnezia-wg-option"]!["jc"]!.GetValue<int>()==3,"junk count retained");
Check(settings["amnezia-wg-option"]!["i1"]!.ToString()=="<b 0x0102>","packet spec retained");
Check(settings["remote-dns-resolve"]!.GetValue<bool>(),"remote DNS retained");
Check(settings["persistent-keepalive"]!.GetValue<int>()==25,"keepalive retained");
foreach(string version in new[]{"1","1.5","2","3","3.1"}){
 var item=Importer.Single(Add("Version="+version+(version.StartsWith('3')?"\nHeaderProtectionKey="+key:"\nJc=3")));
 Check(item.Protocol=="AmneziaWG","version "+version);
}
var form=CoreProfileEditing.Read(p)!;Check(form.Kind=="amneziawg"&&form["i1"]=="<b 0x0102>","editor preserves packet spec");
var changed=CoreProfileEditing.Apply(p,"changed",new Dictionary<string,string>{{"jc","5"},{"s3","100"}}).Profile;
Check(changed.Name=="changed"&&changed.Raw.Contains("H1=123")&&changed.Raw.Contains("I1=<b 0x0102>"),"edit preserves untouched options");
Check(Importer.Connection(changed).Outbound["settings"]!["amnezia-wg-option"]!["s3"]!.GetValue<int>()==100,"new interface option inserted in interface");
string uri="wireguard://"+Uri.EscapeDataString(key)+"@server.example.invalid:51820/?publickey="+Uri.EscapeDataString(key)+"&address=10.77.0.2%2F32&allowedips=0.0.0.0%2F0&Jc=3&Jmin=10&Jmax=50#named";
Check(Importer.Single(uri).Protocol=="AmneziaWG"&&Importer.Single(uri).Name=="named","sharing URI");
string[] envelope=Enumerable.Repeat("",85).ToArray();envelope[0]="31";envelope[1]="xray";envelope[2]="wrapped";envelope[31]="0";envelope[33]="0";envelope[34]="1";envelope[36]="0";envelope[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
var wrapped=Importer.Single("rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',envelope))));Check(wrapped.Protocol=="AmneziaWG","native Android envelope");
string user=new('u',32),pass=new('p',64);
var runtime=JsonNode.Parse(AmneziaConfig.Runtime(wrapped,12345,user,pass))!;
Check(runtime["inbounds"]![0]!["settings"]!["auth"]!.ToString()=="password","private helper authenticated");
var xray=JsonNode.Parse(CoreLinks.Runtime(p,12346,12347,12345,0,user,pass))!;
Check(xray["outbounds"]![0]!["protocol"]!.ToString()=="socks","Xray delegates to AWG");
Check(xray["outbounds"]![0]!["settings"]!["servers"]![0]!["users"]![0]!["pass"]!.ToString()==pass,"helper auth wired");
Reject(()=>CoreLinks.Runtime(p,12346,12347),"AWG direct Xray disallowed");
Reject(()=>CoreLinks.Runtime(p,12346,12347,12345),"unauthenticated helper disallowed");
foreach(string options in new[]{"Jc=3\nJc=4","Jmin=51\nJmax=50","H1=200-100","Jc=65536","Version=4","HeaderProtectionKey=bad","Version=3\nJ1=test","RandomTrailers=true","S1=-1"})Reject(()=>Importer.Single(Add(options)),options.Split('=')[0]);
Reject(()=>Importer.Single(raw+"\nJc=4"),"AWG option in peer");
Reject(()=>Importer.Single(raw+"\n[Peer]\nPublicKey="+key),"multiple peers");
Reject(()=>Importer.Single(Add("Jc=3\nPreUp=do-something")),"scripts");
Check(Importer.Connection(Importer.Single(uri+"&Jc=4")).Outbound["settings"]!["amnezia-wg-option"]!["jc"]!.GetValue<int>()==3,"fragment text never overrides options");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{suite="windows-amnezia-domain",passed=count}));
