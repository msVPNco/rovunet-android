#!/usr/bin/env python3
"""Check that a successful-looking skipped/incomplete device run cannot pass CI."""
from pathlib import Path
import runpy

gate = runpy.run_path(str(Path(__file__).with_name("run-device-gate.py")))
verify = gate["verified_tests"]
required = gate["REQUIRED"]
mtu = ("app.slipnet.service.VpnMtuTest", "tunAndNativeForwarderAgreeAcrossIpv6Modes")
assert mtu in required
history = {
    ("app.slipnet.presentation.ConnectionHistoryDeviceTest", "manualSshChoiceSurvivesCollectionChangesActivityReturnAndColdHistoryRead"),
    ("app.slipnet.presentation.ConnectionHistoryDeviceTest", "completeGroupResultsSurviveProbeCleanupSqlSortAndActivityReturn"),
}
assert history <= required
startup = ("app.slipnet.service.SshStartupCancellationDeviceTest", "stopClosesPendingSocksHandshakeAndNeverPublishesAListener")
assert startup in required


def result(cases):
    lines = []
    for classname, method in sorted(cases):
        for code in (1, 0):
            lines.extend([
                "INSTRUMENTATION_STATUS: class=" + classname,
                "INSTRUMENTATION_STATUS: test=" + method,
                "INSTRUMENTATION_STATUS_CODE: " + str(code),
            ])
    return "\n".join(lines + [f"OK ({len(cases)} tests)", "INSTRUMENTATION_CODE: -1"])


valid = result(required)
assert verify(valid) == required
release_required = {("app.slipnet.core.GomobileBindingChecks", method)
                    for classname, method in required if classname.endswith("GomobileBindingDeviceTest")}
assert len(release_required) == 2
assert verify(result(release_required), required=release_required) == release_required
for output in (result(set()), result(release_required).replace("STATUS_CODE: 0", "STATUS_CODE: -2", 1)):
    try:
        verify(output, required=release_required)
    except ValueError:
        continue
    raise AssertionError("Framework Release gate accepted missing/failed JNI cases")
invalid_runs = {
    "skipped": valid.replace("STATUS_CODE: 0", "STATUS_CODE: -3", 1),
    "failed": valid.replace("STATUS_CODE: 0", "STATUS_CODE: -2", 1),
    "interrupted": valid.replace("INSTRUMENTATION_CODE: -1", ""),
    "missing service cancellation": result(required - {("app.slipnet.service.ServiceStartupCancellationDeviceTest", "disconnectIntentAbortsPendingSshBeforeJoiningStartup")}),
    "missing MTU": result(required - {mtu}),
    "no tests": result(set()),
    "missing cancellable SSH startup": result(required - {startup}),
}
for case in {c for c in required if c[0].endswith("CardInteractionDeviceTest")}:
    invalid_runs["missing UI " + case[1]] = result(required - {case})
for case in history:
    invalid_runs["missing history " + case[1]] = result(required - {case})
for label, output in invalid_runs.items():
    try:
        verify(output)
    except ValueError:
        continue
    raise AssertionError(f"Device gate accepted {label}")
print("PASS device gate: VPN test required, skipped/failed/incomplete runs rejected")
