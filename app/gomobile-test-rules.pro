# Test-only rules: keep the explicit instrumentation filter stable after R8.
-keep class app.slipnet.core.GomobileBindingDeviceTest { *; }
-keep class app.slipnet.core.GomobileBindingChecks { *; }
-keep class app.slipnet.core.GomobileBindingInstrumentation { *; }
