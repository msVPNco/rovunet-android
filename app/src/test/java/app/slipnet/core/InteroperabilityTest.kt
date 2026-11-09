package app.slipnet.core

import app.slipnet.data.export.*
import app.slipnet.data.subscription.PublicFeed
import app.slipnet.domain.model.*
import kotlinx.coroutines.runBlocking
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class InteroperabilityTest {
    private val id = "00000000-0000-4000-8000-000000000001"
    private val publicKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    @Test fun rawDisplayNamesDoNotChangeEndpointQueryOrSecrets() {
        val link="vless://$id@example.invalid:443?security=reality&type=tcp&pbk=$publicKey&headerType=none#Free profile 🇨🇦 + plus"
        val parsed=CoreLink.parse(link)
        assertEquals("Free profile 🇨🇦 + plus",parsed.name)
        assertEquals("example.invalid",parsed.host)
        assertEquals("vless",parsed.outbound.getString("protocol"))
        assertTrue(ConfigImporter().parseAndImport(link) is ImportResult.Success)
        assertEquals(parsed.outbound.toString(),CoreLink.parse(CoreForm.decode(link).encode(parsed.name)).outbound.toString())
    }
    @Test fun hysteriaRetainsVerifiedTlsAndRejectsInsecureRegardlessOfDisplayName() {
        val link="hysteria2://synthetic@example.invalid:443?sni=example.invalid&security=tls#Free name"
        assertEquals("hysteria",CoreLink.parse(link).outbound.getString("protocol"))
        for (flag in listOf("insecure=1","allowInsecure=true")) assertEquals("TLS_UNVERIFIED",runCatching{CoreLink.parse(link.substringBefore('#')+"&$flag#Free name")}.exceptionOrNull()?.message)
    }
    @Test fun trojanAndVmessRoundtripThroughEditorNativeExportAndTcpTarget() {
        val trojan="trojan://synthetic%3A%2B%40%25@example.invalid:443?security=tls&type=ws&host=cdn.example.invalid&path=%2Fsocket#Free name"
        val vmess="vmess://"+WireBase64.encode(JSONObject().put("v","2").put("ps","Synthetic VMess").put("add","2001:db8::1").put("port","443").put("id",id).put("aid","0").put("scy","auto").put("net","grpc").put("type","gun").put("tls","tls").put("path","fixture").toString().toByteArray())
        for (link in listOf(trojan,vmess)) {
            val parsed=CoreLink.parse(link)
            val form=CoreForm.decode(link)
            assertEquals(parsed.outbound.toString(),CoreLink.parse(form.encode(parsed.name)).outbound.toString())
            val profile=(ConfigImporter().parseAndImport(link) as ImportResult.Success).profiles.single()
            assertEquals(TunnelType.XRAY,profile.tunnelType)
            assertEquals(parsed.host to 443,ProfileCollections.coreTcpTarget(profile))
            val restored=(ConfigImporter().parseAndImport(ConfigExporter().exportSingleProfile(profile)) as ImportResult.Success).profiles.single()
            assertEquals(profile.coreLink,restored.coreLink)
        }
    }
    @Test fun commonVmessExporterMetadataAndNullDefaultsDoNotRejectValidAead() {
        val payload=JSONObject().put("v",JSONObject.NULL).put("type",JSONObject.NULL).put("ps","Synthetic").put("name","Display metadata").put("add","example.invalid").put("port",443).put("id",id).put("aid","0").put("net","ws").put("tls","none").put("pcs","").put("vcn","")
        fun parse()=CoreLink.parse("vmess://"+WireBase64.encode(payload.toString().toByteArray()))
        val parsed = parse()
        assertEquals("vmess",parsed.outbound.getString("protocol"))
        val raw = "vmess://"+WireBase64.encode(payload.toString().toByteArray())
        assertEquals(parsed.outbound.toString(),CoreLink.parse(CoreForm.decode(raw).encode(parsed.name)).outbound.toString())
        payload.put("pcs","synthetic-custom-cert-pin")
        assertEquals("UNSUPPORTED_OPTION",runCatching { parse() }.exceptionOrNull()?.message)
    }
    @Test fun websocketEarlyDataUsesThePinnedCorePathDialectWithoutDiscardingHeaders() {
        val link="trojan://synthetic@example.invalid:443?security=tls&type=ws&path=%2Ffixture&ed=2048&eh=Sec-WebSocket-Protocol"
        val parsed=CoreLink.parse(link)
        val ws=parsed.outbound.getJSONObject("streamSettings").getJSONObject("wsSettings")
        assertEquals("/fixture?ed=2048",ws.getString("path"))
        assertFalse(ws.has("maxEarlyData"))
        assertEquals(parsed.outbound.toString(),CoreLink.parse(CoreForm.decode(link).encode("Fixture")).outbound.toString())
        assertTrue(runCatching{CoreLink.parse(link.replace("Sec-WebSocket-Protocol","X-Custom-Header"))}.isFailure)
    }
    @Test fun unsupportedSettingsNeverBecomeAnApparentlyUsableConnection() {
        val base="trojan://synthetic@example.invalid:443?security=tls&type=tcp"
        for (option in listOf("cs=100","fm=1","headerType=http","allowInsecure=1","serviceName=unexpected","security=none")) assertTrue(runCatching{CoreLink.parse("$base&$option#Display name")}.isFailure)
        val json=JSONObject().put("v","2").put("add","example.invalid").put("port",443).put("id",id).put("aid",64)
        assertTrue(runCatching{CoreLink.parse("vmess://"+WireBase64.encode(json.toString().toByteArray()))}.isFailure)
    }
    @Test fun xrayAndSingboxJsonExtractConnectionsWithoutApplyingGlobalConfig() {
        val xray="""{"dns":{"servers":["192.0.2.1"]},"inbounds":[{"listen":"0.0.0.0","port":1234}],"routing":{"rules":[]},"outbounds":[{"tag":"fixture","protocol":"trojan","settings":{"servers":[{"address":"example.invalid","port":443,"password":"synthetic"}]},"streamSettings":{"network":"ws","security":"tls","tlsSettings":{"serverName":"example.invalid"},"wsSettings":{"path":"/socket","headers":{"Host":"cdn.example.invalid"}}}},{"protocol":"freedom"}]}"""
        val sing="""{"outbounds":[{"type":"vless","tag":"fixture","server":"example.invalid","server_port":443,"uuid":"$id","tls":{"enabled":true,"server_name":"example.invalid"},"transport":{"type":"ws","path":"/socket","headers":{"Host":"cdn.example.invalid"}}}]}"""
        for (doc in listOf(xray,sing)) {
            val result=ConfigImporter().parseAndImport(doc) as ImportResult.Success
            assertEquals(1,result.profiles.size)
            assertTrue(result.warnings.contains("REVIEW03_JSON_CONNECTIONS_ONLY"))
            val runtime=JSONObject(CoreLink.runtime(CoreDocument.read(doc).links.single(),18080))
            assertFalse(runtime.has("routing"));assertFalse(runtime.has("dns"))
            assertEquals("127.0.0.1",runtime.getJSONArray("inbounds").getJSONObject(0).getString("listen"))
        }
    }
    @Test fun nativePanelEnvelopeCannotBeReclassifiedAsExternalJsonByAnExtraKey() {
        val base=JSONObject().put("version",1).put("items",org.json.JSONArray().put(JSONObject().put("id","fixture").put("link","trojan://synthetic@example.invalid:443?security=tls")))
        assertEquals(1,app.slipnet.data.subscription.SubscriptionPayload.read(base.toString()).size)
        for (key in listOf("type","protocol","outbounds")) {
            val changed=JSONObject(base.toString()).put(key,"unexpected")
            assertTrue(runCatching { app.slipnet.data.subscription.SubscriptionPayload.read(changed.toString()) }.isFailure)
            assertTrue(ConfigImporter().parseAndImport(changed.toString()) is ImportResult.Error)
        }
    }
    @Test fun jsonCannotSilentlyDropChainsCustomTrustOrEffectiveHeaders() {
        val template="""{"protocol":"trojan","settings":{"address":"example.invalid","port":443,"password":"synthetic"},"streamSettings":{"network":"ws","security":"tls","tlsSettings":{"serverName":"example.invalid"},"wsSettings":{"headers":{"Authorization":"private"}}}}"""
        assertTrue(ConfigImporter().parseAndImport(template) is ImportResult.Error)
        assertTrue(ConfigImporter().parseAndImport(template.replace("\"Authorization\":\"private\"","\"Host\":\"example.invalid\"").replace("\"serverName\":\"example.invalid\"","\"allowInsecure\":true")) is ImportResult.Error)
        assertTrue(ConfigImporter().parseAndImport(template.replace("\"streamSettings\":", "\"proxySettings\":{\"tag\":\"other\"},\"streamSettings\":")) is ImportResult.Error)
    }
    @Test fun publicSummarySeparatesActualProtocolsAndReasons() = runBlocking {
        val valid="vless://$id@example.invalid:443?security=reality&type=tcp&pbk=$publicKey#Raw display name"
        val result=PublicFeed.parse("$valid\nhysteria2://synthetic@example.invalid:443?insecure=1#Free name\ntrojan://synthetic@example.invalid:443?security=tls&type=unsupported#Free name\nunknown://synthetic",ConfigImporter(),"")
        assertEquals(1,result.profiles.size);assertEquals(3,result.unsupported)
        assertEquals(1,result.stats.single{it.protocol=="VLESS"}.accepted)
        assertEquals(1,result.stats.single{it.protocol=="Hysteria2"}.reasons[PublicFeed.Reason.TLS])
        assertEquals(1,result.stats.single{it.protocol=="TROJAN"}.reasons[PublicFeed.Reason.TRANSPORT])
        val empty=PublicFeed.parse("unknown://synthetic",ConfigImporter(),"",allowEmpty=true)
        assertTrue(empty.profiles.isEmpty());assertEquals(1,empty.unsupported)
    }
}
