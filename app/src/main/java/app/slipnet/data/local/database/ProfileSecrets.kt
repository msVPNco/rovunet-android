package app.slipnet.data.local.database

import androidx.sqlite.db.SupportSQLiteDatabase
import app.slipnet.data.local.DeviceSecrets
import org.json.JSONObject
import java.security.MessageDigest
import java.util.concurrent.CancellationException

internal object ProfileSecrets {
    private const val PURPOSE = "profile-credentials-v1"
    private val columns = listOf("ssh_password", "ssh_private_key", "ssh_key_passphrase", "socks_password", "naive_password", "coreLink", "vless_uuid", "lock_password_hash")

    private fun envelope(values: List<String>): String {
        val json = JSONObject()
        columns.forEachIndexed { i, name -> if (values[i].isNotEmpty()) json.put(name, values[i]) }
        return if (json.length() == 0) "" else DeviceSecrets.seal(json.toString(), PURPOSE)
    }

    fun seal(row: ProfileEntity): ProfileEntity {
        val sealed = envelope(listOf(row.sshPassword, row.sshPrivateKey, row.sshKeyPassphrase, row.socksPassword, row.naivePassword, row.coreLink, row.vlessUuid, row.lockPasswordHash))
        return withoutSecrets(row).copy(secretEnvelope = sealed)
    }

    fun withoutSecrets(row: ProfileEntity): ProfileEntity =
        row.copy(sshPassword = "",
            sshPrivateKey = "",
            sshKeyPassphrase = "",
            socksPassword = "",
            naivePassword = "",
            coreLink = "",
            vlessUuid = "",
            lockPasswordHash = "", secretEnvelope = "")

    fun token(row: ProfileEntity): String = android.util.Base64.encodeToString(
        MessageDigest.getInstance("SHA-256").digest(row.secretEnvelope.toByteArray(Charsets.UTF_8)),
        android.util.Base64.NO_WRAP
    )

    fun open(row: ProfileEntity): Result<ProfileEntity> {
        if (row.secretEnvelope.isEmpty()) return Result.success(row)
        return try {
            val json = JSONObject(DeviceSecrets.open(row.secretEnvelope, PURPOSE))
            require(json.length() > 0 && json.keys().asSequence().all { it in columns && json.get(it) is String })
            Result.success(row.copy(
                sshPassword = json.optString("ssh_password"), sshPrivateKey = json.optString("ssh_private_key"),
                sshKeyPassphrase = json.optString("ssh_key_passphrase"), socksPassword = json.optString("socks_password"),
                naivePassword = json.optString("naive_password"), coreLink = json.optString("coreLink"),
                vlessUuid = json.optString("vless_uuid"), lockPasswordHash = json.optString("lock_password_hash"),
                secretEnvelope = ""
            ))
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (error: Exception) {
            Result.failure(error)
        }
    }

    fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL("ALTER TABLE server_profiles ADD COLUMN secretEnvelope TEXT NOT NULL DEFAULT ''")
        db.query("PRAGMA secure_delete=ON").use { cursor ->
            check(cursor.moveToFirst() && cursor.getInt(0) == 1) { "Cannot enable secure deletion during profile migration" }
        }
        val select = columns.joinToString(",")
        val erase = columns.joinToString(",") { "$it=''" }
        var after = Long.MIN_VALUE
        while (true) {
            val rows = mutableListOf<Pair<Long, String>>()
            db.query("SELECT id,$select FROM server_profiles WHERE id > ? ORDER BY id LIMIT 32", arrayOf(after)).use { cursor ->
                while (cursor.moveToNext()) {
                    val fields = columns.indices.map { cursor.getString(it + 1).orEmpty() }
                    rows += cursor.getLong(0) to envelope(fields)
                }
            }
            if (rows.isEmpty()) return
            for ((id, sealed) in rows) {
                if (sealed.isNotEmpty()) db.execSQL("UPDATE server_profiles SET secretEnvelope=?, $erase WHERE id=?", arrayOf<Any>(sealed, id))
            }
            after = rows.last().first
        }
    }
}
