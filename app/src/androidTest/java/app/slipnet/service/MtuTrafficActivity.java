package app.slipnet.service;

import android.app.Activity;
import android.os.Bundle;
import android.os.Process;
import android.os.ResultReceiver;

import java.io.IOException;
import java.io.InputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

/** Runs in the test APK's own UID, using only the Android runtime visible to that process. */
public final class MtuTrafficActivity extends Activity {
    @Override
    @SuppressWarnings("deprecation")
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        ResultReceiver receiver = getIntent().getParcelableExtra("result");
        if (receiver == null) {
            finish();
            return;
        }
        new Thread(() -> fetch(receiver)).start();
    }

    private void fetch(ResultReceiver receiver) {
        Bundle reply = new Bundle();
        reply.putInt("uid", Process.myUid());
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress("198.18.0.13", 80), 8000);
            socket.setSoTimeout(8000);
            socket.getOutputStream().write("GET /mtu HTTP/1.0\r\n\r\n".getBytes(StandardCharsets.US_ASCII));
            InputStream input = socket.getInputStream();
            byte[] buffer = new byte[8192];
            int length = 0;
            while (length < buffer.length) {
                int count = input.read(buffer, length, buffer.length - length);
                if (count < 0) break;
                length += count;
            }
            String http = new String(buffer, 0, length, StandardCharsets.US_ASCII);
            int headerEnd = http.indexOf("\r\n\r\n");
            if (headerEnd < 0) throw new IOException("Missing response headers");
            reply.putString("body", http.substring(headerEnd + 4));
            receiver.send(RESULT_OK, reply);
        } catch (IOException error) {
            reply.putString("error", error.getClass().getSimpleName() + ": " + error.getMessage());
            receiver.send(RESULT_CANCELED, reply);
        } finally {
            runOnUiThread(this::finish);
        }
    }
}
