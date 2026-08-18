package com.amirnourhan.goldbar;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsetsController;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.TreeSet;

public class MainActivity extends Activity {
    private static final int BG = Color.rgb(8, 9, 11);
    private static final int CARD = Color.rgb(18, 20, 25);
    private static final int CARD2 = Color.rgb(26, 29, 35);
    private static final int GOLD = Color.rgb(231, 184, 75);
    private static final int GOLD2 = Color.rgb(247, 211, 112);
    private static final int TEXT = Color.rgb(246, 244, 237);
    private static final int MUTED = Color.rgb(155, 161, 173);
    private static final int STROKE = Color.rgb(52, 55, 63);
    private static final int RED = Color.rgb(255, 105, 105);

    private final List<GoldEntry> entries = new ArrayList<>();
    private final List<Integer> guideValues = new ArrayList<>();
    private final DecimalFormat df = new DecimalFormat("0.###", DecimalFormatSymbols.getInstance(Locale.US));

    private SharedPreferences prefs;
    private Spinner guideSpinner;
    private LinearLayout entriesBox;
    private int selectedGuide = 1;
    private int editingIndex = -1;
    private boolean spinnerUpdating;

    private EditText targetAssay, barAssay, silverPercent;
    private EditText inputGuide, inputWeight, inputAssay;
    private EditText splitBase, correctionWeight, correctionTarget, correctionDrop;
    private Button saveEntryButton;

    private TextView selectedWeight, selectedAverage, selectedCount, globalWeight;
    private TextView assayDiff, requiredBar, barState;
    private TextView totalAlloy, silverNeed, nonSilverNeed, fourPerThousand, finalOther, totalAfterAlloy;
    private TextView split3679, split6321, correctionAdd, correctionTotal;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setStatusBarColor(BG);
        getWindow().setNavigationBarColor(BG);
        if (android.os.Build.VERSION.SDK_INT >= 30) {
            WindowInsetsController c = getWindow().getInsetsController();
            if (c != null) c.setSystemBarsAppearance(0,
                    WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS |
                    WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS);
        }

        prefs = getSharedPreferences("gold_bar_data", MODE_PRIVATE);
        loadEntries();
        selectedGuide = prefs.getInt("selected_guide", firstGuide());

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(BG);
        root.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        root.addView(buildHeader());

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        body.setPadding(dp(14), dp(2), dp(14), dp(28));

        body.addView(buildGuideCard(), cardLp());
        body.addView(buildSummaryCard(), cardLp());
        body.addView(buildBarCard(), cardLp());
        body.addView(buildAlloyCard(), cardLp());
        body.addView(buildEntryCard(), cardLp());
        body.addView(buildListCard(), cardLp());
        body.addView(buildToolsCard(), cardLp());

        scroll.addView(body, new ScrollView.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        setContentView(root);

        bindWatchers();
        refreshAll();
    }

    private View buildHeader() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(18), dp(18), dp(18), dp(12));
        row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);

        TextView badge = tv("Au", 18, Color.rgb(20, 15, 3), true);
        badge.setGravity(Gravity.CENTER);
        badge.setBackground(gradient(new int[]{GOLD2, Color.rgb(184, 130, 23)}, 18));
        LinearLayout.LayoutParams bp = new LinearLayout.LayoutParams(dp(54), dp(54));
        bp.setMarginStart(dp(12));
        row.addView(badge, bp);

        LinearLayout titles = new LinearLayout(this);
        titles.setOrientation(LinearLayout.VERTICAL);
        TextView title = tv("GOLD BAR", 25, TEXT, true);
        title.setLetterSpacing(0.08f);
        TextView sub = tv("محاسبه عیار، شمش و بار ریخته‌گری", 12, MUTED, false);
        sub.setPadding(0, dp(3), 0, 0);
        titles.addView(title);
        titles.addView(sub);
        row.addView(titles, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        return row;
    }

    private View buildGuideCard() {
        LinearLayout c = card("شماره راهنما");
        TextView hint = tv("محاسبات بر اساس ردیف انتخاب‌شده انجام می‌شود.", 12, MUTED, false);
        c.addView(hint);
        guideSpinner = new Spinner(this);
        guideSpinner.setMinimumHeight(dp(52));
        guideSpinner.setPadding(dp(10), 0, dp(10), 0);
        guideSpinner.setBackground(round(CARD2, 14, STROKE, 1));
        guideSpinner.setOnItemSelectedListener(new android.widget.AdapterView.OnItemSelectedListener() {
            @Override public void onItemSelected(android.widget.AdapterView<?> parent, View view, int position, long id) {
                if (spinnerUpdating || position < 0 || position >= guideValues.size()) return;
                selectedGuide = guideValues.get(position);
                prefs.edit().putInt("selected_guide", selectedGuide).apply();
                recalculate();
            }
            @Override public void onNothingSelected(android.widget.AdapterView<?> parent) {}
        });
        LinearLayout.LayoutParams p = matchWrap(); p.topMargin = dp(8);
        c.addView(guideSpinner, p);
        return c;
    }

    private View buildSummaryCard() {
        LinearLayout c = card("خلاصه آبشده‌ها");
        selectedWeight = value(); selectedAverage = value(); selectedCount = value(); globalWeight = value();
        c.addView(metricPair("وزن ردیف", selectedWeight, "عیار میانگین", selectedAverage));
        c.addView(metricPair("تعداد آبشده", selectedCount, "کل وزن همه ردیف‌ها", globalWeight), top8());
        return c;
    }

    private View buildBarCard() {
        LinearLayout c = card("محاسبه شمش ۹۹۵");
        targetAssay = field("747", "عیار ریخته‌گری", false);
        barAssay = field("995", "عیار شمش", false);
        c.addView(fieldPair("عیار ریخته‌گری", targetAssay, "عیار شمش", barAssay));
        assayDiff = value(); requiredBar = value();
        c.addView(metricPair("اختلاف عیار", assayDiff, "شمش مورد نیاز (g)", requiredBar), top8());
        barState = tv("—", 12, MUTED, true);
        barState.setGravity(Gravity.CENTER);
        barState.setPadding(dp(10), dp(10), dp(10), dp(10));
        barState.setBackground(round(CARD2, 14, STROKE, 1));
        c.addView(barState, top8());
        return c;
    }

    private View buildAlloyCard() {
        LinearLayout c = card("بار ریخته‌گری");
        silverPercent = field("45", "درصد نقره", false);
        c.addView(labeled("درصد نقره از کل بار", silverPercent));
        totalAlloy = value(); silverNeed = value(); nonSilverNeed = value();
        fourPerThousand = value(); finalOther = value(); totalAfterAlloy = value();
        c.addView(metricPair("کل بار مورد نیاز (g)", totalAlloy, "نقره مورد نیاز (g)", silverNeed), top8());
        c.addView(metricPair("بار بدون نقره (g)", nonSilverNeed, "۰.۴٪ کل وزن (g)", fourPerThousand), top8());
        c.addView(metricPair("بار نهایی دیگر (g)", finalOther, "وزن پس از بار (g)", totalAfterAlloy), top8());
        return c;
    }

    private View buildEntryCard() {
        LinearLayout c = card("ثبت آبشده");
        inputGuide = field("1", "ردیف", true);
        inputWeight = field("", "وزن", false);
        inputAssay = field("", "عیار", false);
        c.addView(fieldPair("ردیف / شماره راهنما", inputGuide, "وزن آبشده (g)", inputWeight));
        c.addView(labeled("عیار آبشده", inputAssay), top8());
        saveEntryButton = button("افزودن آبشده", true);
        saveEntryButton.setOnClickListener(v -> saveEntry());
        LinearLayout.LayoutParams p = matchWrap(); p.topMargin = dp(12); p.height = dp(52);
        c.addView(saveEntryButton, p);
        return c;
    }

    private View buildListCard() {
        LinearLayout c = card("لیست آبشده‌ها");
        Button clear = button("پاک‌کردن همه", false);
        clear.setTextColor(RED);
        clear.setOnClickListener(v -> new AlertDialog.Builder(this)
                .setTitle("پاک‌کردن اطلاعات")
                .setMessage("همه آبشده‌ها حذف شوند؟")
                .setNegativeButton("خیر", null)
                .setPositiveButton("حذف", (d, w) -> {
                    entries.clear(); editingIndex = -1; saveEntries(); refreshAll(); resetEntryForm();
                }).show());
        c.addView(clear, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(44)));
        entriesBox = new LinearLayout(this);
        entriesBox.setOrientation(LinearLayout.VERTICAL);
        entriesBox.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        c.addView(entriesBox, top8());
        return c;
    }

    private View buildToolsCard() {
        LinearLayout c = card("ابزارهای سریع اکسل");
        splitBase = field("800", "عدد پایه", false);
        c.addView(labeled("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", splitBase));
        split3679 = value(); split6321 = value();
        c.addView(metricPair("۳۶.۷۹٪", split3679, "۶۳.۲۱٪", split6321), top8());

        TextView sep = tv("اصلاح وزن برای افت عیار", 13, TEXT, true);
        LinearLayout.LayoutParams sp = matchWrap(); sp.topMargin = dp(18);
        c.addView(sep, sp);
        correctionWeight = field("250", "وزن پایه", false);
        correctionTarget = field("750", "عیار هدف", false);
        correctionDrop = field("1", "افت عیار", false);
        c.addView(fieldPair("وزن پایه", correctionWeight, "عیار هدف", correctionTarget), top8());
        c.addView(labeled("مقدار افت عیار", correctionDrop), top8());
        correctionAdd = value(); correctionTotal = value();
        c.addView(metricPair("بار افزوده (g)", correctionAdd, "جمع وزن (g)", correctionTotal), top8());
        return c;
    }

    private void bindWatchers() {
        TextWatcher w = new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int st, int c, int a) {}
            public void onTextChanged(CharSequence s, int st, int b, int c) { recalculate(); }
            public void afterTextChanged(Editable e) {}
        };
        targetAssay.addTextChangedListener(w); barAssay.addTextChangedListener(w); silverPercent.addTextChangedListener(w);
        splitBase.addTextChangedListener(w); correctionWeight.addTextChangedListener(w);
        correctionTarget.addTextChangedListener(w); correctionDrop.addTextChangedListener(w);
    }

    private void saveEntry() {
        int guide = (int) parse(inputGuide, -1);
        double weight = parse(inputWeight, -1);
        double assay = parse(inputAssay, -1);
        if (guide <= 0 || weight <= 0 || assay <= 0) {
            toast("ردیف، وزن و عیار را صحیح وارد کن.");
            return;
        }
        GoldEntry e = new GoldEntry(guide, weight, assay);
        if (editingIndex >= 0 && editingIndex < entries.size()) entries.set(editingIndex, e);
        else entries.add(e);
        selectedGuide = guide;
        prefs.edit().putInt("selected_guide", selectedGuide).apply();
        saveEntries();
        resetEntryForm();
        refreshAll();
    }

    private void resetEntryForm() {
        editingIndex = -1;
        saveEntryButton.setText("افزودن آبشده");
        inputWeight.setText(""); inputAssay.setText("");
        inputGuide.setText(String.valueOf(selectedGuide));
    }

    private void editEntry(int index) {
        if (index < 0 || index >= entries.size()) return;
        GoldEntry e = entries.get(index);
        editingIndex = index;
        inputGuide.setText(String.valueOf(e.guide));
        inputWeight.setText(num(e.weight));
        inputAssay.setText(num(e.assay));
        saveEntryButton.setText("ذخیره تغییرات");
    }

    private void deleteEntry(int index) {
        if (index < 0 || index >= entries.size()) return;
        entries.remove(index);
        if (editingIndex == index) resetEntryForm();
        else if (editingIndex > index) editingIndex--;
        saveEntries();
        refreshAll();
    }

    private void refreshEntriesList() {
        entriesBox.removeAllViews();
        if (entries.isEmpty()) {
            TextView empty = tv("هنوز آبشده‌ای ثبت نشده است.", 12, MUTED, false);
            empty.setGravity(Gravity.CENTER);
            empty.setPadding(0, dp(14), 0, dp(14));
            entriesBox.addView(empty);
            return;
        }
        for (int i = 0; i < entries.size(); i++) {
            final int index = i;
            GoldEntry e = entries.get(i);
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(dp(10), dp(9), dp(10), dp(9));
            row.setBackground(round(CARD2, 14, STROKE, 1));
            row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);

            TextView info = tv("ردیف " + e.guide + "   •   " + num(e.weight) + " g   •   عیار " + num(e.assay), 13, TEXT, true);
            row.addView(info, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            Button edit = smallButton("ویرایش", GOLD2);
            Button del = smallButton("حذف", RED);
            edit.setOnClickListener(v -> editEntry(index));
            del.setOnClickListener(v -> deleteEntry(index));
            LinearLayout.LayoutParams ep = new LinearLayout.LayoutParams(dp(66), dp(38)); ep.setMarginStart(dp(6));
            row.addView(edit, ep);
            LinearLayout.LayoutParams dpv = new LinearLayout.LayoutParams(dp(52), dp(38)); dpv.setMarginStart(dp(6));
            row.addView(del, dpv);
            LinearLayout.LayoutParams rp = matchWrap(); if (i > 0) rp.topMargin = dp(7);
            entriesBox.addView(row, rp);
        }
    }

    private void refreshAll() {
        updateGuideSpinner();
        refreshEntriesList();
        recalculate();
    }

    private void updateGuideSpinner() {
        TreeSet<Integer> set = new TreeSet<>();
        for (GoldEntry e : entries) if (e.guide > 0) set.add(e.guide);
        if (set.isEmpty()) set.add(1);
        guideValues.clear(); guideValues.addAll(set);
        if (!guideValues.contains(selectedGuide)) selectedGuide = guideValues.get(0);
        List<String> labels = new ArrayList<>();
        for (Integer g : guideValues) labels.add("ردیف " + g);
        spinnerUpdating = true;
        ArrayAdapter<String> adapter = new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, labels) {
            @Override public View getView(int position, View convertView, ViewGroup parent) {
                TextView v = (TextView) super.getView(position, convertView, parent);
                v.setTextColor(TEXT); v.setTextSize(15); v.setGravity(Gravity.RIGHT | Gravity.CENTER_VERTICAL);
                return v;
            }
            @Override public View getDropDownView(int position, View convertView, ViewGroup parent) {
                TextView v = (TextView) super.getDropDownView(position, convertView, parent);
                v.setTextColor(TEXT); v.setBackgroundColor(CARD2); v.setPadding(dp(14), dp(12), dp(14), dp(12));
                return v;
            }
        };
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        guideSpinner.setAdapter(adapter);
        guideSpinner.setSelection(Math.max(0, guideValues.indexOf(selectedGuide)), false);
        spinnerUpdating = false;
        prefs.edit().putInt("selected_guide", selectedGuide).apply();
        if (inputGuide != null && editingIndex < 0) inputGuide.setText(String.valueOf(selectedGuide));
    }

    private void recalculate() {
        if (selectedWeight == null) return;
        GoldCalculator.Summary s = GoldCalculator.summarize(entries, selectedGuide);
        GoldCalculator.Summary all = GoldCalculator.summarize(entries, null);
        selectedWeight.setText(num(s.weight));
        selectedAverage.setText(num(s.averageAssay));
        selectedCount.setText(String.valueOf(s.count));
        globalWeight.setText(num(all.weight));

        double casting = parse(targetAssay, 747);
        double high = parse(barAssay, 995);
        double silver = parse(silverPercent, 45);
        GoldCalculator.Adjustment a = GoldCalculator.requiredHighAssayBar(s, casting, high);
        assayDiff.setText(num(a.assayDifference));
        requiredBar.setText(num(a.requiredBar));
        if (!Double.isFinite(a.requiredBar)) {
            barState.setText("اطلاعات کافی برای محاسبه وجود ندارد"); barState.setTextColor(MUTED);
        } else if (a.requiredBar > 0) {
            barState.setText("نیاز به افزودن شمش با عیار بالا"); barState.setTextColor(GOLD2);
        } else if (a.requiredBar < 0) {
            barState.setText("عیار آبشده از عیار هدف بالاتر است"); barState.setTextColor(GOLD2);
        } else {
            barState.setText("عیار روی هدف است"); barState.setTextColor(GOLD2);
        }

        GoldCalculator.Alloy x = GoldCalculator.requiredAlloy(s, casting, silver, all.weight);
        totalAlloy.setText(num(x.totalAlloyRequired));
        silverNeed.setText(num(x.silverRequired));
        nonSilverNeed.setText(num(x.nonSilverRequired));
        fourPerThousand.setText(num(x.fourPerThousand));
        finalOther.setText(num(x.finalOtherAlloy));
        totalAfterAlloy.setText(num(x.totalAfterAlloy));

        double base = parse(splitBase, 800);
        double part = GoldCalculator.split3679(base);
        split3679.setText(num(part)); split6321.setText(num(base - part));
        double cw = parse(correctionWeight, 250);
        double ct = parse(correctionTarget, 750);
        double cd = parse(correctionDrop, 1);
        double add = GoldCalculator.correctionAddition(cw, ct, cd);
        correctionAdd.setText(num(add)); correctionTotal.setText(num(cw + add));
    }

    private void loadEntries() {
        entries.clear();
        String raw = prefs.getString("entries_json", null);
        if (raw != null && !raw.isEmpty()) {
            try {
                JSONArray a = new JSONArray(raw);
                for (int i = 0; i < a.length(); i++) {
                    JSONObject o = a.getJSONObject(i);
                    entries.add(new GoldEntry(o.getInt("guide"), o.getDouble("weight"), o.getDouble("assay")));
                }
            } catch (Exception ignored) { entries.clear(); }
        }
        if (raw == null && entries.isEmpty()) {
            entries.add(new GoldEntry(1, 84.38, 749));
            entries.add(new GoldEntry(1, 86.69, 750));
            entries.add(new GoldEntry(1, 14.00, 749));
            entries.add(new GoldEntry(1, 23.48, 778));
            entries.add(new GoldEntry(1, 36.26, 977));
            entries.add(new GoldEntry(1, 66.07, 749));
            entries.add(new GoldEntry(1, 42.23, 757));
            saveEntries();
        }
    }

    private void saveEntries() {
        JSONArray a = new JSONArray();
        try {
            for (GoldEntry e : entries) {
                JSONObject o = new JSONObject();
                o.put("guide", e.guide); o.put("weight", e.weight); o.put("assay", e.assay); a.put(o);
            }
            prefs.edit().putString("entries_json", a.toString()).apply();
        } catch (Exception ignored) {}
    }

    private int firstGuide() {
        for (GoldEntry e : entries) if (e.guide > 0) return e.guide;
        return 1;
    }

    private LinearLayout card(String title) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        c.setPadding(dp(14), dp(14), dp(14), dp(14));
        c.setBackground(round(CARD, 22, STROKE, 1));
        c.setElevation(dp(2));
        TextView t = tv(title, 16, TEXT, true); t.setPadding(0, 0, 0, dp(10));
        c.addView(t);
        return c;
    }

    private View metricPair(String l1, TextView v1, String l2, TextView v2) {
        LinearLayout row = new LinearLayout(this); row.setOrientation(LinearLayout.HORIZONTAL); row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        row.addView(metric(l1, v1), weightLp());
        LinearLayout.LayoutParams p = weightLp(); p.setMarginStart(dp(8)); row.addView(metric(l2, v2), p);
        return row;
    }

    private View metric(String label, TextView value) {
        LinearLayout b = new LinearLayout(this); b.setOrientation(LinearLayout.VERTICAL); b.setGravity(Gravity.CENTER);
        b.setPadding(dp(8), dp(10), dp(8), dp(10)); b.setBackground(round(CARD2, 15, STROKE, 1));
        TextView l = tv(label, 11, MUTED, false); l.setGravity(Gravity.CENTER);
        value.setGravity(Gravity.CENTER); b.addView(l); b.addView(value);
        return b;
    }

    private TextView value() { TextView v = tv("—", 20, GOLD2, true); v.setSingleLine(true); return v; }

    private View fieldPair(String l1, EditText f1, String l2, EditText f2) {
        LinearLayout row = new LinearLayout(this); row.setOrientation(LinearLayout.HORIZONTAL); row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        row.addView(labeled(l1, f1), weightLp());
        LinearLayout.LayoutParams p = weightLp(); p.setMarginStart(dp(8)); row.addView(labeled(l2, f2), p);
        return row;
    }

    private LinearLayout labeled(String label, EditText f) {
        LinearLayout b = new LinearLayout(this); b.setOrientation(LinearLayout.VERTICAL);
        TextView l = tv(label, 11, MUTED, false); l.setPadding(dp(4), 0, dp(4), dp(5)); b.addView(l);
        b.addView(f, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(52))); return b;
    }

    private EditText field(String initial, String hint, boolean integer) {
        EditText e = new EditText(this); e.setTextColor(TEXT); e.setHintTextColor(Color.rgb(105, 110, 120)); e.setTextSize(15);
        e.setSingleLine(true); e.setGravity(Gravity.RIGHT | Gravity.CENTER_VERTICAL); e.setHint(hint); e.setPadding(dp(12), 0, dp(12), 0);
        e.setBackground(round(CARD2, 14, STROKE, 1)); e.setSelectAllOnFocus(true);
        e.setInputType(integer ? InputType.TYPE_CLASS_NUMBER : InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_DECIMAL | InputType.TYPE_NUMBER_FLAG_SIGNED);
        if (initial != null && !initial.isEmpty()) e.setText(initial);
        return e;
    }

    private Button button(String label, boolean filled) {
        Button b = new Button(this); b.setText(label); b.setTextSize(13); b.setAllCaps(false); b.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        b.setGravity(Gravity.CENTER); b.setMinHeight(0); b.setMinimumHeight(0); b.setMinWidth(0); b.setMinimumWidth(0);
        b.setTextColor(filled ? Color.rgb(22, 16, 3) : GOLD2);
        b.setBackground(filled ? gradient(new int[]{GOLD2, Color.rgb(184, 130, 23)}, 14) : round(CARD2, 14, STROKE, 1));
        return b;
    }

    private Button smallButton(String label, int color) {
        Button b = button(label, false); b.setTextSize(10); b.setTextColor(color); b.setPadding(dp(3), 0, dp(3), 0); return b;
    }

    private TextView tv(String s, float size, int color, boolean bold) {
        TextView t = new TextView(this); t.setText(s); t.setTextSize(size); t.setTextColor(color);
        t.setTypeface(Typeface.create("sans", bold ? Typeface.BOLD : Typeface.NORMAL));
        t.setGravity(Gravity.RIGHT); t.setLayoutDirection(View.LAYOUT_DIRECTION_RTL); return t;
    }

    private GradientDrawable round(int fill, int radius, int stroke, int strokeWidth) {
        GradientDrawable g = new GradientDrawable(); g.setColor(fill); g.setCornerRadius(dp(radius));
        if (strokeWidth > 0) g.setStroke(dp(strokeWidth), stroke); return g;
    }

    private GradientDrawable gradient(int[] colors, int radius) {
        GradientDrawable g = new GradientDrawable(GradientDrawable.Orientation.LEFT_RIGHT, colors); g.setCornerRadius(dp(radius)); return g;
    }

    private LinearLayout.LayoutParams cardLp() { LinearLayout.LayoutParams p = matchWrap(); p.topMargin = dp(10); return p; }
    private LinearLayout.LayoutParams matchWrap() { return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT); }
    private LinearLayout.LayoutParams weightLp() { return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f); }
    private LinearLayout.LayoutParams top8() { LinearLayout.LayoutParams p = matchWrap(); p.topMargin = dp(8); return p; }
    private int dp(float v) { return Math.round(v * getResources().getDisplayMetrics().density); }

    private double parse(EditText e, double fallback) { return parseString(e == null ? "" : e.getText().toString(), fallback); }
    private double parseString(String raw, double fallback) {
        try {
            String s = normalizeDigits(raw).trim().replace(',', '.').replace('٫', '.');
            if (s.isEmpty() || s.equals("-") || s.equals(".")) return fallback;
            return Double.parseDouble(s);
        } catch (Exception ex) { return fallback; }
    }

    private String normalizeDigits(String s) {
        if (s == null) return "";
        char[] fa = {'۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'};
        char[] ar = {'٠','١','٢','٣','٤','٥','٦','٧','٨','٩'};
        String out = s;
        for (int i = 0; i < 10; i++) { out = out.replace(fa[i], (char)('0' + i)); out = out.replace(ar[i], (char)('0' + i)); }
        return out;
    }

    private String num(double v) {
        if (!Double.isFinite(v)) return "—";
        if (Math.abs(v) < 0.0000001) v = 0;
        return df.format(v);
    }

    private void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_SHORT).show(); }
}
