using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;

int count=0;
void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
void Reject(Action action,string label){try{action();}catch(SafeError){count++;return;}throw new Exception(label);}
string any="anytls://a%3Ab%40c@server.example.invalid:443?sni=tls.example.invalid&fp=chrome&idle-session-timeout=60#AnyTLS";
string tuic="tuic://00000000-0000-4000-8000-000000000001:p%3Aa@server.example.invalid:443?alpn=h3&reduce_rtt=true&heartbeat=12000ms#TUIC";
string mieru="mierus://u%3An:p%40s@server.example.invalid?profile=Test&port=8443&protocol=UDP&multiplexing=MULTIPLEXING_LOW#Mieru";
foreach(string raw in new[]{any,tuic,mieru}){
    var p=Importer.Single(raw);Check(p.CanConnect,"new protocol admitted");var form=ExtraProfileEditing.Read(p)!;
    Check(ExtraProfileEditing.Apply(p,p.Name,form.Fields).Raw==raw,"no-op retains sharing bytes");
    p.SourceId="source";p.ItemId="item";var edits=form.Fields.ToDictionary();edits["server"]="changed.example.invalid";
    var changed=ExtraProfileEditing.Apply(p,"Changed",edits);Check(changed.Id==p.Id&&changed.SourceId==p.SourceId&&changed.ItemId==p.ItemId&&ExtraProfileEditing.Read(changed)!["server"]=="changed.example.invalid","edit preserves ownership metadata");
    Reject(()=>CoreLinks.Runtime(p,10001,10002),"native helper required");Reject(()=>CoreLinks.Runtime(p,10001,10002,10003),"helper auth required");
    var adapter=JsonNode.Parse(SupplementalLinks.Runtime(p,10003,"random-user","random-secret"))!;
    Check(adapter["inbounds"]![0]!["settings"]!["auth"]!.ToString()=="password","helper requires auth");
    var outer=JsonNode.Parse(CoreLinks.Runtime(p,10001,10002,10003,helperUser:"random-user",helperPassword:"random-secret"))!;
    Check(outer["outbounds"]!.AsArray().Count==1&&outer["outbounds"]![0]!["protocol"]!.ToString()=="socks","no direct fallback");
    Reject(()=>SupplementalLinks.Parse(raw.Replace("?","?arbitrary=1&")),"unknown options refused");
    Reject(()=>SupplementalLinks.Parse(raw.Replace("?","?alpn=h3&alpn=h2&")),"duplicate options refused");
}
Check(SupplementalLinks.Form(any)["auth"]=="a:b@c","AnyTLS encoded password");Check(SupplementalLinks.Form(mieru)["username"]=="u:n","Mieru encoded username");
foreach(string raw in new[]{any+"&",tuic.Replace("reduce_rtt=true","reduce_rtt=maybe"),tuic.Replace("12000ms","9ms"),tuic.Replace("alpn=h3","udp_relay_mode=invalid"),any.Replace("fp=chrome","fp=unknown"),any.Replace("timeout=60","timeout=601"),mieru.Replace("protocol=UDP","protocol=QUIC"),mieru.Replace("?profile=Test&","?"),mieru.Replace("?profile","?unsupported=1&profile")}){
    if(raw==any+"&")continue;Reject(()=>Importer.Single(raw),"invalid bounded option");
}
Reject(()=>Importer.Single(any.Replace("?","?insecure=true&")),"AnyTLS TLS verify cannot be disabled");Reject(()=>Importer.Single(tuic.Replace("?","?allowInsecure=1&")),"TUIC TLS verify cannot be disabled");
string native=ManualProfiles.Native("ssh","SSH","server.example.invalid",22,"u","p");
var nativeFields=Importer.Decode(native[10..]).Split('|');nativeFields[1]="xray";nativeFields[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(any));
var wrapped=Importer.Single("rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',nativeFields))));Check(wrapped.Protocol=="AnyTLS"&&wrapped.CanConnect,"Android Xray wrapper");
var wf=ExtraProfileEditing.Read(wrapped)!;var we=wf.Fields.ToDictionary();we["port"]="8443";var result=ExtraProfileEditing.Apply(wrapped,"Renamed",we);
Check(Importer.Fields(result.Raw)[1]=="xray"&&Importer.Fields(result.Raw)[34]=="1"&&ExtraProfileEditing.Read(result)!["port"]=="8443","wrapped edit retains envelope");
var doh=Importer.Single(AlternativeProfiles.Create("doh","DoH","https://dns.example.invalid/query"));Check(doh.CanConnect&&doh.Protocol=="DoH"&&TransportPlan.UsesHelper(doh.Raw),"standalone DoH validated");
Reject(()=>AlternativeProfiles.Create("doh","DoH","http://dns.example.invalid/query"),"DoH requires HTTPS");Reject(()=>AlternativeProfiles.Create("doh","DoH","https://dns.example.invalid/query#bad"),"DoH fragments refused");
var tor=Importer.Single(AlternativeProfiles.Create("snowflake","Tor",""));Check(tor.CanConnect&&tor.Protocol=="Tor"&&AlternativeProfiles.Bridges(tor.Raw).Length==0,"default Tor bridge profile");
Check(AlternativeProfiles.Bridges(AlternativeProfiles.Create("snowflake","Tor","Bridge obfs4 192.0.2.2:443 ABC cert=fixture iat-mode=0")).Length==1,"explicit Tor bridge");
Reject(()=>AlternativeProfiles.Create("snowflake","Tor","obfs4 192.0.2.2:443 ABC\nSocksPort 0"),"torrc injection refused");
Reject(()=>AlternativeProfiles.Create("snowflake","Tor","exec 192.0.2.2:443 ABC"),"unknown Tor transport refused");
var socks=Enumerable.Range(1,4).Select(n=>Importer.Single("socks5://127.0.0.1:"+(11000+n)+"#Layer"+n)).ToList();
var chain=Importer.Single(ChainProfiles.Create("Four layers",socks));Check(chain.CanConnect&&ChainProfiles.Read(chain.Raw).Layers.Count==4,"four-layer chain roundtrip");
Reject(()=>ChainProfiles.Create("short",socks.Take(1).ToList()),"chain minimum");Reject(()=>ChainProfiles.Create("nested",new[]{chain,socks[0]}),"no nested chains");
Reject(()=>ChainProfiles.Create("repeated",new[]{socks[0],socks[0]}),"duplicate chain identity");Reject(()=>ChainProfiles.Create("xray invalid",new[]{Importer.Single(any),socks[0]}),"Xray chains restricted to SSH");
Check(ChainProfiles.Read(ChainProfiles.Create("Xray SSH",new[]{Importer.Single(any),Importer.Single(native)})).Layers.Count==2,"new core protocol outer plus SSH");
Reject(()=>ChainProfiles.Create("double DoH",new[]{doh,Importer.Single(AlternativeProfiles.Create("doh","D2","https://d2.example.invalid/query"))}),"singleton bridge groups");
var n=JsonNode.Parse(TransportPlan.NaiveConfig(Importer.Single(ManualProfiles.Native("naive","Naive","server.example.invalid",443,"u","p")),12001,12002))!;
Check(n["proxy"]!.ToString().StartsWith("http://127.0.0.1:12002,https://"),"Naive chains through preceding HTTP proxy");
Check(!chain.ToString().Contains("127.0.0.1")&&!new ChainLayer{Raw=any}.ToString().Contains("a%3A"),"credential-bearing objects redact ToString");
var alias=Importer.Single("anytls://fixture@server.example.invalid?peer=alias.example.invalid#Alias");var aliasForm=ExtraProfileEditing.Read(alias)!;Check(aliasForm["sni"]=="alias.example.invalid","peer alias shown as SNI");Check(ExtraProfileEditing.Apply(alias,alias.Name,aliasForm.Fields).Raw==alias.Raw,"peer no-op preserved");var aliasEdits=aliasForm.Fields.ToDictionary();aliasEdits["sni"]="new.example.invalid";Check(ExtraProfileEditing.Read(ExtraProfileEditing.Apply(alias,alias.Name,aliasEdits))!["sni"]=="new.example.invalid","changing SNI clears the old alias");
Console.WriteLine(JsonSerializer.Serialize(new{suite="protocol25-domain",passed=count}));
