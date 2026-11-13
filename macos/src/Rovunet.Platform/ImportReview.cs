using System.Text;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed record ImportIssue(string Code,string Option,int Count);
public sealed class ImportReview
{
    public List<Profile> Profiles {get;}=[];
    public List<ImportIssue> Issues {get;}=[];
    public HashSet<string> Notices {get;}=[];
    public int TlsAdjustedCount {get;private set;}
    public bool RequiresConsent=>Rejected>0||TlsAdjustedCount>0;
    public int Rejected=>Issues.Sum(i=>i.Count);
    public int Total=>Profiles.Count+Rejected;
    void Reject(string code,string option="",int count=1){int index=Issues.FindIndex(i=>i.Code==code&&i.Option==option);if(index<0)Issues.Add(new(code,option,count));else Issues[index]=Issues[index] with{Count=Issues[index].Count+count};}
    void Admit(Profile p)
    {
        try{_=MacImport.Read(p.Raw);if(!p.CanConnect){Reject(p.BlockReason);return;}Profiles.Add(p);}
        catch(ImportFailure e){Reject(e.Code,e.Option);}catch(SafeError e){Reject(e.Code);}
    }
    public static ImportReview Read(string body,bool reviewTlsRequests=false)
    {
        if(Encoding.UTF8.GetByteCount(body)>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
        string text=SubscriptionUsage20.ConfigBody(body).Trim().TrimStart('\uFEFF');if(text.Length==0||text.StartsWith('<'))throw new SafeError("INVALID_IMPORT");
        if(!text.Contains("://")&&!text.StartsWith('{')&&!text.StartsWith('['))text=Importer.Decode(text);
        var review=new ImportReview();
        if(reviewTlsRequests){var normalized=MacTlsPolicy.Normalize(text);text=normalized.Text;review.TlsAdjustedCount=normalized.Adjusted;}
        if(text.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)){foreach(var profile in MacImport.Read(text))review.Admit(profile);return review;}
        if(text.StartsWith('{')||text.StartsWith('[')){
            if(text.StartsWith('{')){
                using var doc=System.Text.Json.JsonDocument.Parse(text,new(){MaxDepth=16});var root=doc.RootElement;
                if(root.TryGetProperty("version",out var version)){
                    Importer.Keys(root,"version","items","name");if(version.ValueKind!=System.Text.Json.JsonValueKind.Number||!version.TryGetInt32(out int value)||value!=1)throw new SafeError("UNSUPPORTED_FORMAT");var items=root.GetProperty("items");if(items.ValueKind!=System.Text.Json.JsonValueKind.Array)throw new SafeError("INVALID_IMPORT");if(items.GetArrayLength() is <1 or >1000)throw new SafeError("IMPORT_TOO_LARGE");string name="";if(root.TryGetProperty("name",out var label)){if(label.ValueKind!=System.Text.Json.JsonValueKind.String)throw new SafeError("INVALID_IMPORT");name=label.GetString()!;}if(name.Length>120)throw new SafeError("INVALID_IMPORT");var ids=new HashSet<string>();
                    foreach(var item in items.EnumerateArray()){Importer.Keys(item,"id","link");string id=item.GetProperty("id").GetString()??"",link=item.GetProperty("link").GetString()??"";if(string.IsNullOrWhiteSpace(id)||(id.Length>200&&id.EnumerateRunes().Take(201).Count()>200)||!ids.Add(id))throw new SafeError("INVALID_IMPORT");try{var profile=MacProtocols.Single(link);profile.ItemId=id;if(name.Length>0)profile.CollectionName=name;review.Admit(profile);}catch(ImportFailure error){if(IsProtected(link))throw;review.Reject(error.Code,error.Option);}catch(SafeError error){if(IsProtected(link))throw;review.Reject(error.Code);}}
                    return review;
                }
            }
            var common=new CompatibleImport();if(!MacExternalConfigs.Recognizes(text))throw new SafeError("INVALID_IMPORT");common.Profiles.AddRange(MacExternalConfigs.Read(text,common,true));review.Notices.UnionWith(common.Notices);foreach(var skipped in common.Skipped)review.Reject(skipped.Key,count:skipped.Value);foreach(var p in common.Profiles)review.Admit(p);return review;
        }
        string[] lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(lines.Length>10000)throw new SafeError("IMPORT_TOO_LARGE");
        foreach(string line in lines){
            if(line.StartsWith('#')||SubscriptionUsage20.MetadataLine(line,out _,out _))continue;
            try{foreach(var p in MacImport.Read(line)){if(!p.CanConnect)review.Reject(p.BlockReason);else review.Profiles.Add(p);}}
            catch(ImportFailure e){if(IsProtected(line))throw;review.Reject(e.Code,e.Option);}
            catch(SafeError e){if(IsProtected(line)||e.Code=="IMPORT_TOO_LARGE")throw;review.Reject(e.Code);}
        }
        if(review.Total==0)throw new SafeError("NO_SUPPORTED_IMPORT");return review;
    }
    static bool IsProtected(string line)=>line.StartsWith("rovunet",StringComparison.OrdinalIgnoreCase)||line.StartsWith("slipnet",StringComparison.OrdinalIgnoreCase);
}
