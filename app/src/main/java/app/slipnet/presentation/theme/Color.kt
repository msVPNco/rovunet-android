package app.slipnet.presentation.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance

// Primary colors
val Purple80 = Color(0xFFD0BCFF)
val PurpleGrey80 = Color(0xFFCCC2DC)
val Pink80 = Color(0xFFEFB8C8)

val Purple40 = Color(0xFF6650a4)
val PurpleGrey40 = Color(0xFF625b71)
val Pink40 = Color(0xFF7D5260)

// Slipstream brand colors
val SlipstreamPrimary = Color(0xFF087F8C)
val SlipstreamPrimaryDark = Color(0xFF096473)
val SlipstreamPrimaryLight = Color(0xFF8DDDD8)
val SlipstreamSecondary = Color(0xFF03DAC6)
val SlipstreamSecondaryDark = Color(0xFF00A896)

// Status colors
val ConnectedGreen = Color(0xFF61DBA3)
val ConnectedGreenDark = Color(0xFF388E3C)
val DisconnectedRed = Color(0xFFF44336)
val DisconnectedRedDark = Color(0xFFD32F2F)
val ConnectingOrange = Color(0xFFFF9800)
val ConnectingOrangeDark = Color(0xFFF57C00)
val WarningYellow = Color(0xFFFFC107)

// Background colors
val SurfaceLight = Color(0xFFF5F8FA)
val SurfaceDark = Color(0xFF1C1B1F)
val BackgroundLight = Color(0xFFF5F8FA)
val BackgroundDark = Color(0xFF1C1B1F)

// AMOLED dark colors (pure black)
val SurfaceAmoled = Color(0xFF000000)
val BackgroundAmoled = Color(0xFF000000)
val SurfaceVariantAmoled = Color(0xFF1A1A1A)

// Traffic indicator colors
val UploadBlue = Color(0xFF087F8C)
val DownloadGreen = Color(0xFF4CAF50)

/** Status text keeps sufficient contrast on both light and dark surfaces. */
val ConnectionSuccessColor: Color
    @androidx.compose.runtime.Composable get() =
        if (androidx.compose.material3.MaterialTheme.colorScheme.background.luminance() > 0.5f) Color(0xFF126B46)
        else Color(0xFF61DBA3)
