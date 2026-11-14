import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import java.util.Properties
import java.security.KeyStore
import java.security.MessageDigest
import java.security.cert.X509Certificate

plugins {
    id("com.android.application")
    id("org.mozilla.rust-android-gradle.rust-android")
    kotlin("android")
    id("com.google.devtools.ksp")
    id("org.jetbrains.kotlin.plugin.compose")
    id("com.google.dagger.hilt.android")
}

val veyraPrebuiltNative = providers.gradleProperty("veyraPrebuiltNative").orNull == "true"
val minSdkVersion = 24
val deviceCandidate = providers.gradleProperty("rovunetDeviceCandidate").orNull == "true"
val minifiedBindingTests = providers.gradleProperty("rovunetMinifiedBindingTests").orNull == "true"
require(!minifiedBindingTests || deviceCandidate) { "Minified binding instrumentation requires the private candidate" }
// Prepare a non-installable Release artifact for an offline signer; never a debug-signed public APK.
val unsignedPublication = providers.gradleProperty("rovunetUnsignedPublication").orNull == "true"
require(!(unsignedPublication && deviceCandidate)) { "Unsigned publication and private-candidate modes are separate" }
val appVersionName = if (deviceCandidate) "0.3.0-alpha22-review01" else "0.3.0-alpha22"
val appVersionCode = 47
val cargoProfile = (findProperty("CARGO_PROFILE") as String?) ?: run {
    val isRelease = gradle.startParameter.taskNames.any { it.contains("Release", ignoreCase = true) }
    if (isRelease) "release" else "debug"
}

fun abiFromTarget(target: String): String = when {
    target.startsWith("aarch64") -> "arm64-v8a"
    target.startsWith("armv7") || target.startsWith("arm") -> "armeabi-v7a"
    target.startsWith("i686") -> "x86"
    target.startsWith("x86_64") -> "x86_64"
    else -> target
}

// Signing configuration
val keystorePropertiesFile = rootProject.file("keystore.properties")
val keystoreProperties = Properties()
if (keystorePropertiesFile.exists()) {
    keystorePropertiesFile.inputStream().use(keystoreProperties::load)
}

fun requiredSigningProperty(name: String): String =
    keystoreProperties.getProperty(name)?.takeIf { it.isNotBlank() }
        ?: throw GradleException("Missing or blank '$name' in keystore.properties")

// Config encryption key from local.properties
val localPropertiesFile = rootProject.file("local.properties")
val localProperties = Properties()
if (localPropertiesFile.exists()) {
    localProperties.load(localPropertiesFile.inputStream())
}
val configEncryptionKey = localProperties.getProperty("CONFIG_ENCRYPTION_KEY", "")

// OpenSSL configuration
val opensslVersion = "3.0.15"
val opensslBaseDir = file("${System.getProperty("user.home")}/android-openssl/android-ssl")
val supportedAbis = listOf("arm64-v8a", "armeabi-v7a", "x86_64")

// Check if OpenSSL is available for all ABIs
fun isOpenSslAvailable(): Boolean {
    return supportedAbis.all { abi ->
        val libDir = opensslBaseDir.resolve("$abi/lib")
        val includeDir = opensslBaseDir.resolve("$abi/include")
        libDir.resolve("libssl.a").exists() &&
        libDir.resolve("libcrypto.a").exists() &&
        includeDir.exists()
    }
}

android {
    testBuildType = if (minifiedBindingTests) "release" else "debug"
    val javaVersion = JavaVersion.VERSION_17
    namespace = "app.slipnet"
    compileSdk = 36
    compileOptions {
        isCoreLibraryDesugaringEnabled = true
        sourceCompatibility = javaVersion
        targetCompatibility = javaVersion
    }

    signingConfigs {
        create("release") {
            if (keystorePropertiesFile.exists()) {
                storeFile = file(requiredSigningProperty("storeFile"))
                storePassword = requiredSigningProperty("storePassword")
                keyAlias = requiredSigningProperty("keyAlias")
                keyPassword = requiredSigningProperty("keyPassword")
            }
        }
    }

    defaultConfig {
        applicationId = if (deviceCandidate) "app.veyra.client.candidate" else "app.veyra.client"
        manifestPlaceholders["launcherName"] = if (deviceCandidate) "Rovunet Test" else "@string/app_name"
        buildConfigField("boolean", "PRIVATE_CANDIDATE", deviceCandidate.toString())
        minSdk = minSdkVersion
        targetSdk = 35
        versionCode = appVersionCode
        versionName = appVersionName

        testInstrumentationRunner = if (minifiedBindingTests)
            "app.slipnet.core.GomobileBindingInstrumentation"
        else "androidx.test.runner.AndroidJUnitRunner"

    }

    flavorDimensions += "edition"
    productFlavors {
        create("full") {
            dimension = "edition"
            buildConfigField("boolean", "INCLUDE_TOR", "true")
            buildConfigField("boolean", "INCLUDE_NAIVE", "true")
        }
        create("lite") {
            dimension = "edition"
            applicationIdSuffix = ".lite"
            versionNameSuffix = "-lite"
            buildConfigField("boolean", "INCLUDE_TOR", "false")
            buildConfigField("boolean", "INCLUDE_NAIVE", "false")
        }
    }

    buildTypes {
        release {
            isShrinkResources = true
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro", "gomobile-consumer-rules.pro")
            testProguardFiles("gomobile-test-rules.pro")
            signingConfig = when {
                unsignedPublication -> null
                deviceCandidate -> signingConfigs.getByName("debug")
                else -> signingConfigs.getByName("release")
            }
        }
    }

    sourceSets["androidTest"].assets.srcDir("$projectDir/schemas")
    sourceSets["androidTest"].java.apply {
        if (minifiedBindingTests) setSrcDirs(listOf("src/gomobileReleaseTest/java"))
        else srcDir("src/gomobileTest/java")
        srcDir("src/gomobileChecks/java")
    }
    if (minifiedBindingTests) sourceSets["androidTest"].manifest.srcFile("src/gomobileReleaseTest/AndroidManifest.xml")

    buildFeatures {
        compose = true
        buildConfig = true
    }

    applicationVariants.all {
        outputs.all {
            val output = this as com.android.build.gradle.internal.api.BaseVariantOutputImpl
            val abiFilter = output.getFilter("ABI")
            val abi = abiFilter ?: "universal"
            val unsignedSuffix = if (unsignedPublication && buildType.name == "release") "-unsigned" else ""
            output.outputFileName = "Rovunet-v${appVersionName}-${flavorName}-${buildType.name}-${abi}${unsignedSuffix}.apk"
        }
    }

    splits {
        abi {
            isEnable = true
            reset()
            include("arm64-v8a", "armeabi-v7a", "x86_64")
            isUniversalApk = true
        }
    }

    ndkVersion = "29.0.14206865"
    packaging {
        jniLibs {
            useLegacyPackaging = true
            // Do not advertise a 32-bit x86 ABI containing only dependency stubs.
            excludes += "**/x86/**"
        }
    }

    // Build hev-socks5-tunnel with ndk-build
    if (!veyraPrebuiltNative) externalNativeBuild {
        ndkBuild {
            path = file("src/main/cpp/Android.mk")
        }
    }

}

val verifyPublicationSigning = tasks.register("verifyPublicationSigning") {
    doLast {
        val pinnedCertificate = rootProject.file("config/release-signing-certificate.sha256")
            .readText().trim().lowercase()
        require(pinnedCertificate.matches(Regex("[0-9a-f]{64}"))) { "Invalid pinned production signing certificate" }
        val configuredCertificate = providers.gradleProperty("rovunetReleaseCertSha256")
            .orElse(providers.environmentVariable("ROVUNET_RELEASE_CERT_SHA256")).orNull
            ?.trim()?.lowercase()?.takeIf { it.isNotEmpty() }
        require(configuredCertificate == null || configuredCertificate == pinnedCertificate) {
            "Configured production certificate differs from the repository pin; do not rotate the release key implicitly"
        }
        val store = KeyStore.getInstance(KeyStore.getDefaultType())
        val storeFile = file(requiredSigningProperty("storeFile"))
        val alias = requiredSigningProperty("keyAlias")
        requiredSigningProperty("keyPassword")
        storeFile.inputStream().use { store.load(it, requiredSigningProperty("storePassword").toCharArray()) }
        val cert = store.getCertificate(alias) as? X509Certificate
            ?: throw GradleException("No X.509 signing certificate for keyAlias in the configured keystore")
        val digest = MessageDigest.getInstance("SHA-256").digest(cert.encoded).joinToString("") { "%02x".format(it) }
        require(digest == pinnedCertificate && !cert.subjectX500Principal.name.contains("CN=Android Debug", ignoreCase = true)) {
            "The release key must match the approved production signing identity"
        }
    }
}
tasks.matching { it.name == "validateSigningFullRelease" || it.name == "validateSigningLiteRelease" }
    .configureEach { if (!deviceCandidate && !unsignedPublication) dependsOn(verifyPublicationSigning) }

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

val cargoHome = System.getenv("CARGO_HOME") ?: "${System.getProperty("user.home")}/.cargo"
val cargoBin = "$cargoHome/bin"

// Task to setup OpenSSL for Android
tasks.register("setupOpenSsl") {
    group = "build setup"
    description = "Downloads and sets up OpenSSL for Android if not present"

    doLast {
        if (isOpenSslAvailable()) {
            println("OpenSSL for Android is already available at: $opensslBaseDir")
            return@doLast
        }

        println("OpenSSL for Android not found. Setting up...")

        exec {
            commandLine("bash", file("scripts/build-openssl-android.sh").absolutePath)
            environment("OPENSSL_VERSION", opensslVersion)
            environment("OUTPUT_DIR", opensslBaseDir.absolutePath)
            environment("ANDROID_NDK_HOME", android.ndkDirectory.absolutePath)
        }
        check(isOpenSslAvailable()) { "OpenSSL build did not produce the required architectures" }
    }
}

// Task to verify OpenSSL setup
tasks.register("verifyOpenSsl") {
    group = "verification"
    description = "Verifies that OpenSSL for Android is properly set up"

    doLast {
        val missing = mutableListOf<String>()

        supportedAbis.forEach { abi ->
            val libDir = opensslBaseDir.resolve("$abi/lib")
            val includeDir = opensslBaseDir.resolve("$abi/include")

            if (!libDir.resolve("libssl.a").exists()) {
                missing.add("$abi/lib/libssl.a")
            }
            if (!libDir.resolve("libcrypto.a").exists()) {
                missing.add("$abi/lib/libcrypto.a")
            }
            if (!includeDir.resolve("openssl").exists()) {
                missing.add("$abi/include/openssl/")
            }
        }

        if (missing.isNotEmpty()) {
            throw GradleException("""
                OpenSSL for Android is not properly set up.
                Missing files in $opensslBaseDir:
                ${missing.joinToString("\n  - ", prefix = "  - ")}

                Run './gradlew setupOpenSsl' to download and set up OpenSSL.
            """.trimIndent())
        }

        println("OpenSSL verification passed! All required files present.")
    }
}

cargo {
    cargoCommand = "$cargoBin/cargo"
    rustcCommand = "$cargoBin/rustc"
    module = "src/main/rust/slipstream-rust"
    libname = "slipstream"
    targets = listOf("arm", "arm64", "x86_64")
    profile = cargoProfile
    rustupChannel = "stable"
    extraCargoBuildArguments = listOf(
        "-p", "slipstream-client",
        "--lib",  // Only build the library, not the binary (avoids overwriting cdylib with executable)
        "--features", "openssl-static,picoquic-minimal-build",
    )
    exec = { spec, toolchain ->
        // Add cargo to PATH
        val currentPath = System.getenv("PATH") ?: ""
        spec.environment("PATH", "$cargoHome/bin:$currentPath")
        // Try python3 first, fall back to python
        // The rust-android-gradle plugin will handle errors if python is not available
        spec.environment("RUST_ANDROID_GRADLE_PYTHON_COMMAND", "python3")
        spec.environment(
            "RUST_ANDROID_GRADLE_CC_LINK_ARG",
            "-Wl,-z,max-page-size=16384,-soname,lib$libname.so"
        )
        spec.environment(
            "RUST_ANDROID_GRADLE_LINKER_WRAPPER_PY",
            "$projectDir/src/main/rust/linker-wrapper.py"
        )
        spec.environment(
            "RUST_ANDROID_GRADLE_TARGET",
            "target/${toolchain.target}/$cargoProfile/lib$libname.so"
        )
        val abi = abiFromTarget(toolchain.target)
        spec.environment("ANDROID_NDK_HOME", android.ndkDirectory.absolutePath)
        spec.environment("ANDROID_ABI", abi)
        spec.environment("ANDROID_PLATFORM", "android-$minSdkVersion")
        spec.environment(
            "PICOQUIC_BUILD_DIR",
            "$projectDir/src/main/rust/slipstream-rust/.picoquic-build/$abi"
        )
        spec.environment("PICOQUIC_AUTO_BUILD", "1")
        spec.environment("BUILD_TYPE", if (cargoProfile == "release") "Release" else "Debug")

        // Add OpenSSL paths for picoquic build and openssl-sys
        val opensslAbiDir = opensslBaseDir.resolve(abi)
        spec.environment("OPENSSL_DIR", opensslAbiDir.absolutePath)
        spec.environment("OPENSSL_LIB_DIR", opensslAbiDir.resolve("lib").absolutePath)
        spec.environment("OPENSSL_INCLUDE_DIR", opensslAbiDir.resolve("include").absolutePath)
        // For picoquic build script
        spec.environment("OPENSSL_ROOT_DIR", opensslAbiDir.absolutePath)
        spec.environment("OPENSSL_CRYPTO_LIBRARY", opensslAbiDir.resolve("lib/libcrypto.a").absolutePath)
        spec.environment("OPENSSL_SSL_LIBRARY", opensslAbiDir.resolve("lib/libssl.a").absolutePath)
        spec.environment("OPENSSL_USE_STATIC_LIBS", "1")

        // Pass config encryption key to Rust build.rs for obfuscation
        require(configEncryptionKey.matches(Regex("[A-Fa-f0-9]{64}")) && configEncryptionKey.any { it != '0' }) {
            "A non-zero, 32-byte CONFIG_ENCRYPTION_KEY is required when rebuilding the native engine"
        }
        spec.environment("CONFIG_ENCRYPTION_KEY", configEncryptionKey)

        // Remap the build-machine home directory out of Rust panic strings.
        // CARGO_ENCODED_RUSTFLAGS is appended to rustflags in .cargo/config.toml
        // so the link-arg flags there are preserved.
        val homeDir = System.getProperty("user.home")
        spec.environment("CARGO_ENCODED_RUSTFLAGS", "--remap-path-prefix=${homeDir}=~")

        val toolchainPrebuilt = android.ndkDirectory
            .resolve("toolchains/llvm/prebuilt")
            .listFiles()
            ?.firstOrNull { it.isDirectory }
        val toolchainBin = toolchainPrebuilt?.resolve("bin")
        if (toolchainBin != null) {
            spec.environment("AR", toolchainBin.resolve("llvm-ar").absolutePath)
            spec.environment("RANLIB", toolchainBin.resolve("llvm-ranlib").absolutePath)
        }
    }
}

// Make cargo build tasks depend on OpenSSL verification
tasks.whenTaskAdded {
    when (name) {
        "cargoBuildArm", "cargoBuildArm64", "cargoBuildX86_64" -> {
            dependsOn("verifyOpenSsl")
            doFirst {
                throw GradleException("The legacy cargo packaging path would replace the configuration codec. Run python3 tools/veyra/prepare-native.py, then Gradle with -PveyraPrebuiltNative=true; the client and legacy codec must remain separate.")
            }
        }
        "mergeFullDebugJniLibFolders", "mergeFullReleaseJniLibFolders",
        "mergeLiteDebugJniLibFolders", "mergeLiteReleaseJniLibFolders" -> {
            if (!veyraPrebuiltNative) dependsOn("cargoBuild")
            // Track Rust JNI output without adding a second source set (avoids duplicate resources).
            if (!veyraPrebuiltNative) inputs.dir(layout.buildDirectory.dir("rustJniLibs/android"))
        }
    }
}

tasks.register<Exec>("cargoClean") {
    executable("$cargoBin/cargo")
    args("clean")
    workingDir("$projectDir/${cargo.module}")
}
tasks.named("clean") {
    dependsOn("cargoClean")
}

dependencies {
    // Backport java.time without raising the supported Android7/API24 floor.
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.5")
    // Go libraries — flavor-specific AARs built via: cd gomobile-build && make build
    // Full: DNSTT + Snowflake, Lite: DNSTT only (smaller binary)
    "fullImplementation"(files("libs/golibs-full.aar"))
    "liteImplementation"(files("libs/golibs-lite.aar"))

    // Tor binary for Snowflake tunnel — libtor.so extracted from
    // info.guardianproject:tor-android:0.4.9.5 into jniLibs/
    // (AAR excluded as Gradle dep because its Kotlin metadata 2.3.0
    //  is incompatible with Room 2.6.1 kapt processor)

    // Compose BOM
    val composeBom = platform("androidx.compose:compose-bom:2026.01.01")
    implementation(composeBom)
    androidTestImplementation(composeBom)

    // Compose
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-graphics")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")

    // Activity Compose
    implementation("androidx.activity:activity-compose:1.12.4")

    // Lifecycle & ViewModel
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.10.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.10.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.10.0")

    // Navigation Compose
    implementation("androidx.navigation:navigation-compose:2.9.7")

    // Hilt
    implementation("com.google.dagger:hilt-android:2.57.1")
    ksp("com.google.dagger:hilt-compiler:2.57.1")
    implementation("androidx.hilt:hilt-navigation-compose:1.3.0")

    // Room
    implementation( "androidx.room:room-runtime:2.8.4")
    implementation("androidx.room:room-ktx:2.8.4")
    ksp("androidx.room:room-compiler:2.8.4")

    // DataStore
    implementation("androidx.datastore:datastore-preferences:1.2.0")

    // Kotlin Coroutines
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")

    // JSON serialization for Room converters
    implementation("com.google.code.gson:gson:2.13.2")

    // SSH tunneling library (mwiede fork — supports modern ciphers: AES-GCM, ChaCha20)
    implementation("com.github.mwiede:jsch:2.27.7")
    // BouncyCastle for curve25519 key exchange (Android JCE lacks XDH/X25519 on many devices)
    implementation("org.bouncycastle:bcprov-jdk18on:1.80")

    // OkHttp for DoH (HTTP/2, connection pooling, custom DNS resolver)
    implementation("com.squareup.okhttp3:okhttp:5.3.2")

    // Drag-and-drop reorderable LazyColumn
    implementation("sh.calvin.reorderable:reorderable:3.0.0")

    // QR code generation & scanning
    implementation("com.google.zxing:core:3.5.4")
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")

    // Core
    implementation("androidx.core:core-ktx:1.17.0")

    // Testing
    implementation("androidx.work:work-runtime:2.11.2")
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.json:json:20250517")
    if (!minifiedBindingTests) {
        androidTestImplementation("androidx.test:runner:1.7.0")
        androidTestImplementation("androidx.test.espresso:espresso-core:3.7.0")
        androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    }
}

ksp {
    // Room schema export
    arg("room.schemaLocation", "$projectDir/schemas")
}

// Missing native assets must fail the build rather than advertise unusable protocols.
val verifyVeyraCore by tasks.registering {
    doLast {
        for (abi in listOf("arm64-v8a", "armeabi-v7a", "x86_64")) {
            for (binary in listOf("libveyra_xray.so", "libveyra_core_launcher.so")) {
                check(file("src/main/jniLibs/$abi/$binary").isFile) {
                    "Missing Veyra core for $abi; run tools/veyra/prepare-native.py first"
                }
            }
        }
    }
}
tasks.matching { it.name.startsWith("merge") && it.name.endsWith("JniLibFolders") }.configureEach { dependsOn(verifyVeyraCore) }
