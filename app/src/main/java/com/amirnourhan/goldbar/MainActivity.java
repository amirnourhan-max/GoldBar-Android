package com.amirnourhan.goldbar;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Insets;
import android.graphics.Rect;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class MainActivity extends Activity {
    private static final int BG = Color.rgb(8, 9, 11);
    private static final int CARD = Color.rgb(18, 20, 25);
    private static final int CARD2 = Color.rgb(26, 29, 35);
    private static final int GOLD2 = Color.rgb(247, 211, 112);
    private static final int TEXT = Color.rgb(246, 244, 237);
    private static final int MUTED = Color.rgb(155, 161, 173);
    private static final int STROKE = Color.rgb(52, 55, 63);
    private static final int RED = Color.rgb(255, 105, 105);

    private final List<GoldEntry> entries = new ArrayList<>();
    private final DecimalFormat df =
            new DecimalFormat("0.###", DecimalFormatSymbols.getInstance(Locale.US));

    private SharedPreferences prefs;
    private ScrollView mainScroll;
    private LinearLayout entriesBox;
    private int editingIndex = -1;

    private EditText raiseTargetAssay, barAssay;
    private EditText lowerTargetAssay, silverPercent;
    private EditText inputWeight, inputAssay;
    private EditText splitBase, correctionWeight, correctionTarget, correctionDrop;
    private Button saveEntryButton;

    private TextView totalWeight, averageAssay, entryCount;
    private TextView raiseDiff, requiredBar, raiseState;
    private TextView totalAlloy, silverNeed, nonSilverNeed, fourPerThousand,
            finalOther, totalAfterAlloy, lowerState;
    private TextView split3679, split6321, correctionAdd, correctionTotal;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setStatusBarColor(BG);
        getWindow().setNavigationBarColor(BG);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);

        prefs = getSharedPreferences("gold_bar_data", MODE_PRIVATE);
        loadEntries();

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(BG);
        root.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        root.addView(buildHeader());

        mainScroll = new ScrollView(this);
        mainScroll.setFillViewport(true);
        mainScroll.setClipToPadding(false);

        LinearLayout body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        body.setPadding(dp(14), dp(2), dp(14), dp(42));

        body.addView(buildSummaryCard(), cardLp());
        body.addView(buildEntryCard(), cardLp());
        body.addView(buildRaiseCard(), cardLp());
        body.addView(buildLowerCard(), cardLp());
        body.addView(buildListCard(), cardLp());
        body.addView(buildToolsCard(), cardLp());

        mainScroll.addView(body,
                new ScrollView.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(mainScroll,
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        setContentView(root);

        installImeInsets(root);
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
        badge.setBackground(
                gradient(new int[]{GOLD2, Color.rgb(184, 130, 23)}, 18));
        LinearLayout.LayoutParams bp =
                new LinearLayout.LayoutParams(dp(54), dp(54));
        bp.setMarginStart(dp(12));
        row.addView(badge, bp);

        LinearLayout titles = new LinearLayout(this);
        titles.setOrientation(LinearLayout.VERTICAL);
        TextView title = tv("GOLD BAR", 25, TEXT, true);
        title.setContentDescription("gold-bar-title");
        title.setLetterSpacing(0.08f);
        TextView sub = tv("محاسبه عیار، شمش و بار ریخته‌گری", 12, MUTED, false);
        sub.setPadding(0, dp(3), 0, 0);
        titles.addView(title);
        titles.addView(sub);
        row.addView(titles,
                new LinearLayout.LayoutParams(
                        0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        return row;
    }

    private View buildSummaryCard() {
        LinearLayout c = card("خلاصه آبشده‌ها");
        totalWeight = value();
        averageAssay = value();
        entryCount = value();
        c.addView(metricPair("کل وزن آبشده (g)", totalWeight,
                "عیار میانگین", averageAssay));
        c.addView(metric("تعداد آبشده", entryCount), top8());
        return c;
    }

    private View buildEntryCard() {
        LinearLayout c = card("ثبت سریع آبشده");
        TextView hint = tv(
                "وزن → بعدی → عیار → ثبت. بعد از ثبت، فیلد وزن خودکار برای آبشده بعدی آماده می‌شود.",
                12, MUTED, false);
        hint.setPadding(0, 0, 0, dp(10));
        c.addView(hint);

        inputWeight = field("", "وزن", false);
        inputAssay = field("", "عیار", false);
        inputWeight.setContentDescription("entry-weight");
        inputAssay.setContentDescription("entry-assay");
        inputWeight.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        inputAssay.setImeOptions(EditorInfo.IME_ACTION_DONE);
        c.addView(fieldPair("وزن آبشده (g)", inputWeight,
                "عیار آبشده", inputAssay));

        inputWeight.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_NEXT || isEnter(event)) {
                inputAssay.requestFocus();
                ensureFieldVisible(inputAssay);
                return true;
            }
            return false;
        });
        inputAssay.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_DONE || isEnter(event)) {
                saveEntry();
                return true;
            }
            return false;
        });

        saveEntryButton = button("ثبت آبشده + بعدی", true);
        saveEntryButton.setOnClickListener(v -> saveEntry());
        LinearLayout.LayoutParams p = matchWrap();
        p.topMargin = dp(12);
        p.height = dp(54);
        c.addView(saveEntryButton, p);
        return c;
    }

    private View buildRaiseCard() {
        LinearLayout c = card("بالا بردن عیار با شمش ۹۹۵");
        TextView hint = tv(
                "فقط وقتی عیار میانگین از عیار هدف کمتر باشد محاسبه می‌شود.",
                11, MUTED, false);
        hint.setPadding(0, 0, 0, dp(8));
        c.addView(hint);

        raiseTargetAssay = field("747", "عیار هدف", false);
        barAssay = field("995", "عیار شمش", false);
        raiseTargetAssay.setContentDescription("raise-target-assay");
        barAssay.setContentDescription("high-bar-assay");
        raiseTargetAssay.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        barAssay.setImeOptions(EditorInfo.IME_ACTION_DONE);
        linkNext(raiseTargetAssay, barAssay);
        doneHidesKeyboard(barAssay);
        c.addView(fieldPair("عیار هدف افزایش", raiseTargetAssay,
                "عیار شمش", barAssay));

        raiseDiff = value();
        requiredBar = value();
        c.addView(metricPair("اختلاف تا هدف", raiseDiff,
                "شمش مورد نیاز (g)", requiredBar), top8());

        raiseState = statusView();
        c.addView(raiseState, top8());
        return c;
    }

    private View buildLowerCard() {
        LinearLayout c = card("پایین آوردن عیار با بار ریخته‌گری");
        TextView hint = tv(
                "این بخش فرمول جداگانه دارد و فقط وقتی عیار میانگین از هدف کاهش بالاتر باشد اجرا می‌شود.",
                11, MUTED, false);
        hint.setPadding(0, 0, 0, dp(8));
        c.addView(hint);

        lowerTargetAssay = field("746", "عیار هدف کاهش", false);
        silverPercent = field("32", "درصد نقره", false);
        lowerTargetAssay.setContentDescription("lower-target-assay");
        silverPercent.setContentDescription("silver-percent");
        lowerTargetAssay.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        silverPercent.setImeOptions(EditorInfo.IME_ACTION_DONE);
        linkNext(lowerTargetAssay, silverPercent);
        doneHidesKeyboard(silverPercent);
        c.addView(fieldPair("عیار هدف کاهش", lowerTargetAssay,
                "درصد نقره از بار", silverPercent));

        totalAlloy = value();
        silverNeed = value();
        nonSilverNeed = value();
        fourPerThousand = value();
        finalOther = value();
        totalAfterAlloy = value();

        c.addView(metricPair("کل بار مورد نیاز (g)", totalAlloy,
                "نقره مورد نیاز (g)", silverNeed), top8());
        c.addView(metricPair("بار بدون نقره (g)", nonSilverNeed,
                "۰.۴٪ کل وزن (g)", fourPerThousand), top8());
        c.addView(metricPair("بار نهایی دیگر (g)", finalOther,
                "وزن پس از بار (g)", totalAfterAlloy), top8());

        lowerState = statusView();
        c.addView(lowerState, top8());
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
                    entries.clear();
                    editingIndex = -1;
                    saveEntries();
                    refreshAll();
                    resetEntryForm(false);
                }).show());
        c.addView(clear,
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, dp(44)));

        entriesBox = new LinearLayout(this);
        entriesBox.setOrientation(LinearLayout.VERTICAL);
        entriesBox.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        c.addView(entriesBox, top8());
        return c;
    }

    private View buildToolsCard() {
        LinearLayout c = card("ابزارهای سریع اکسل");
        TextView keyboardHint = tv(
                "فیلد فعال هنگام باز شدن کیبورد به بالای کیبورد منتقل می‌شود.",
                11, MUTED, false);
        keyboardHint.setPadding(0, 0, 0, dp(8));
        c.addView(keyboardHint);

        splitBase = field("800", "عدد پایه", false);
        splitBase.setContentDescription("quick-split-base");
        splitBase.setImeOptions(EditorInfo.IME_ACTION_DONE);
        doneHidesKeyboard(splitBase);
        c.addView(labeled("تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪", splitBase));
        split3679 = value();
        split6321 = value();
        c.addView(metricPair("۳۶.۷۹٪", split3679,
                "۶۳.۲۱٪", split6321), top8());

        TextView sep = tv("اصلاح وزن برای افت عیار", 13, TEXT, true);
        LinearLayout.LayoutParams sp = matchWrap();
        sp.topMargin = dp(18);
        c.addView(sep, sp);

        correctionWeight = field("250", "وزن پایه", false);
        correctionTarget = field("750", "عیار هدف", false);
        correctionDrop = field("1", "افت عیار", false);
        correctionWeight.setContentDescription("quick-correction-weight");
        correctionTarget.setContentDescription("quick-correction-target");
        correctionDrop.setContentDescription("quick-correction-drop");
        correctionWeight.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        correctionTarget.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        correctionDrop.setImeOptions(EditorInfo.IME_ACTION_DONE);
        linkNext(correctionWeight, correctionTarget);
        linkNext(correctionTarget, correctionDrop);
        doneHidesKeyboard(correctionDrop);

        c.addView(fieldPair("وزن پایه", correctionWeight,
                "عیار هدف", correctionTarget), top8());
        c.addView(labeled("مقدار افت عیار", correctionDrop), top8());

        correctionAdd = value();
        correctionTotal = value();
        c.addView(metricPair("بار افزوده (g)", correctionAdd,
                "جمع وزن (g)", correctionTotal), top8());
        return c;
    }

    private void bindWatchers() {
        TextWatcher w = new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int st, int c, int a) {}
            public void onTextChanged(CharSequence s, int st, int b, int c) {
                recalculate();
            }
            public void afterTextChanged(Editable e) {}
        };

        raiseTargetAssay.addTextChangedListener(w);
        barAssay.addTextChangedListener(w);
        lowerTargetAssay.addTextChangedListener(w);
        silverPercent.addTextChangedListener(w);
        splitBase.addTextChangedListener(w);
        correctionWeight.addTextChangedListener(w);
        correctionTarget.addTextChangedListener(w);
        correctionDrop.addTextChangedListener(w);
    }

    private void saveEntry() {
        double weight = parse(inputWeight, -1);
        double assay = parse(inputAssay, -1);

        if (weight <= 0 || assay <= 0 || assay > 1000) {
            toast("وزن و عیار را صحیح وارد کن. عیار باید بین ۱ تا ۱۰۰۰ باشد.");
            View bad = weight <= 0 ? inputWeight : inputAssay;
            bad.requestFocus();
            ensureFieldVisible(bad);
            return;
        }

        boolean wasEditing =
                editingIndex >= 0 && editingIndex < entries.size();
        GoldEntry e = new GoldEntry(1, weight, assay);

        if (wasEditing) {
            entries.set(editingIndex, e);
        } else {
            entries.add(e);
        }

        saveEntries();
        refreshAll();
        resetEntryForm(true);
        toast(wasEditing ? "تغییرات ذخیره شد" : "آبشده ثبت شد");
    }

    private void resetEntryForm(boolean keepKeyboard) {
        editingIndex = -1;
        saveEntryButton.setText("ثبت آبشده + بعدی");
        inputWeight.setText("");
        inputAssay.setText("");

        if (keepKeyboard) {
            inputWeight.post(() -> {
                inputWeight.requestFocus();
                ensureFieldVisible(inputWeight);
                InputMethodManager imm =
                        (InputMethodManager)
                                getSystemService(Context.INPUT_METHOD_SERVICE);
                if (imm != null) {
                    imm.showSoftInput(
                            inputWeight, InputMethodManager.SHOW_IMPLICIT);
                }
            });
        }
    }

    private void editEntry(int index) {
        if (index < 0 || index >= entries.size()) return;
        GoldEntry e = entries.get(index);
        editingIndex = index;
        inputWeight.setText(num(e.weight));
        inputAssay.setText(num(e.assay));
        saveEntryButton.setText("ذخیره تغییرات");
        inputWeight.requestFocus();
        ensureFieldVisible(inputWeight);
    }

    private void deleteEntry(int index) {
        if (index < 0 || index >= entries.size()) return;
        entries.remove(index);

        if (editingIndex == index) {
            resetEntryForm(false);
        } else if (editingIndex > index) {
            editingIndex--;
        }

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

            TextView info = tv(
                    "وزن " + num(e.weight) + " g   •   عیار " + num(e.assay),
                    13, TEXT, true);
            row.addView(info,
                    new LinearLayout.LayoutParams(
                            0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

            Button edit = smallButton("ویرایش", GOLD2);
            Button del = smallButton("حذف", RED);
            edit.setOnClickListener(v -> editEntry(index));
            del.setOnClickListener(v -> deleteEntry(index));

            LinearLayout.LayoutParams ep =
                    new LinearLayout.LayoutParams(dp(66), dp(38));
            ep.setMarginStart(dp(6));
            row.addView(edit, ep);

            LinearLayout.LayoutParams dpv =
                    new LinearLayout.LayoutParams(dp(52), dp(38));
            dpv.setMarginStart(dp(6));
            row.addView(del, dpv);

            LinearLayout.LayoutParams rp = matchWrap();
            if (i > 0) rp.topMargin = dp(7);
            entriesBox.addView(row, rp);
        }
    }

    private void refreshAll() {
        refreshEntriesList();
        recalculate();
    }

    private void recalculate() {
        if (totalWeight == null) return;

        GoldCalculator.Summary s =
                GoldCalculator.summarize(entries, null);
        totalWeight.setText(num(s.weight));
        averageAssay.setText(num(s.averageAssay));
        entryCount.setText(String.valueOf(s.count));

        double raiseTarget = parse(raiseTargetAssay, 747);
        double high = parse(barAssay, 995);
        double lowerTarget = parse(lowerTargetAssay, 746);
        double silver = parse(silverPercent, 32);

        GoldCalculator.Adjustment raise =
                GoldCalculator.requiredHighAssayBar(
                        s, raiseTarget, high);

        raiseDiff.setText(num(raise.assayDifference));
        requiredBar.setText(num(raise.requiredBar));

        if (!Double.isFinite(raise.requiredBar)) {
            raiseState.setText("اطلاعات یا عیارهای ورودی برای محاسبه افزایش کافی نیست.");
            raiseState.setTextColor(MUTED);
        } else if (raise.requiredBar > 0) {
            raiseState.setText(
                    "برای رسیدن به عیار " + num(raiseTarget)
                            + " باید " + num(raise.requiredBar)
                            + " g شمش عیار " + num(high) + " اضافه شود.");
            raiseState.setTextColor(GOLD2);
        } else {
            raiseState.setText(
                    "بالا بردن عیار لازم نیست؛ شمش عیار بالا = ۰ g");
            raiseState.setTextColor(GOLD2);
        }

        GoldCalculator.Alloy lower =
                GoldCalculator.requiredAlloy(
                        s, lowerTarget, silver, s.weight);

        totalAlloy.setText(num(lower.totalAlloyRequired));
        silverNeed.setText(num(lower.silverRequired));
        nonSilverNeed.setText(num(lower.nonSilverRequired));
        fourPerThousand.setText(num(lower.fourPerThousand));
        finalOther.setText(num(lower.finalOtherAlloy));
        totalAfterAlloy.setText(num(lower.totalAfterAlloy));

        if (!Double.isFinite(lower.totalAlloyRequired)) {
            lowerState.setText("اطلاعات یا عیارهای ورودی برای محاسبه کاهش کافی نیست.");
            lowerState.setTextColor(MUTED);
        } else if (lower.totalAlloyRequired > 0) {
            lowerState.setText(
                    "برای کاهش عیار تا " + num(lowerTarget)
                            + " باید " + num(lower.totalAlloyRequired)
                            + " g بار ریخته‌گری اضافه شود.");
            lowerState.setTextColor(GOLD2);
        } else {
            lowerState.setText(
                    "پایین آوردن عیار لازم نیست؛ بار ریخته‌گری = ۰ g");
            lowerState.setTextColor(GOLD2);
        }

        double base = parse(splitBase, 800);
        double part = GoldCalculator.split3679(base);
        split3679.setText(num(part));
        split6321.setText(num(base - part));

        double cw = parse(correctionWeight, 250);
        double ct = parse(correctionTarget, 750);
        double cd = parse(correctionDrop, 1);
        double add = GoldCalculator.correctionAddition(cw, ct, cd);
        correctionAdd.setText(num(add));
        correctionTotal.setText(num(cw + add));
    }

    private void loadEntries() {
        entries.clear();
        String raw = prefs.getString("entries_json", null);
        if (raw == null || raw.isEmpty()) return;

        try {
            JSONArray a = new JSONArray(raw);
            for (int i = 0; i < a.length(); i++) {
                JSONObject o = a.getJSONObject(i);
                entries.add(new GoldEntry(
                        1, o.getDouble("weight"), o.getDouble("assay")));
            }
        } catch (Exception ignored) {
            entries.clear();
        }
    }

    private void saveEntries() {
        JSONArray a = new JSONArray();
        try {
            for (GoldEntry e : entries) {
                JSONObject o = new JSONObject();
                o.put("guide", 1);
                o.put("weight", e.weight);
                o.put("assay", e.assay);
                a.put(o);
            }
            prefs.edit().putString("entries_json", a.toString()).apply();
        } catch (Exception ignored) {}
    }

    private void linkNext(EditText from, EditText to) {
        from.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_NEXT || isEnter(event)) {
                to.requestFocus();
                ensureFieldVisible(to);
                return true;
            }
            return false;
        });
    }

    private void doneHidesKeyboard(EditText field) {
        field.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_DONE || isEnter(event)) {
                hideKeyboard(field);
                return true;
            }
            return false;
        });
    }

    private boolean isEnter(KeyEvent event) {
        return event != null
                && event.getAction() == KeyEvent.ACTION_DOWN
                && event.getKeyCode() == KeyEvent.KEYCODE_ENTER;
    }

    private void installImeInsets(View root) {
        if (android.os.Build.VERSION.SDK_INT < 30) return;

        root.setOnApplyWindowInsetsListener((v, insets) -> {
            Insets ime = insets.getInsets(WindowInsets.Type.ime());
            Insets nav = insets.getInsets(WindowInsets.Type.navigationBars());
            int imeOnly = Math.max(0, ime.bottom - nav.bottom);

            if (mainScroll != null) {
                mainScroll.setPadding(
                        0, 0, 0, imeOnly + dp(28));
            }

            View focused = getCurrentFocus();
            if (imeOnly > 0 && focused != null) {
                ensureFieldVisible(focused);
            }
            return insets;
        });
        root.requestApplyInsets();
    }

    private void ensureFieldVisible(View field) {
        if (mainScroll == null || field == null) return;

        field.postDelayed(() -> {
            Rect local = new Rect(
                    0, 0,
                    Math.max(field.getWidth(), 1),
                    field.getHeight() + dp(110));
            field.requestRectangleOnScreen(local, true);

            Rect r = new Rect();
            field.getDrawingRect(r);
            mainScroll.offsetDescendantRectToMyCoords(field, r);
            int targetY = Math.max(0, r.top - dp(120));
            mainScroll.smoothScrollTo(0, targetY);
        }, 220);
    }

    private void hideKeyboard(View field) {
        InputMethodManager imm =
                (InputMethodManager)
                        getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null) {
            imm.hideSoftInputFromWindow(field.getWindowToken(), 0);
        }
        field.clearFocus();
    }

    private LinearLayout card(String title) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        c.setPadding(dp(14), dp(14), dp(14), dp(14));
        c.setBackground(round(CARD, 22, STROKE, 1));
        c.setElevation(dp(2));

        TextView t = tv(title, 16, TEXT, true);
        t.setPadding(0, 0, 0, dp(10));
        c.addView(t);
        return c;
    }

    private View metricPair(
            String l1, TextView v1, String l2, TextView v2) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        row.addView(metric(l1, v1), weightLp());

        LinearLayout.LayoutParams p = weightLp();
        p.setMarginStart(dp(8));
        row.addView(metric(l2, v2), p);
        return row;
    }

    private View metric(String label, TextView value) {
        LinearLayout b = new LinearLayout(this);
        b.setOrientation(LinearLayout.VERTICAL);
        b.setGravity(Gravity.CENTER);
        b.setPadding(dp(8), dp(10), dp(8), dp(10));
        b.setBackground(round(CARD2, 15, STROKE, 1));

        TextView l = tv(label, 11, MUTED, false);
        l.setGravity(Gravity.CENTER);
        value.setGravity(Gravity.CENTER);

        b.addView(l);
        b.addView(value);
        return b;
    }

    private TextView statusView() {
        TextView v = tv("—", 12, MUTED, true);
        v.setGravity(Gravity.CENTER);
        v.setPadding(dp(10), dp(10), dp(10), dp(10));
        v.setBackground(round(CARD2, 14, STROKE, 1));
        return v;
    }

    private TextView value() {
        TextView v = tv("—", 20, GOLD2, true);
        v.setSingleLine(true);
        return v;
    }

    private View fieldPair(
            String l1, EditText f1, String l2, EditText f2) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        row.addView(labeled(l1, f1), weightLp());

        LinearLayout.LayoutParams p = weightLp();
        p.setMarginStart(dp(8));
        row.addView(labeled(l2, f2), p);
        return row;
    }

    private LinearLayout labeled(String label, EditText f) {
        LinearLayout b = new LinearLayout(this);
        b.setOrientation(LinearLayout.VERTICAL);

        TextView l = tv(label, 11, MUTED, false);
        l.setPadding(dp(4), 0, dp(4), dp(5));
        b.addView(l);
        b.addView(f,
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, dp(52)));
        return b;
    }

    private EditText field(
            String initial, String hint, boolean integer) {
        EditText e = new EditText(this);
        e.setTextColor(TEXT);
        e.setHintTextColor(Color.rgb(105, 110, 120));
        e.setTextSize(15);
        e.setSingleLine(true);
        e.setGravity(Gravity.RIGHT | Gravity.CENTER_VERTICAL);
        e.setHint(hint);
        e.setPadding(dp(12), 0, dp(12), 0);
        e.setBackground(round(CARD2, 14, STROKE, 1));
        e.setSelectAllOnFocus(true);

        e.setInputType(
                integer
                        ? InputType.TYPE_CLASS_NUMBER
                        : InputType.TYPE_CLASS_NUMBER
                                | InputType.TYPE_NUMBER_FLAG_DECIMAL
                                | InputType.TYPE_NUMBER_FLAG_SIGNED);

        e.setOnFocusChangeListener((v, hasFocus) -> {
            if (hasFocus) ensureFieldVisible(v);
        });
        e.setOnClickListener(this::ensureFieldVisible);

        if (initial != null && !initial.isEmpty()) {
            e.setText(initial);
        }
        return e;
    }

    private Button button(String label, boolean filled) {
        Button b = new Button(this);
        b.setText(label);
        b.setTextSize(13);
        b.setAllCaps(false);
        b.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        b.setGravity(Gravity.CENTER);
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        b.setMinWidth(0);
        b.setMinimumWidth(0);
        b.setTextColor(
                filled ? Color.rgb(22, 16, 3) : GOLD2);
        b.setBackground(
                filled
                        ? gradient(
                                new int[]{GOLD2, Color.rgb(184, 130, 23)},
                                14)
                        : round(CARD2, 14, STROKE, 1));
        return b;
    }

    private Button smallButton(String label, int color) {
        Button b = button(label, false);
        b.setTextSize(10);
        b.setTextColor(color);
        b.setPadding(dp(3), 0, dp(3), 0);
        return b;
    }

    private TextView tv(
            String s, float size, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(size);
        t.setTextColor(color);
        t.setTypeface(
                Typeface.create(
                        "sans",
                        bold ? Typeface.BOLD : Typeface.NORMAL));
        t.setGravity(Gravity.RIGHT);
        t.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);
        return t;
    }

    private GradientDrawable round(
            int fill, int radius, int stroke, int strokeWidth) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(fill);
        g.setCornerRadius(dp(radius));
        if (strokeWidth > 0) {
            g.setStroke(dp(strokeWidth), stroke);
        }
        return g;
    }

    private GradientDrawable gradient(
            int[] colors, int radius) {
        GradientDrawable g =
                new GradientDrawable(
                        GradientDrawable.Orientation.LEFT_RIGHT, colors);
        g.setCornerRadius(dp(radius));
        return g;
    }

    private LinearLayout.LayoutParams cardLp() {
        LinearLayout.LayoutParams p = matchWrap();
        p.topMargin = dp(10);
        return p;
    }

    private LinearLayout.LayoutParams matchWrap() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    private LinearLayout.LayoutParams weightLp() {
        return new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
    }

    private LinearLayout.LayoutParams top8() {
        LinearLayout.LayoutParams p = matchWrap();
        p.topMargin = dp(8);
        return p;
    }

    private int dp(float v) {
        return Math.round(
                v * getResources().getDisplayMetrics().density);
    }

    private double parse(EditText e, double fallback) {
        return parseString(
                e == null ? "" : e.getText().toString(), fallback);
    }

    private double parseString(String raw, double fallback) {
        try {
            String s = normalizeDigits(raw)
                    .trim()
                    .replace(',', '.')
                    .replace('٫', '.');
            if (s.isEmpty()
                    || s.equals("-")
                    || s.equals(".")) {
                return fallback;
            }
            return Double.parseDouble(s);
        } catch (Exception ex) {
            return fallback;
        }
    }

    private String normalizeDigits(String s) {
        if (s == null) return "";

        char[] fa =
                {'۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'};
        char[] ar =
                {'٠','١','٢','٣','٤','٥','٦','٧','٨','٩'};

        String out = s;
        for (int i = 0; i < 10; i++) {
            out = out.replace(fa[i], (char) ('0' + i));
            out = out.replace(ar[i], (char) ('0' + i));
        }
        return out;
    }

    private String num(double v) {
        if (!Double.isFinite(v)) return "—";
        if (Math.abs(v) < 0.0000001) v = 0;
        return df.format(v);
    }

    private void toast(String s) {
        Toast.makeText(
                this, s, Toast.LENGTH_SHORT).show();
    }
}
