package com.amirnourhan.goldbar;

import android.content.ContentValues;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;
import android.text.Editable;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.TextPaint;
import android.text.TextWatcher;
import android.text.method.LinkMovementMethod;
import android.text.style.ClickableSpan;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

public class MainActivityV106 extends MainActivity {
    private static final int GOLD = Color.rgb(247, 211, 112);
    private static final int DARK = Color.rgb(18, 20, 25);
    private static final int REQUEST_SAVE_REPORT = 5106;
    private final DecimalFormat reportDf = new DecimalFormat("0.###", DecimalFormatSymbols.getInstance(Locale.US));
    private String pendingReport;
    private String pendingReportName;
    private boolean quickSyncReady;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        decorateHeader();
        renameQuickTools();
        addReportButton();
        applyQuickPrefsToApp();
        wireQuickCalculatorSync();
        quickSyncReady = true;
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (quickSyncReady) applyQuickPrefsToApp();
    }

    private void decorateHeader() {
        TextView title = findTextExact(findViewById(android.R.id.content), "GOLD BAR");
        if (title == null) return;

        String full = "GOLD BAR (by:Amirnourhan)";
        SpannableString span = new SpannableString(full);
        int start = full.indexOf("Amirnourhan");
        int end = start + "Amirnourhan".length();
        span.setSpan(new ClickableSpan() {
            @Override public void onClick(View widget) { openInstagram(); }
            @Override public void updateDrawState(TextPaint ds) {
                ds.setColor(GOLD);
                ds.setUnderlineText(false);
                ds.setFakeBoldText(true);
            }
        }, start, end, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        title.setText(span);
        title.setMovementMethod(LinkMovementMethod.getInstance());
        title.setHighlightColor(Color.TRANSPARENT);
        title.setContentDescription("gold-bar-title");
    }

    private void renameQuickTools() {
        TextView tools = findTextExact(findViewById(android.R.id.content), "ابزارهای سریع اکسل");
        if (tools != null) tools.setText("محاسبه سریع");
    }

    private void openInstagram() {
        try {
            Intent app = new Intent(Intent.ACTION_VIEW, Uri.parse("instagram://user?username=4mirnourhan"));
            app.setPackage("com.instagram.android");
            startActivity(app);
        } catch (Exception ignored) {
            Intent web = new Intent(Intent.ACTION_VIEW, Uri.parse("https://www.instagram.com/4mirnourhan/"));
            startActivity(web);
        }
    }

    private void addReportButton() {
        ScrollView scroll = findFirst(findViewById(android.R.id.content), ScrollView.class);
        if (scroll == null || scroll.getChildCount() == 0 || !(scroll.getChildAt(0) instanceof LinearLayout)) return;
        LinearLayout body = (LinearLayout) scroll.getChildAt(0);

        Button save = new Button(this);
        save.setText("ذخیره گزارش کامل");
        save.setContentDescription("save-report-button");
        save.setTextSize(14);
        save.setAllCaps(false);
        save.setTextColor(Color.rgb(22, 16, 3));
        GradientDrawable bg = new GradientDrawable(
                GradientDrawable.Orientation.LEFT_RIGHT,
                new int[]{GOLD, Color.rgb(184, 130, 23)});
        bg.setCornerRadius(dpLocal(16));
        save.setBackground(bg);
        save.setOnClickListener(v -> saveFullReport());

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dpLocal(56));
        lp.topMargin = dpLocal(14);
        lp.bottomMargin = dpLocal(12);
        body.addView(save, lp);
    }

    private void saveFullReport() {
        pendingReport = buildFullReport();
        pendingReportName = "GoldBar_" + new SimpleDateFormat("yyyy-MM-dd_HH-mm-ss", Locale.US).format(new Date()) + ".txt";

        if (Build.VERSION.SDK_INT >= 29) {
            try {
                ContentValues values = new ContentValues();
                values.put(MediaStore.MediaColumns.DISPLAY_NAME, pendingReportName);
                values.put(MediaStore.MediaColumns.MIME_TYPE, "text/plain");
                values.put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/GoldBar");
                Uri uri = getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
                if (uri == null) throw new IllegalStateException("MediaStore insert failed");
                try (OutputStream out = getContentResolver().openOutputStream(uri)) {
                    if (out == null) throw new IllegalStateException("Output stream unavailable");
                    out.write(pendingReport.getBytes(StandardCharsets.UTF_8));
                }
                Toast.makeText(this, "گزارش در Downloads/GoldBar ذخیره شد", Toast.LENGTH_LONG).show();
                return;
            } catch (Exception ignored) {
                // Fallback to Storage Access Framework below.
            }
        }

        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("text/plain");
        intent.putExtra(Intent.EXTRA_TITLE, pendingReportName);
        startActivityForResult(intent, REQUEST_SAVE_REPORT);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_SAVE_REPORT || resultCode != RESULT_OK || data == null || data.getData() == null) return;
        try (OutputStream out = getContentResolver().openOutputStream(data.getData())) {
            if (out == null) throw new IllegalStateException("Output stream unavailable");
            out.write((pendingReport == null ? buildFullReport() : pendingReport).getBytes(StandardCharsets.UTF_8));
            Toast.makeText(this, "گزارش ذخیره شد", Toast.LENGTH_LONG).show();
        } catch (Exception e) {
            Toast.makeText(this, "ذخیره گزارش انجام نشد", Toast.LENGTH_LONG).show();
        }
    }

    private String buildFullReport() {
        List<GoldEntry> entries = loadSavedEntries();
        GoldCalculator.Summary s = GoldCalculator.summarize(entries, null);

        double raiseTarget = valueOf("raise-target-assay", 747);
        double highBar = valueOf("high-bar-assay", 995);
        double lowerTarget = valueOf("lower-target-assay", 746);
        double silverPercent = valueOf("silver-percent", 32);
        double splitBase = valueOf("quick-split-base", 800);
        double correctionWeight = valueOf("quick-correction-weight", 250);
        double correctionTarget = valueOf("quick-correction-target", 750);
        double correctionDrop = valueOf("quick-correction-drop", 1);

        GoldCalculator.Adjustment raise = GoldCalculator.requiredHighAssayBar(s, raiseTarget, highBar);
        GoldCalculator.Alloy lower = GoldCalculator.requiredAlloy(s, lowerTarget, silverPercent, s.weight);
        double split3679 = GoldCalculator.split3679(splitBase);
        double correctionAdd = GoldCalculator.correctionAddition(correctionWeight, correctionTarget, correctionDrop);

        StringBuilder b = new StringBuilder();
        b.append("GOLD BAR (by:Amirnourhan)\n");
        b.append("گزارش کامل محاسبات طلا\n");
        b.append("تاریخ و ساعت: ")
                .append(new SimpleDateFormat("yyyy/MM/dd HH:mm:ss", Locale.US).format(new Date()))
                .append("\n\n");

        b.append("=== آبشده‌ها ===\n");
        if (entries.isEmpty()) {
            b.append("آبشده‌ای ثبت نشده است.\n");
        } else {
            for (int i = 0; i < entries.size(); i++) {
                GoldEntry e = entries.get(i);
                b.append(i + 1).append(") وزن: ").append(n(e.weight))
                        .append(" g | عیار: ").append(n(e.assay))
                        .append(" | وزن×عیار: ").append(n(e.weight * e.assay)).append("\n");
            }
        }
        b.append("تعداد آبشده: ").append(s.count).append("\n");
        b.append("وزن کل: ").append(n(s.weight)).append(" g\n");
        b.append("مجموع وزن×عیار: ").append(n(s.weightedSum)).append("\n");
        b.append("عیار میانگین: ").append(n(s.averageAssay)).append("\n\n");

        b.append("=== بالا بردن عیار ===\n");
        b.append("عیار هدف: ").append(n(raiseTarget)).append("\n");
        b.append("عیار شمش: ").append(n(highBar)).append("\n");
        b.append("اختلاف تا هدف: ").append(n(raise.assayDifference)).append("\n");
        b.append("شمش مورد نیاز: ").append(n(raise.requiredBar)).append(" g\n\n");

        b.append("=== پایین آوردن عیار ===\n");
        b.append("عیار هدف کاهش: ").append(n(lowerTarget)).append("\n");
        b.append("درصد نقره از بار: ").append(n(silverPercent)).append("%\n");
        b.append("کل بار مورد نیاز: ").append(n(lower.totalAlloyRequired)).append(" g\n");
        b.append("نقره مورد نیاز: ").append(n(lower.silverRequired)).append(" g\n");
        b.append("بار بدون نقره: ").append(n(lower.nonSilverRequired)).append(" g\n");
        b.append("۰.۴٪ کل وزن: ").append(n(lower.fourPerThousand)).append(" g\n");
        b.append("بار نهایی دیگر: ").append(n(lower.finalOtherAlloy)).append(" g\n");
        b.append("وزن پس از بار: ").append(n(lower.totalAfterAlloy)).append(" g\n\n");

        b.append("=== محاسبه سریع ===\n");
        b.append("عدد پایه: ").append(n(splitBase)).append("\n");
        b.append("۳۶.۷۹٪: ").append(n(split3679)).append("\n");
        b.append("۶۳.۲۱٪: ").append(n(splitBase - split3679)).append("\n\n");
        b.append("اصلاح وزن برای افت عیار\n");
        b.append("وزن پایه: ").append(n(correctionWeight)).append(" g\n");
        b.append("عیار هدف: ").append(n(correctionTarget)).append("\n");
        b.append("افت عیار: ").append(n(correctionDrop)).append("\n");
        b.append("بار افزوده: ").append(n(correctionAdd)).append(" g\n");
        b.append("جمع وزن: ").append(n(correctionWeight + correctionAdd)).append(" g\n");
        return b.toString();
    }

    private List<GoldEntry> loadSavedEntries() {
        List<GoldEntry> out = new ArrayList<>();
        try {
            SharedPreferences prefs = getSharedPreferences("gold_bar_data", MODE_PRIVATE);
            String raw = prefs.getString("entries_json", "[]");
            JSONArray a = new JSONArray(raw);
            for (int i = 0; i < a.length(); i++) {
                JSONObject o = a.getJSONObject(i);
                out.add(new GoldEntry(1, o.getDouble("weight"), o.getDouble("assay")));
            }
        } catch (Exception ignored) {}
        return out;
    }

    private void wireQuickCalculatorSync() {
        EditText split = findEdit("quick-split-base");
        EditText weight = findEdit("quick-correction-weight");
        EditText target = findEdit("quick-correction-target");
        EditText drop = findEdit("quick-correction-drop");
        if (split == null || weight == null || target == null || drop == null) return;

        TextWatcher watcher = new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                getSharedPreferences(QuickCalcWidget.PREFS, MODE_PRIVATE).edit()
                        .putString("split_base", split.getText().toString())
                        .putString("corr_weight", weight.getText().toString())
                        .putString("corr_target", target.getText().toString())
                        .putString("corr_drop", drop.getText().toString())
                        .apply();
                QuickCalcWidget.updateAll(MainActivityV106.this);
            }
            @Override public void afterTextChanged(Editable s) {}
        };
        split.addTextChangedListener(watcher);
        weight.addTextChangedListener(watcher);
        target.addTextChangedListener(watcher);
        drop.addTextChangedListener(watcher);
    }

    private void applyQuickPrefsToApp() {
        SharedPreferences p = getSharedPreferences(QuickCalcWidget.PREFS, MODE_PRIVATE);
        if (!p.contains("split_base")) return;
        setIfDifferent(findEdit("quick-split-base"), p.getString("split_base", "800"));
        setIfDifferent(findEdit("quick-correction-weight"), p.getString("corr_weight", "250"));
        setIfDifferent(findEdit("quick-correction-target"), p.getString("corr_target", "750"));
        setIfDifferent(findEdit("quick-correction-drop"), p.getString("corr_drop", "1"));
    }

    private void setIfDifferent(EditText e, String value) {
        if (e != null && value != null && !value.equals(e.getText().toString())) e.setText(value);
    }

    private EditText findEdit(String description) {
        View v = findByDescription(findViewById(android.R.id.content), description);
        return v instanceof EditText ? (EditText) v : null;
    }

    private double valueOf(String description, double fallback) {
        EditText e = findEdit(description);
        return e == null ? fallback : parseNumber(e.getText().toString(), fallback);
    }

    private double parseNumber(String raw, double fallback) {
        try {
            String s = normalizeDigits(raw).trim().replace(',', '.').replace('٫', '.');
            return s.isEmpty() ? fallback : Double.parseDouble(s);
        } catch (Exception e) {
            return fallback;
        }
    }

    private String normalizeDigits(String s) {
        if (s == null) return "";
        char[] fa = {'۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'};
        char[] ar = {'٠','١','٢','٣','٤','٥','٦','٧','٨','٩'};
        String out = s;
        for (int i = 0; i < 10; i++) {
            out = out.replace(fa[i], (char)('0' + i));
            out = out.replace(ar[i], (char)('0' + i));
        }
        return out;
    }

    private String n(double v) {
        if (!Double.isFinite(v)) return "—";
        if (Math.abs(v) < 0.0000001) v = 0;
        return reportDf.format(v);
    }

    private TextView findTextExact(View root, String text) {
        if (root instanceof TextView && text.contentEquals(((TextView) root).getText())) return (TextView) root;
        if (root instanceof ViewGroup) {
            ViewGroup g = (ViewGroup) root;
            for (int i = 0; i < g.getChildCount(); i++) {
                TextView found = findTextExact(g.getChildAt(i), text);
                if (found != null) return found;
            }
        }
        return null;
    }

    private View findByDescription(View root, String description) {
        if (description.contentEquals(root.getContentDescription())) return root;
        if (root instanceof ViewGroup) {
            ViewGroup g = (ViewGroup) root;
            for (int i = 0; i < g.getChildCount(); i++) {
                View found = findByDescription(g.getChildAt(i), description);
                if (found != null) return found;
            }
        }
        return null;
    }

    private <T extends View> T findFirst(View root, Class<T> type) {
        if (type.isInstance(root)) return type.cast(root);
        if (root instanceof ViewGroup) {
            ViewGroup g = (ViewGroup) root;
            for (int i = 0; i < g.getChildCount(); i++) {
                T found = findFirst(g.getChildAt(i), type);
                if (found != null) return found;
            }
        }
        return null;
    }

    private int dpLocal(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
