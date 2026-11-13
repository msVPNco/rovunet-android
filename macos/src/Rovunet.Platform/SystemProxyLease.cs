using System.Xml;
using System.Xml.Linq;
using Rovunet.Core;

namespace Rovunet.Platform;

// Values are complete plist elements, so unknown vendor options and absent keys
// survive a round trip. A lease owns only the settings it actually writes.
public sealed class ProxyMap : Dictionary<string,string>
{
    public ProxyMap() : base(StringComparer.Ordinal) { }
    public ProxyMap Copy(){var copy=new ProxyMap();foreach(var pair in this)copy[pair.Key]=pair.Value;return copy;}
    public static string Number(int value)=>new XElement("integer",value).ToString(SaveOptions.DisableFormatting);
    public static string Text(string value)=>new XElement("string",value).ToString(SaveOptions.DisableFormatting);
    public static ProxyMap Parse(string xml)
    {
        using var input=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=Importer.MaxBytes});
        var root=XDocument.Load(input).Root;var dict=root?.Element("dict")??throw new SafeError("MAC_PROXY_SETTINGS_INVALID");
        var result=new ProxyMap();var nodes=dict.Elements().ToArray();if(nodes.Length%2!=0)throw new SafeError("MAC_PROXY_SETTINGS_INVALID");
        for(int i=0;i<nodes.Length;i+=2){if(nodes[i].Name!="key"||!result.TryAdd(nodes[i].Value,nodes[i+1].ToString(SaveOptions.DisableFormatting)))throw new SafeError("MAC_PROXY_SETTINGS_INVALID");}
        return result;
    }
    public string Xml()=>new XDocument(new XElement("plist",new XAttribute("version","1.0"),new XElement("dict",this.OrderBy(p=>p.Key,StringComparer.Ordinal).SelectMany(p=>new[]{new XElement("key",p.Key),XElement.Parse(p.Value)})))).ToString(SaveOptions.DisableFormatting);
}

public sealed class ProxyLeaseEntry
{
    public bool OriginalExists {get;set;}
    public ProxyMap Original {get;set;}=new();
    public ProxyMap Installed {get;set;}=new();
}
public sealed class ProxyLeaseJournal
{
    public int Version {get;set;}=1;
    public Dictionary<string,ProxyLeaseEntry> Services {get;set;}=[];
}
public interface IProxyJournal
{
    ProxyLeaseJournal Load();
    void Save(ProxyLeaseJournal journal);
}
public interface IProxyPreferences : IDisposable
{
    void RefreshSnapshot(){} // Read-only discovery, without acquiring the system write lock.
    IDisposable Begin();
    IReadOnlyList<string> Services();
    ProxyMap? Read(string service);
    void Write(string service,ProxyMap? value);
    void Commit();
}

public sealed class SystemProxyLease(IProxyPreferences preferences,IProxyJournal storage,Func<bool>? stillOwned=null)
{
    static readonly string[][] groups=[
        ["HTTPEnable","HTTPProxy","HTTPPort"],["HTTPSEnable","HTTPSProxy","HTTPSPort"],
        ["SOCKSEnable","SOCKSProxy","SOCKSPort"],["ProxyAutoConfigEnable"],["ProxyAutoDiscoveryEnable"]];
    ProxyLeaseJournal journal=new();
    int httpPort,socksPort;
    public bool Active {get;private set;}
    public bool RestoreAttempted {get;private set;}
    public int ServiceCount=>journal.Services.Count;
    public void Start(int http,int socks)
    {
        if(http is <1 or >65535||socks is <1 or >65535||http==socks)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        Recover();RestoreAttempted=false;httpPort=http;socksPort=socks;Refresh();
        if(ServiceCount==0)throw new SafeError("MAC_PROXY_NO_SERVICE");Active=true;
    }
    public void Refresh()
    {
        if(httpPort==0)return;
        preferences.RefreshSnapshot();
        if(!preferences.Services().Any(id=>!journal.Services.ContainsKey(id)))return;
        using var transaction=preferences.Begin();
        if(stillOwned!=null&&!stillOwned())throw new SafeError("CANCELLED");
        var fresh=preferences.Services().Where(id=>!journal.Services.ContainsKey(id)).ToArray();if(fresh.Length==0)return;
        foreach(string id in fresh){var original=preferences.Read(id);journal.Services[id]=new(){OriginalExists=original!=null,Original=original?.Copy()??new(),Installed=Installed(original,httpPort,socksPort)};}
        storage.Save(journal); // Durable before the first system change.
        foreach(string id in fresh)preferences.Write(id,journal.Services[id].Installed);
        if(stillOwned!=null&&!stillOwned())throw new SafeError("CANCELLED");
        preferences.Commit();
    }
    public void Recover()
    {
        RestoreAttempted=true;
        journal=storage.Load();if(journal.Version!=1||journal.Services.Count>256||journal.Services.Keys.Any(id=>!Guid.TryParse(id,out _)))throw new SafeError("MAC_PROXY_JOURNAL_INVALID");
        Active=false;if(journal.Services.Count==0)return;
        using var transaction=preferences.Begin();bool changed=false;
        foreach(var (id,entry) in journal.Services){
            var current=preferences.Read(id);if(current==null)continue;
            var restored=current.Copy();
            foreach(var group in groups){
                if(!group.All(key=>current.GetValueOrDefault(key)==entry.Installed.GetValueOrDefault(key)))continue;
                foreach(string key in group){if(entry.Original.TryGetValue(key,out string? original))restored[key]=original;else restored.Remove(key);}
            }
            if(!Equal(current,restored)){preferences.Write(id,!entry.OriginalExists&&restored.Count==0?null:restored);changed=true;}
        }
        // Apply even after a previous commit succeeded but its apply step failed.
        if(changed||journal.Services.Count>0)preferences.Commit();
        storage.Save(new());journal=new();
    }
    public void Stop(){Recover();httpPort=socksPort=0;}
    static bool Equal(ProxyMap a,ProxyMap b)=>a.Count==b.Count&&a.All(p=>b.GetValueOrDefault(p.Key)==p.Value);
    public static ProxyMap Installed(ProxyMap? previous,int http,int socks)
    {
        var result=previous?.Copy()??new();
        foreach(string type in new[]{"HTTP","HTTPS","SOCKS"}){result[type+"Enable"]=ProxyMap.Number(1);result[type+"Proxy"]=ProxyMap.Text("127.0.0.1");result[type+"Port"]=ProxyMap.Number(type=="SOCKS"?socks:http);}
        result["ProxyAutoConfigEnable"]=ProxyMap.Number(0);result["ProxyAutoDiscoveryEnable"]=ProxyMap.Number(0);
        // Existing bypass lists, authentication and unrelated network/DNS keys stay intact.
        return result;
    }
}

public sealed class PrivateProxyJournal : IProxyJournal,IDisposable
{
    readonly PrivateStore store;
    readonly FileStream ownership;
    public PrivateProxyJournal(string root,ISecretVault vault)
    {
        Directory.CreateDirectory(root);if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new SafeError("PRIVATE_STORE_FAILED");
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        string path=Path.Combine(root,"owner.lock");if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new SafeError("PRIVATE_STORE_FAILED");
        try{var options=new FileStreamOptions{Mode=FileMode.OpenOrCreate,Access=FileAccess.ReadWrite,Share=FileShare.None};if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;ownership=new FileStream(path,options);}
        catch(IOException){throw new SafeError("MAC_PROXY_BUSY");}
        store=new(root,vault);
    }
    public ProxyLeaseJournal Load()=>store.Load<ProxyLeaseJournal>();
    public void Save(ProxyLeaseJournal journal)=>store.Save(journal);
    public void Dispose()=>ownership.Dispose();
}
