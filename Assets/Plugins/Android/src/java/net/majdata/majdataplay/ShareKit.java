package net.majdata.majdataplay;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.util.Log;

import java.io.File;
import java.util.ArrayList;

/**
 * 分享成绩：弹出系统的分享选择器。文件通过 {@link ShareFileProvider} 以 content:// 提供给其他 App
 */
public final class ShareKit {
    private static final String TAG = "MajdataPlay";

    private ShareKit() {
    }

    public static void shareFiles(final Activity activity, final String[] paths, final String mimeType, final String title) {
        if (activity == null || paths == null || paths.length == 0) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    ShareFileProvider.setShareDirectory(activity, new File(paths[0]).getParent());
                    ArrayList<Uri> uris = new ArrayList<>();
                    for (String path : paths) {
                        uris.add(ShareFileProvider.getUri(activity, new File(path)));
                    }
                    Intent intent;
                    if (uris.size() == 1) {
                        intent = new Intent(Intent.ACTION_SEND);
                        intent.putExtra(Intent.EXTRA_STREAM, uris.get(0));
                    } else {
                        intent = new Intent(Intent.ACTION_SEND_MULTIPLE);
                        intent.putParcelableArrayListExtra(Intent.EXTRA_STREAM, uris);
                    }
                    intent.setType(mimeType);
                    ClipData clip = ClipData.newRawUri(title, uris.get(0));
                    for (int i = 1; i < uris.size(); i++) {
                        clip.addItem(new ClipData.Item(uris.get(i)));
                    }
                    intent.setClipData(clip);
                    intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                    Intent chooser = Intent.createChooser(intent, title);
                    chooser.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                    activity.startActivity(chooser);
                } catch (Exception e) {
                    Log.e(TAG, "Failed to share files", e);
                }
            }
        });
    }

    public static void shareText(final Activity activity, final String text, final String title) {
        if (activity == null || text == null) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    Intent intent = new Intent(Intent.ACTION_SEND);
                    intent.setType("text/plain");
                    intent.putExtra(Intent.EXTRA_SUBJECT, title);
                    intent.putExtra(Intent.EXTRA_TEXT, text);
                    activity.startActivity(Intent.createChooser(intent, title));
                } catch (Exception e) {
                    Log.e(TAG, "Failed to share text", e);
                }
            }
        });
    }
}
