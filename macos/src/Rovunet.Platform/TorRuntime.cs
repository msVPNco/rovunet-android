using System.Net;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class TorRuntime
{
    static readonly HashSet<string> transports=new(["obfs4","obfs3","obfs2","scramblesuit","meek_lite","webtunnel","snowflake"]);
    public static string[] Bridges(string raw)
    {
        var fields=Importer.Fields(raw);string value=fields[26].Length>0?Importer.Decode(fields[26]):"";if(value.Length>16384)throw new SafeError("IMPORT_TOO_LARGE");var lines=value.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(l=>!l.StartsWith('#')).ToArray();if(lines.Length>32)throw new SafeError("INVALID_IMPORT");
        foreach(string line in lines){if(line.Any(c=>char.IsControl(c)))throw new SafeError("INVALID_IMPORT");var words=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);int start=words[0].Equals("Bridge",StringComparison.OrdinalIgnoreCase)?1:0;if(words.Length<start+2)throw new SafeError("INVALID_IMPORT");string first=words[start];if(!transports.Contains(first)&&!IPEndPoint.TryParse(first,out _))throw new SafeError("TOR_BRIDGE_NOT_SUPPORTED");if(line.Contains('"')||line.Contains('\\'))throw new SafeError("INVALID_IMPORT");}
        return lines.Select(l=>l.StartsWith("Bridge ",StringComparison.OrdinalIgnoreCase)?l[7..]:l).ToArray();
    }
    static string Quote(string path){if(path.Any(c=>char.IsControl(c))||path.Contains('"'))throw new SafeError("INVALID_IMPORT");return '"'+path.Replace('\\','/')+'"';}
    public static string Config(string raw,int port,string dataDirectory)
    {
        if(port is <1 or >65535)throw new SafeError("LOCAL_PROXY_PORT_INVALID");string tor=EnginePaths.Require("tor"),plugin=EnginePaths.Require("lyrebird"),root=Path.GetDirectoryName(Path.GetDirectoryName(tor))!;
        var bridges=Bridges(raw);if(bridges.Length==0){var defaults=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"data","pt_config.json")))!;bridges=defaults["bridges"]!["snowflake"]!.AsArray().Select(n=>n!.ToString()).ToArray();}
        return "ClientOnly 1\nAvoidDiskWrites 1\nRunAsDaemon 0\nSafeLogging 1\nLog notice stdout\nSocksPort 127.0.0.1:"+port+"\nDataDirectory "+Quote(dataDirectory)+"\nGeoIPFile "+Quote(Path.Combine(root,"data","geoip"))+"\nGeoIPv6File "+Quote(Path.Combine(root,"data","geoip6"))+"\nUseBridges 1\nClientTransportPlugin meek_lite,obfs2,obfs3,obfs4,scramblesuit,webtunnel,snowflake exec "+Quote(plugin)+"\n"+string.Concat(bridges.Select(line=>"Bridge "+line+"\n"));
    }
}
