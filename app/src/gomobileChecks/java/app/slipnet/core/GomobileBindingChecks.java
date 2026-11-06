package app.slipnet.core;

import mobile.DnsttClient;
import mobile.Mobile;
import soar.SoarClient;
import snowflake.SnowflakeClient;
import vaydns.VaydnsClient;

/** Test-only, shared native assertions; no AndroidX, Kotlin or JUnit runtime. */
public final class GomobileBindingChecks {
    private GomobileBindingChecks() {}
    public static void nativeConstructorsAndReferenceCallbacksSurviveMinification() {
        for (int i = 0; i < 8; i++) {
            DnsttClient dns = new DnsttClient();
            dns.setNoizMode(true);
            dns.setResolverMode("fanout");
            dns.setRRSpreadCount(1);
            if (dns.isRunning()) throw new AssertionError("Unstarted DNS client is running");
            dns.stop();
            VaydnsClient vay = new VaydnsClient();
            if (vay.isRunning()) throw new AssertionError("Unstarted Vay client is running");
            vay.stop();
            SoarClient soar = new SoarClient();
            if (soar.isRunning()) throw new AssertionError("Unstarted Soar client is running");
            soar.stop();
            SnowflakeClient snowflake = new SnowflakeClient();
            if (snowflake.isRunning()) throw new AssertionError("Unstarted Snowflake client is running");
            snowflake.stop();
        }
    }
    public static void nativeErrorConstructorSurvivesMinification() {
        try {
            Mobile.newClient("127.0.0.1:9", "fixture.invalid", "not-a-key", "127.0.0.1:0");
            throw new AssertionError("The native constructor accepted the synthetic invalid key");
        } catch (Exception failure) {
            if (!failure.getClass().getName().equals("go.Universe$proxyerror"))
                throw new AssertionError("Expected the native-created Go exception", failure);
            if (failure.getMessage() == null || failure.getMessage().isEmpty())
                throw new AssertionError("Native Go exception has no message");
        }
    }
}
