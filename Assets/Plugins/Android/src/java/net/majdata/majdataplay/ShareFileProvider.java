package net.majdata.majdataplay;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

/**
 * 分享成绩用：把分享文件夹里的文件以只读的 content:// 提供给其他 App，不依赖 androidx。
 * authorities 为 包名 + ".majshare"，在 AndroidManifest.xml 中以 ${applicationId}.majshare 声明
 */
public class ShareFileProvider extends ContentProvider {
    private static final String PREFS = "majdataplay_share";
    private static final String KEY_DIRECTORY = "directory";

    static String getAuthority(Context context) {
        return context.getPackageName() + ".majshare";
    }

    static Uri getUri(Context context, File file) {
        return new Uri.Builder()
                .scheme("content")
                .authority(getAuthority(context))
                .appendPath(file.getName())
                .build();
    }

    /**
     * 只允许访问这个文件夹里的文件
     */
    static void setShareDirectory(Context context, String directory) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString(KEY_DIRECTORY, directory)
                .apply();
    }

    private File resolve(Uri uri) throws FileNotFoundException {
        Context context = getContext();
        String directory = context == null ? null
                : context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(KEY_DIRECTORY, null);
        String name = uri.getLastPathSegment();
        if (directory == null || name == null) {
            throw new FileNotFoundException(uri.toString());
        }
        try {
            File root = new File(directory).getCanonicalFile();
            File file = new File(root, name).getCanonicalFile();
            if (!root.equals(file.getParentFile()) || !file.isFile()) {
                throw new FileNotFoundException(uri.toString());
            }
            return file;
        } catch (IOException e) {
            throw new FileNotFoundException(uri.toString());
        }
    }

    @Override
    public boolean onCreate() {
        return true;
    }

    @Override
    public String getType(Uri uri) {
        String name = uri.getLastPathSegment();
        if (name == null) {
            return "application/octet-stream";
        }
        name = name.toLowerCase();
        if (name.endsWith(".png")) {
            return "image/png";
        } else if (name.endsWith(".csv")) {
            return "text/csv";
        } else if (name.endsWith(".json")) {
            return "application/json";
        } else if (name.endsWith(".txt")) {
            return "text/plain";
        }
        return "application/octet-stream";
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        return ParcelFileDescriptor.open(resolve(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        File file;
        try {
            file = resolve(uri);
        } catch (FileNotFoundException e) {
            return null;
        }
        String[] columns = projection != null ? projection
                : new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE};
        MatrixCursor cursor = new MatrixCursor(columns, 1);
        Object[] row = new Object[columns.length];
        for (int i = 0; i < columns.length; i++) {
            if (OpenableColumns.DISPLAY_NAME.equals(columns[i])) {
                row[i] = file.getName();
            } else if (OpenableColumns.SIZE.equals(columns[i])) {
                row[i] = file.length();
            }
        }
        cursor.addRow(row);
        return cursor;
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;
    }
}
