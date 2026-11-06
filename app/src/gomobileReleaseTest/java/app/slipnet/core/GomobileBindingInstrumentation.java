package app.slipnet.core;

import android.app.Activity;
import android.app.Instrumentation;
import android.os.Bundle;
import java.io.PrintWriter;
import java.io.StringWriter;

/** Framework-only runner: never depends on an AndroidX class R8 may rename. */
public final class GomobileBindingInstrumentation extends Instrumentation {
    @Override public void onCreate(Bundle arguments) {
        super.onCreate(arguments);
        start();
    }
    @Override public void onStart() {
        waitForIdleSync();
        String[] names = {
            "nativeConstructorsAndReferenceCallbacksSurviveMinification",
            "nativeErrorConstructorSurvivesMinification"
        };
        for (int index = 0; index < names.length; index++) {
            Bundle status = new Bundle();
            status.putString("class", GomobileBindingChecks.class.getName());
            status.putString("test", names[index]);
            status.putString("id", getClass().getName());
            status.putInt("current", index + 1);
            status.putInt("numtests", names.length);
            sendStatus(1, status);
            try {
                if (index == 0) GomobileBindingChecks.nativeConstructorsAndReferenceCallbacksSurviveMinification();
                else GomobileBindingChecks.nativeErrorConstructorSurvivesMinification();
                status.putString("stream", ".");
                sendStatus(0, status);
            } catch (Throwable failure) {
                // Only synthetic values enter this process; retain the actual failure.
                StringWriter trace = new StringWriter();
                failure.printStackTrace(new PrintWriter(trace));
                status.putString("stack", trace.toString());
                status.putString("stream", trace.toString());
                sendStatus(-2, status);
                Bundle result = new Bundle();
                result.putString("stream", "\nFAILURES!!!\n");
                finish(Activity.RESULT_CANCELED, result);
                return;
            }
        }
        Bundle result = new Bundle();
        result.putString("stream", "\nOK (2 tests)\n");
        finish(Activity.RESULT_OK, result);
    }
}
