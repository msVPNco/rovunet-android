#!/usr/bin/env python3
"""Run the private candidate's instrumentation suite on a disposable emulator."""
import os
import base64
import json
import select
import tempfile
from contextlib import contextmanager
from pathlib import Path
import re
import subprocess
import sys
import zipfile
from slipstream_fixture import slipstream_fixture

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = "app.veyra.client.candidate"
REQUIRED = {
    ("app.slipnet.presentation.StartupCoverDeviceTest", "brandCoverUsesAllThemesAndLanguagesAndReleasesContentWithoutAMinimumWait"),
    ("app.slipnet.service.StartupReviewDeviceTest", "deferredMapperDigestsKeepTheOriginalSnapshotAndRefuseStaleReviewedDeletion"),
    ("app.slipnet.service.MahsaSubscriptionDeviceTest", "legacyMahsaRefreshIsDirectAtomicAndRemovesOnlyUnmodifiedRetiredFeedRows"),
    ("app.slipnet.presentation.main.PublicSourcePrivacyDeviceTest", "bothBuiltinDialogsHideUrlsInFourLanguagesAndThreeThemes"),
    ("app.slipnet.presentation.main.PublicSourcePrivacyDeviceTest", "savedPublicSubscriptionCannotRevealOrCopyItsSourceWhilePrivateEntryRemainsEditable"),
    ("app.slipnet.core.CoreDeviceSmokeTest", "packagedAuxiliarySurvivesMaximumSocksDomainLengths"),
    ("app.slipnet.util.UpdateNetworkDeviceTest", "partialDownloadResumesAndStillRejectsWrongRangeOrChecksum"),
    ("app.slipnet.presentation.Alpha20UiDeviceTest", "failedTextIsRedAndLegalFooterStaysEnglishInFourLanguagesAndThreeThemes"),
    ("app.slipnet.util.UpdateNetworkDeviceTest", "authenticatedTunnelCarriesHttpsAndCdnRedirectWithoutLocalDns"),
    ("app.slipnet.util.UpdateNetworkDeviceTest", "cancellationClosesAStalledBodyAndSessionChangeNeverFallsBackToDirect"),
    ("app.slipnet.util.UpdateNetworkDeviceTest", "tunnelStillRejectsAnUntrustedTlsCertificate"),

    ("app.slipnet.presentation.MarqueeDeviceTest", "overflowingTextMovesInsideFixedSingleLineWhileShortTextStaysStill"),
    ("app.slipnet.core.CoreDeviceSmokeTest", "packagedSupplementalProtocolsOwnAuthenticatedEndpointsAndReleaseThem"),
    ("app.slipnet.presentation.main.SubscriptionUiDeviceTest", "cardUpdateStaysOnCardAndDisablesRepeatedRequests"),
    ("app.slipnet.core.CoreDeviceSmokeTest", "packagedAmneziaCoreOwnsAuthenticatedPortAndStopsWithoutAHandshake"),
    ("app.slipnet.service.SubscriptionDeviceTest", "providerMetadataAndOptionalNamePersistAtomicallyWithoutPanelRequests"),
    ("app.slipnet.service.SubscriptionDeviceTest", "sharedWebsocketHostsStayDistinctAndLegacyRefreshKeepsLocalRows"),
    ("app.slipnet.presentation.main.SoftPopUiDeviceTest", "interruptionMessagesKeepTheirSpecificReasonInAllLanguages"),
    ("app.slipnet.service.XrayChainDeviceTest", "xrayOuterCarriesSshAndTunUsesTheInnerEndpoint"),
    ("app.slipnet.presentation.main.SoftPopUiDeviceTest", "realHomeKeepsComfortableHeaderAcrossRtlAndThemes"),
    ("app.slipnet.service.SlipstreamIsolationDeviceTest", "pinnedQuicCarriesRealBytesAndRejectsAnotherCertificate"),
    ("app.slipnet.service.SlipstreamIsolationDeviceTest", "fourWorkersHaveIndependentProcessesAndReleaseCancelledSlots"),
    ("app.slipnet.presentation.main.PublicProviderUiDeviceTest", "allRejectedPreviewShowsReasonAndCannotCreateOrReplaceSource"),
    ("app.slipnet.core.CoreDeviceSmokeTest", "trojanVmessAndGrpcUseThePackagedCore"),
    ("app.slipnet.presentation.main.PublicProviderUiDeviceTest", "providerRequiresConsentCreatesOneSourceAndReaddingReviewsExistingCard"),
    ("app.slipnet.presentation.Alpha17UiDeviceTest", "pingShowsFreshOrPreviousTimestampAndAppBypassNeedsConfirmation"),
    ("app.slipnet.presentation.ConnectionHistoryDeviceTest", "selectedCardNeverReusesAnotherCollectionsLastConnection"),
    ("app.slipnet.presentation.Alpha17PreferencesDeviceTest", "freshPreferencesFollowSystemAndPreserveExplicitTheme"),
    ("app.slipnet.presentation.Alpha17UiDeviceTest", "languageWelcomeHasFourExplicitChoicesAndNoImportOrConnection"),
    ("app.slipnet.presentation.Alpha17UiDeviceTest", "expandedThreeThousandPathsComposeOnlyVisibleRowsAndScrollToLast"),
    ("app.slipnet.core.CoreDeviceSmokeTest", "stoppingIndependentCandidateLeavesLiveCoreAndPrivateEndpointWorking"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "prismWaitsForBothTransportsAndCountsResolverOnce"),
    ("app.slipnet.data.subscription.AccountStatusDeviceTest", "metadataCacheSurvivesSourceUpdatesAndRejectsDetachedOrReplacedSources"),
    ("app.slipnet.presentation.main.AccountStatusUiDeviceTest", "persianAccountSummaryKeepsTheCollectionCardAndDistinguishesUnknownFromExhausted"),
    ("app.slipnet.presentation.scanner.ScannerDeadlineDeviceTest", "advancedUsesOneDeadlineAndCancellationClosesAWaitingQuery"),
    ("app.slipnet.presentation.main.SubscriptionUiDeviceTest", "oneImportOrUpdateClickClosesTheWholeDialogAndFailuresKeepItOpen"),
    ("app.slipnet.service.SubscriptionDeviceTest", "newSourcePreservesTunnelOwnershipAndDuplicateImportsAreAtomic"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "oldProtocolProofIsDiscardedWithoutLosingImportedResolverCandidates"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "soarTransportDefaultsToBothAndDoesNotChangeTheOtherScannerPreference"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "fourSoarModesAndConnectionGuideUsePersianResources"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "tcpOnlyResolverPassesWhileUdpAndWrongServerKeyFail"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "prismRepeatsRealSoarHandshakesAndRejectsOrdinaryDnsReplies"),
    ("app.slipnet.presentation.scanner.SoarScannerDeviceTest", "fullHttpAndCancellationLeaveTheExistingSoarClientUntouched"),
    ("app.slipnet.service.NotificationStatusDeviceTest", "statusShapesAndAccentFollowActualConnectionState"),
    ("app.slipnet.presentation.main.DataPathNoteDeviceTest", "reopeningHomeDoesNotRenewAnExpiredSuccess"),
    ("app.slipnet.presentation.main.DataPathNoteDeviceTest", "failedCheckLivesInDetailsAndDoesNotLeaveAHomeRow"),
    ("app.slipnet.presentation.main.SubscriptionUiDeviceTest", "persianSubscriptionFormUsesCurrentResourcesAndRtlWithoutStartingADownload"),
    ("app.slipnet.service.SubscriptionDeviceTest", "updatesAreAtomicPreserveIdentityAndDoNotOverwriteLocalChanges"),
    ("app.slipnet.service.SubscriptionDeviceTest", "collectionQrRoundTripPreservesEveryProfileAndSharingRestrictions"),
    ("app.slipnet.service.PacketStopDeviceTest", "idlePacketEngineStopsAndCanRestartWithoutTraffic"),
    ("app.slipnet.service.PacketStopDeviceTest", "immediateStopCannotWaitForAnInitializerThatAlreadyExited"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "collectionMenuBrowsingAndExplicitConnectionRemainSeparate"),
    ("app.slipnet.presentation.scanner.ScannerLocalizationDeviceTest", "persianScannerUsesTranslatedLabelsAcrossAllModes"),
    ("app.slipnet.presentation.scanner.ScannerLocalizationDeviceTest", "changingLanguageUpdatesScannerLabelsWithoutLosingTransportSelection"),
    ("app.slipnet.presentation.scanner.ScannerLocalizationDeviceTest", "persianResultsAndValidationErrorsDoNotExposeEnglishBackendMessages"),

    ("app.slipnet.core.GomobileBindingDeviceTest", "nativeConstructorsAndReferenceCallbacksSurviveMinification"),
    ("app.slipnet.core.GomobileBindingDeviceTest", "nativeErrorConstructorSurvivesMinification"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "lockedPathDoesNotRevealTheRestoredDestinationLine"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "persianCollectionCountUsesCurrentResourcesWhileActivityContextStaysAlive"),
    ("app.slipnet.service.ServiceStartupCancellationDeviceTest", "disconnectIntentAbortsPendingSshBeforeJoiningStartup"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "collectionBrowsingNeverConnectsAndItsLocalActionWorksWhenCollapsed"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "firstTapSelectsSecondConnectsAndNestedActionsDoNotConnect"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "soarModesHaveDistinctLabelsFitRtlAndSelectTheActualWireValues"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "measuredHttpsLatencyRemainsVisibleWithoutConnecting"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "localCancelRemainsAvailableWhileConnectionIsBusy"),
    ("app.slipnet.presentation.CardInteractionDeviceTest", "persianCoreEditorUsesTranslatedLabelsAndLtrTechnicalValues"),
    ("app.slipnet.service.SshStartupCancellationDeviceTest", "stopClosesPendingSocksHandshakeAndNeverPublishesAListener"),
    ("app.slipnet.security.CredentialRecoveryTest", "damagedRowIsIsolatedWritesPreserveItAndExplicitRepairKeepsItsIdentity"),
    ("app.slipnet.service.VpnMtuTest", "tunAndNativeForwarderAgreeAcrossIpv6Modes"),
    ("app.slipnet.service.ScanLifecycleTest", "removingTheTaskKeepsTheRunButStoppingTheServiceReleasesIt"),
    ("app.slipnet.security.SshIdentityTest", "requestedTestsReviewIdentitiesAndSubsequentConnectionsReusePins"),
    ("app.slipnet.security.MigrationPerformanceTest", "firstProfileReadMigratesOffMainAndPreservesEveryCredential"),
    ("app.slipnet.presentation.ConnectionHistoryDeviceTest", "manualSshChoiceSurvivesCollectionChangesActivityReturnAndColdHistoryRead"),
    ("app.slipnet.presentation.ConnectionHistoryDeviceTest", "completeGroupResultsSurviveProbeCleanupSqlSortAndActivityReturn"),
}


@contextmanager
def ssh_fixture(adb):
    with tempfile.TemporaryDirectory(prefix="rovunet-ssh-fixture-") as directory:
        binary = str(Path(directory) / "server")
        subprocess.run(["go", "build", "-o", binary, "./cmd/device-ssh-fixture"],
                       cwd=ROOT / "cli", check=True, timeout=240)
        process = subprocess.Popen([binary], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            if not select.select([process.stdout], [], [], 15)[0]:
                raise RuntimeError("SSH fixture did not announce its loopback ports")
            ports = json.loads(process.stdout.readline())
            adb("reverse", "tcp:39222", f'tcp:{ports["ssh"]}')
            adb("reverse", "tcp:39223", f'tcp:{ports["tls"]}')
            yield
        finally:
            try:
                try:
                    adb("reverse", "--remove", "tcp:39222")
                finally:
                    adb("reverse", "--remove", "tcp:39223")
            finally:
                process.terminate()
                process.communicate(timeout=10)


@contextmanager
def soar_fixture():
    with tempfile.TemporaryDirectory(prefix="rovunet-soar-fixture-") as directory:
        binary = str(Path(directory) / "server")
        subprocess.run(["go", "build", "-o", binary, "."], cwd=ROOT / "tools/soar-fixture", check=True, timeout=240)
        process = subprocess.Popen([binary], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            if not select.select([process.stdout], [], [], 15)[0]:
                raise RuntimeError("Soar fixture did not announce its loopback ports")
            peers = json.loads(process.stdout.readline())
            yield base64.b64encode(json.dumps(peers).encode()).decode()
        finally:
            process.terminate()
            process.communicate(timeout=10)


def verified_tests(output, required=REQUIRED):
    status = {}
    passed = set()
    for line in output.splitlines():
        if line.startswith("INSTRUMENTATION_STATUS: "):
            key, _, value = line.removeprefix("INSTRUMENTATION_STATUS: ").partition("=")
            status[key] = value
        elif line.startswith("INSTRUMENTATION_STATUS_CODE: "):
            code = line.removeprefix("INSTRUMENTATION_STATUS_CODE: ")
            case = (status.get("class"), status.get("test"))
            if code == "0" and all(case) and case not in passed:
                passed.add(case)
            elif code != "1":
                raise ValueError(f"Test skipped, failed or malformed: {case}, status {code}")
            status = {}
    summary = re.search(r"^OK \((\d+) tests?\)$", output, re.M)
    if not summary or int(summary[1]) != len(passed) or "INSTRUMENTATION_CODE: -1" not in output.splitlines():
        raise ValueError("Incomplete instrumentation run")
    if required - passed:
        raise ValueError(f"Required device tests missing: {sorted(required - passed)}")
    return passed


def main():
    serial = os.environ["ANDROID_SERIAL"]
    def adb(*args, timeout=90):
        return subprocess.run(["adb", "-s", serial, *args], text=True,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=True, timeout=timeout).stdout

    if adb("shell", "getprop", "ro.kernel.qemu").strip() != "1":
        raise SystemExit("Select a disposable Android emulator with ANDROID_SERIAL")
    outputs = ROOT / "app/build/outputs/apk"
    app, = (outputs / "full/debug").glob("*-full-debug-universal.apk")
    tests, = (outputs / "androidTest/full/debug").glob("*-androidTest.apk")
    for apk in (app, tests):
        with zipfile.ZipFile(apk) as archive:
            if "AndroidManifest.xml" not in archive.namelist() or archive.testzip() is not None:
                raise ValueError(f"Incomplete APK: {apk.name}")
        print(f"Installing {apk.name}", flush=True)
        if "Success" not in adb("install", "-r", "-t", "-g", str(apk), timeout=300).splitlines():
            raise RuntimeError(f"Could not install {apk.name}")
    adb("shell", "cmd", "appops", "set", PACKAGE, "ACTIVATE_VPN", "allow")
    adb("shell", "am", "start", "-W", "-n", f"{PACKAGE}/app.slipnet.presentation.MainActivity")
    with ssh_fixture(adb), soar_fixture() as soar_peers, slipstream_fixture(ROOT) as slipstream_peer:
        print("Running device regressions with local SSH and TLS peers", flush=True)
        result = adb("shell", "am", "instrument", "-w", "-r", "-e", "testVpnMtu", "true",
                     "-e", "sshFixturePort", "39222", "-e", "tlsFixturePort", "39223",
                     "-e", "soarFixture", soar_peers, "-e", "slipstreamFixture", slipstream_peer,
                     f"{PACKAGE}.test/androidx.test.runner.AndroidJUnitRunner", timeout=900)
    print(result, flush=True)
    print(f"PASS: {len(verified_tests(result))} device tests, including VPN traffic across UIDs.")

    # Synthetic scanner screenshot: no real profile, address or credential data.
    previews = ROOT / "tools/veyra/device-results"
    previews.mkdir(exist_ok=True)
    for filename in ("scanner-fa-review02.png", "subscription-fa-code29.png", "account-status-fa-code32.png",
                     "home-alpha18-en-dark.png", "home-alpha18-fa-dark.png", "home-alpha18-fa-amoled.png", "home-alpha18-en-light.png"):
        with (previews / filename).open("wb") as image:
            subprocess.run(["adb", "-s", serial, "exec-out", "run-as", PACKAGE,
                            "cat", "files/" + filename], stdout=image, check=True)

    # Debug tests cannot detect JNI members removed by R8. Install the minified
    # private candidate and its matching Release instrumentation. Debug tests
    # cannot be reused against obfuscated application/dependency bytecode.
    release, = (outputs / "full/release").glob("*-full-release-universal.apk")
    release_tests, = (outputs / "androidTest/full/release").glob("*-androidTest.apk")
    subprocess.run([sys.executable, str(ROOT / "tools/quality/check-gomobile-apk.py"), str(release)], check=True)
    adb("shell", "am", "force-stop", PACKAGE)
    for apk in (release, release_tests):
        if "Success" not in adb("install", "-r", "-t", "-g", str(apk), timeout=300).splitlines():
            raise RuntimeError(f"Could not install the minified binding fixture: {apk.name}")
    result = adb("shell", "am", "instrument", "-w", "-r",
                 f"{PACKAGE}.test/app.slipnet.core.GomobileBindingInstrumentation", timeout=180)
    print(result, flush=True)
    required = {("app.slipnet.core.GomobileBindingChecks", case[1]) for case in REQUIRED
                if case[0] == "app.slipnet.core.GomobileBindingDeviceTest"}
    if verified_tests(result, required=required) != required:
        raise ValueError("Unexpected minified gomobile test set")
    print("PASS: 2 minified-Release JNI tests; constructors, callbacks and native errors.")


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        sys.exit(f"adb exited with {error.returncode}: {error.stdout}")
