using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Rovunet.Platform;

static class NativePreferencesFixture
{
    const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string SC="/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration";
    public static async Task Run(Action<bool,string> check)
    {
        var dir=Directory.CreateTempSubdirectory("rovunet-prefs-");
        string service=Guid.NewGuid().ToString().ToUpperInvariant(),set=Guid.NewGuid().ToString().ToUpperInvariant();
        string path=Path.Combine(dir.FullName,"isolated.plist");
        var empty=new XElement("dict");XElement String(string value)=>new("string",value);
        XElement Dict(params (string,object)[] entries)=>new("dict",entries.SelectMany(e=>new[]{new XElement("key",e.Item1),(XElement)e.Item2}));
        var proxy=Dict(("HTTPEnable",new XElement("integer",0)),("HTTPProxy",String("original.invalid")),("HTTPPort",new XElement("integer",8111)));
        var serviceData=Dict(("UserDefinedName",String("Fixture")),("Interface",Dict(("DeviceName",String("fixture0")),("Hardware",String("Ethernet")),("Type",String("Ethernet")))),("Proxies",proxy),("IPv4",Dict(("ConfigMethod",String("DHCP")))));
        var setData=Dict(("UserDefinedName",String("Fixture")),("Network",Dict(("Service",Dict((service,Dict(("__LINK__",String("/NetworkServices/"+service)))))))));
        new XDocument(new XElement("plist",new XAttribute("version","1.0"),Dict(("CurrentSet",String("/Sets/"+set)),("Sets",Dict((set,setData))),("NetworkServices",Dict((service,serviceData)))))).Save(path);
        MacProxyPreferences Open(){
            nint name=CFStringCreateWithCString(0,"Rovunet isolated QA",0x08000100),file=CFStringCreateWithCString(0,path,0x08000100);
            nint prefs;try{prefs=SCPreferencesCreate(0,name,file);}finally{CFRelease(name);CFRelease(file);}
            return (MacProxyPreferences)(Activator.CreateInstance(typeof(MacProxyPreferences),BindingFlags.Instance|BindingFlags.NonPublic,null,[prefs],null)??throw new InvalidOperationException());
        }
        try{
            var vault=new Vault();using var journal=new PrivateProxyJournal(Path.Combine(dir.FullName,"journal"),vault);
            using var prefs=Open();check(prefs.Services().SequenceEqual([service]),"Darwin-isolated-active-service");
            if(!prefs.Services().Contains(service))return;
            var original=prefs.Read(service)!.Xml();
            // On some Darwin versions configd requires admin for any preferences lock, including
            // an application-owned absolute path. An ordinary user must leave that file intact.
            try{using var permitted=prefs.Begin();}
            catch(Rovunet.Core.SafeError error) when(error.Code=="MAC_PROXY_AUTH_DENIED"){
                check(prefs.Read(service)!.Xml()==original&&journal.Load().Services.Count==0,"Darwin-isolated-denied-before-write-preimage-intact");
                Console.WriteLine("SKIP native SCPreferences commits: the OS denied an ordinary-user lock on its isolated file.");
                return;
            }
            var lease=new SystemProxyLease(prefs,journal);lease.Start(18200,18201);
            using(var independent=Open()){var actual=independent.Read(service)!;check(actual["HTTPProxy"]==ProxyMap.Text("127.0.0.1")&&actual["SOCKSPort"]==ProxyMap.Number(18201),"Darwin-isolated-committed-systemconfiguration-write");}
            lease.Stop();using(var restored=Open()){check(restored.Read(service)!.Xml()==original,"Darwin-isolated-restores-preimage");}
            await Task.Delay(TimeSpan.FromSeconds(31));lease.Start(18202,18203);
            using(var external=Open())using(external.Begin()){var value=external.Read(service)!;value["HTTPProxy"]=ProxyMap.Text("changed.invalid");external.Write(service,value);external.Commit();}
            using(var recover=Open()){new SystemProxyLease(recover,journal).Recover();var after=recover.Read(service)!;check(after["HTTPProxy"]==ProxyMap.Text("changed.invalid")&&!after.ContainsKey("SOCKSPort")&&journal.Load().Services.Count==0,"Darwin-isolated-reconnect-after-31s-external-edit-CAS-and-fresh-session-recovery");}
        }finally{dir.Delete(true);}
    }
    [DllImport(SC)] static extern nint SCPreferencesCreate(nint allocator,nint name,nint prefsId);
    [DllImport(CF)] static extern nint CFStringCreateWithCString(nint allocator,string value,uint encoding);
    [DllImport(CF)] static extern void CFRelease(nint value);
}
