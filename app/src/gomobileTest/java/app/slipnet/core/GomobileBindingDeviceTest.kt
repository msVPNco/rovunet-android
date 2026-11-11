package app.slipnet.core

import org.junit.Test

/** Debug JUnit and Release framework instrumentation execute the same assertions. */
class GomobileBindingDeviceTest {
    @Test fun nativeConstructorsAndReferenceCallbacksSurviveMinification() =
        GomobileBindingChecks.nativeConstructorsAndReferenceCallbacksSurviveMinification()

    @Test fun nativeErrorConstructorSurvivesMinification() =
        GomobileBindingChecks.nativeErrorConstructorSurvivesMinification()
}
