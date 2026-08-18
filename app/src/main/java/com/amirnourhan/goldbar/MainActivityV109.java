package com.amirnourhan.goldbar;

import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
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

/**
 * Gold Bar v1.0.9 report/UI refinement:
 * - compact one-page text report
 * - raw entries + final results only
 * - no formulas, weighted sums, differences, or calculation-method details
 * - hides 0.4% total-weight and final-other-alloy fields from the app UI
 */
public class MainActivityV109 extends MainActivityV108 {
    private static final int REQUEST_SAVE_SIMPLE_REPORT = 5109;

    private final DecimalFormat df =
            new DecimalFormat("0.###", DecimalFormatSymbols.getInstance(Locale.US));

    private String pendingReport;
    private String pendingReportName;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        removeObsoleteAlloyFields();
        replaceReportAction();
    }

    private void removeObsoleteAlloyFields() {
        removeMetricByLabel("۰.۴٪ کل وزن (g)");
        removeMetricByLabel("بار نهایی دیگر (g)");
    }

    private void removeMetricByLabel(String label) {
        TextView text = findTextExact(findViewById(android.R.id.content), label);
        if (text == null || !(text.getParent() instanceof View)) return;
        View metricBox = (View) text.getParent();
        if (metricBox.getParent() instanceof ViewGroup) {
            ((ViewGroup) metricBox.getParent()).removeView(metricBox);
        }
    }

    private TextView findTextExact(View root, String text) {
        if (root instanceof TextView) {
            CharSequence value = ((TextView) root).getText();
            if (value != null && text.contentEquals(value)) {
                return (TextView) root;
            }
        }
        if (root instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                TextView found = findTextExact(group.getChildAt(i), text);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void replaceReportAction() {
        Button save = findButtonExact(
                findViewById(android.R.id.content), "ذخیره گزارش کامل");
        if (save == null) return;
        save.setOnClickListener(v -> chooseSimpleReportDestination());
    }

    private void chooseSimpleReportDestination() {
        pendingReport = buildSimpleReport();
        pendingReportName = "GoldBar_"
                + new SimpleDateFormat("yyyy-MM-dd_HH-mm-ss", Locale.US)
                .format(new Date())
                + ".txt";

        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("text/plain");
        intent.putExtra(Intent.EXTRA_TITLE, pendingReportName);
        startActivityForResult(intent, REQUEST_SAVE_SIMPLE_REPORT);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_SAVE_SIMPLE_REPORT) {
            if (resultCode != RESULT_OK
                    || data == null
                    || data.getData() == null) {
                return;
            }
            writeReport(data.getData());
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    private void writeReport(Uri destination) {
        try (OutputStream out =
                     getContentResolver().openOutputStream(destination)) {
            if (out == null) throw new IllegalStateException("No output stream");
            String report = pendingReport == null
                    ? buildSimpleReport()
                    : pendingReport;
            out.write(report.getBytes(StandardCharsets.UTF_8));
            out.flush();
            Toast.makeText(
                    this,
                    "گزارش ذخیره شد",
                    Toast.LENGTH_LONG).show();
        } catch (Exception e) {
            Toast.makeText(
                    this,
                    "ذخیره گزارش انجام نشد",
                    Toast.LENGTH_LONG).show();
        }
    }

    private String buildSimpleReport() {
        List<GoldEntry> entries = loadSavedEntries();
        GoldCalculator.Summary summary =
                GoldCalculator.summarize(entries, null);

        double raiseTarget = valueOf("raise-target-assay", 747);
        double highBarAssay = valueOf("high-bar-assay", 995);
        double lowerTarget = valueOf("lower-target-assay", 746);
        double silverPercent = valueOf("silver-percent", 32);

        GoldCalculator.Adjustment raise =
                GoldCalculator.requiredHighAssayBar(
                        summary, raiseTarget, highBarAssay);
        GoldCalculator.Alloy lower =
                GoldCalculator.requiredAlloy(
                        summary, lowerTarget, silverPercent, summary.weight);

        double splitBase = valueOf("quick-split-base", 800);
        double split3679 = GoldCalculator.split3679(splitBase);

        double correctionWeight = valueOf("quick-correction-weight", 250);
        double correctionTarget = valueOf("quick-correction-target", 750);
        double correctionDrop = valueOf("quick-correction-drop", 1);
        double correctionAdd = GoldCalculator.correctionAddition(
                correctionWeight, correctionTarget, correctionDrop);

        StringBuilder b = new StringBuilder();
        b.append("GOLD BAR (by:Amirnourhan) | ")
                .append("تاریخ و ساعت: ")
                .append(new SimpleDateFormat(
                        "yyyy/MM/dd HH:mm:ss", Locale.US).format(new Date()))
                .append("\n");

        b.append("آبشده‌ها: ");
        if (entries.isEmpty()) {
            b.append("ثبت نشده");
        } else {
            for (int i = 0; i < entries.size(); i++) {
                if (i > 0) b.append(" ؛ ");
                GoldEntry e = entries.get(i);
                b.append(i + 1)
                        .append(") ")
                        .append(n(e.weight)).append("g")
                        .append(" | عیار ").append(n(e.assay));
            }
        }
        b.append("\n");

        b.append("خلاصه: تعداد ").append(summary.count)
                .append(" | وزن کل ").append(n(summary.weight)).append("g")
                .append(" | عیار میانگین ").append(n(summary.averageAssay))
                .append(" | وزن پس از بار ").append(n(lower.totalAfterAlloy)).append("g")
                .append("\n");

        b.append("بالا بردن عیار: هدف ").append(n(raiseTarget))
                .append(" | عیار شمش ").append(n(highBarAssay))
                .append(" | شمش مورد نیاز ").append(n(raise.requiredBar)).append("g")
                .append("\n");

        b.append("پایین آوردن عیار: هدف ").append(n(lowerTarget))
                .append(" | نقره ").append(n(silverPercent)).append("%")
                .append(" | مقدار بار مصرفی ").append(n(lower.totalAlloyRequired)).append("g")
                .append(" | نقره مورد نیاز ").append(n(lower.silverRequired)).append("g")
                .append(" | بار بدون نقره ").append(n(lower.nonSilverRequired)).append("g")
                .append(" | وزن پس از بار ").append(n(lower.totalAfterAlloy)).append("g")
                .append("\n");

        b.append("محاسبه سریع: پایه ").append(n(splitBase))
                .append(" | ۳۶.۷۹٪ ").append(n(split3679))
                .append(" | ۶۳.۲۱٪ ").append(n(splitBase - split3679))
                .append("\n");

        b.append("اصلاح افت عیار: وزن پایه ").append(n(correctionWeight)).append("g")
                .append(" | عیار هدف ").append(n(correctionTarget))
                .append(" | افت عیار ").append(n(correctionDrop))
                .append(" | بار افزوده ").append(n(correctionAdd)).append("g")
                .append(" | جمع وزن ").append(n(correctionWeight + correctionAdd)).append("g")
                .append("\n");

        return b.toString();
    }

    private List<GoldEntry> loadSavedEntries() {
        List<GoldEntry> out = new ArrayList<>();
        try {
            SharedPreferences prefs =
                    getSharedPreferences("gold_bar_data", MODE_PRIVATE);
            JSONArray a = new JSONArray(
                    prefs.getString("entries_json", "[]"));
            for (int i = 0; i < a.length(); i++) {
                JSONObject o = a.getJSONObject(i);
                out.add(new GoldEntry(
                        1,
                        o.getDouble("weight"),
                        o.getDouble("assay")));
            }
        } catch (Exception ignored) {
        }
        return out;
    }

    private double valueOf(String description, double fallback) {
        View v = findByDescription(
                findViewById(android.R.id.content), description);
        if (!(v instanceof EditText)) return fallback;
        try {
            String s = normalizeDigits(((EditText) v).getText().toString())
                    .trim()
                    .replace(',', '.')
                    .replace('٫', '.');
            return s.isEmpty() ? fallback : Double.parseDouble(s);
        } catch (Exception e) {
            return fallback;
        }
    }

    private View findByDescription(View root, String description) {
        if (root == null) return null;
        CharSequence cd = root.getContentDescription();
        if (cd != null && description.contentEquals(cd)) return root;
        if (root instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                View found = findByDescription(group.getChildAt(i), description);
                if (found != null) return found;
            }
        }
        return null;
    }

    private Button findButtonExact(View root, String text) {
        if (root instanceof Button) {
            CharSequence value = ((Button) root).getText();
            if (value != null && text.contentEquals(value)) {
                return (Button) root;
            }
        }
        if (root instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                Button found = findButtonExact(group.getChildAt(i), text);
                if (found != null) return found;
            }
        }
        return null;
    }

    private String normalizeDigits(String s) {
        if (s == null) return "";
        char[] fa = {'۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'};
        char[] ar = {'٠','١','٢','٣','٤','٥','٦','٧','٨','٩'};
        String out = s;
        for (int i = 0; i < 10; i++) {
            out = out.replace(fa[i], (char) ('0' + i));
            out = out.replace(ar[i], (char) ('0' + i));
        }
        return out;
    }

    private String n(double value) {
        if (!Double.isFinite(value)) return "—";
        if (Math.abs(value) < 0.0000001) value = 0;
        return df.format(value);
    }
}
