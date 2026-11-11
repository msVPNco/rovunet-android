package app.slipnet.core

import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ImportResult
import app.slipnet.domain.model.TunnelType
import org.junit.Assert.*
import org.junit.Test
import java.util.Base64

class ShadowsocksLinkTest {
    private fun auth(method:String="aes-256-gcm", password:String="fixture:password+%/") =
        Base64.getUrlEncoder().withoutPadding().encodeToString("$method:$password".toByteArray())
    @Test fun sip002AndLegacyPreserveSecretsAndIpv6WithoutWeakeningEncryption() {
        val link="ss://${auth()}@[2001:db8::1]:8388#Synthetic"
        val parsed=CoreLink.parse(link)
        assertEquals("2001:db8::1",parsed.host)
        val settings=parsed.outbound.getJSONObject("settings").getJSONArray("servers").getJSONObject(0)
        assertEquals("aes-256-gcm",settings.getString("method"))
        assertEquals("fixture:password+%/",settings.getString("password"))
        val legacy="ss://"+Base64.getEncoder().encodeToString("aes-256-gcm:fixture:password+%/@[2001:db8::1]:8388".toByteArray())+"#Legacy"
        assertEquals(parsed.outbound.toString(),CoreLink.parse(legacy).outbound.toString())
        val form=CoreForm.decode(legacy)
        assertEquals("ss",form.kind)
        assertEquals(parsed.outbound.toString(),CoreLink.parse(form.encode("Renamed")).outbound.toString())
    }
    @Test fun unsafeCiphersPluginsAndMissingPortsAreRejected() {
        for(method in listOf("none","aes-128-cfb","rc4-md5")) assertTrue(runCatching { CoreLink.parse("ss://${auth(method)}@example.invalid:8388") }.isFailure)
        for(suffix in listOf("?plugin=v2ray-plugin", "?insecure=1")) assertTrue(runCatching { CoreLink.parse("ss://${auth()}@example.invalid:8388$suffix") }.isFailure)
        assertTrue(runCatching { CoreLink.parse("ss://${auth()}@example.invalid") }.isFailure)
    }
    @Test fun importedAeadProfileUsesExistingCoreAndNativeExportRetainsIt() {
        val link="ss://${auth(password="synthetic")}@example.invalid:8388#Fixture"
        val profile=(ConfigImporter().parseAndImport(link) as ImportResult.Success).profiles.single()
        assertEquals(TunnelType.XRAY,profile.tunnelType)
        assertEquals(link,profile.coreLink)
        val restored=(ConfigImporter().parseAndImport(ConfigExporter().exportSingleProfile(profile)) as ImportResult.Success).profiles.single()
        assertEquals(link,restored.coreLink)
    }
}
