# gomobile calls Java classes, constructors, fields and callbacks by JNI name.
# Native-method rules alone do not preserve those non-native ABI members.
# Match the consumer rules normally emitted by gomobile's buildAAR.
-keep class go.** { *; }
-keep class mobile.** { *; }
-keep class vaydns.** { *; }
-keep class soar.** { *; }
-keep class snowflake.** { *; }
