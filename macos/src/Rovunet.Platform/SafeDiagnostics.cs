using System.Text.Json;

namespace Rovunet.Platform;

public sealed record DiagnosticEvent(DateTimeOffset Time,string Stage,string Code,string Option,string Detail="");
public static class SafeDiagnostics
{
    static readonly Queue<DiagnosticEvent> events=new();
    static readonly object gate=new();
    static string Token(string value,int limit)=>value.Length<=limit&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '_' or '-' or '.' or '=' or ',' or ' ')?value:"";
    public static void Record(string stage,string code,string option="",string detail="")
    {
        lock(gate){events.Enqueue(new(DateTimeOffset.UtcNow,Token(stage,40),Token(code,80),Token(option,200),Token(detail,600)));while(events.Count>64)events.Dequeue();}
    }
    public static void Error(string stage,string code,Exception error,string option=""){
        string metadata=error.GetType().Name+" "+string.Join(" ",new System.Diagnostics.StackTrace(error,false).GetFrames()?.Take(4).Select(frame=>frame.GetMethod()).Where(m=>m!=null).Select(m=>m!.DeclaringType?.FullName+"."+m.Name)??[]);
        metadata=new string(metadata.Select(c=>char.IsAsciiLetterOrDigit(c)||c is '_' or '.' or ' '?c:'_').Take(600).ToArray());Record(stage,code,option,metadata);
    }
    public static DiagnosticEvent[] Events(){lock(gate)return events.ToArray();}
    public static void Restore(IEnumerable<DiagnosticEvent> previous){lock(gate){events.Clear();foreach(var entry in previous.TakeLast(64))events.Enqueue(new(entry.Time,Token(entry.Stage,40),Token(entry.Code,80),Token(entry.Option,200),Token(entry.Detail,600)));}}
    public static string Json(string runtime,string state,bool verified,bool proxyRequested,bool proxyActive,bool tunRequested=false,bool tunActive=false)=>JsonSerializer.Serialize(new{
        version=BuildVersion.Text,runtime,connection=state,verified,systemProxyRequested=proxyRequested,systemProxyActive=proxyActive,
        tunRequested,tunActive,source="local macos development snapshot; pinned Windows 562a3d71670ceb7de909146bf68116d5cc614496",
        scope="0.1.7 Beta 1; managed functional changes; host fixture evidence only; native macOS acceptance pending on each architecture",
        events=Events()
    },new JsonSerializerOptions{WriteIndented=true});
}
