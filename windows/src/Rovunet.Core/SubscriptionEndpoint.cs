using System.Text.Json.Nodes;
namespace Rovunet.Core;

// An existing source item may carry local selection and pinned server trust.
// A changed remote destination needs an explicit migration before reusing that state.
public static class SubscriptionEndpoint
{
    public static string Binding(Profile profile)
    {
        if(ChainProfiles.IsChain(profile.Raw))
            return Importer.Fingerprint("chain|"+string.Join("|",ChainProfiles.Read(profile.Raw).Layers.Select(Binding)));
        if (ProfileEditing.IsNative(profile.Raw))
        {
            var fields=Importer.Fields(profile.Raw);
            if(fields[1]=="doh")return Importer.Fingerprint("doh|"+AlternativeProfiles.DohUrl(profile.Raw));
            if(fields[1]=="snowflake")return Importer.Fingerprint(string.Join("|",fields.Where((_,index)=>index!=2)));
            if(fields[1]=="xray")return Binding(Importer.Single(Importer.Decode(fields[84])));
            return Importer.Fingerprint(string.Join("|",new[]{1,3,17,19,28,40,66,67}
                .Select(index=>index<fields.Length?fields[index]:"")));
        }
        var endpoints=new List<string>();
        void Visit(JsonNode? node,string path)
        {
            if(node is JsonObject obj) {
                foreach(var pair in obj.OrderBy(pair=>pair.Key,StringComparer.Ordinal)) {
                    string key=pair.Key;
                    if(key is "address" or "server" or "port" or "endpoint" or "serverName" or "publicKey")
                        endpoints.Add(path+"/"+key+"="+pair.Value?.ToJsonString());
                    else Visit(pair.Value,path+"/"+key);
                }
            } else if(node is JsonArray array) {
                for(int i=0;i<array.Count;i++)Visit(array[i],path+"/"+i);
            }
        }
        var connection=Importer.Connection(profile);
        Visit(connection.Outbound,"");
        // Formats without an exposed destination cannot inherit state across a raw change.
        return Importer.Fingerprint(connection.Protocol+"|"+(endpoints.Count>0?
            string.Join("|",endpoints):profile.Raw.Split('#')[0]));
    }
    public static void RequireSame(string original,Profile incoming)
    {
        if(Binding(Importer.Single(original))!=Binding(incoming))throw new SafeError("SUBSCRIPTION_IDENTITY_CONFLICT");
    }
}
