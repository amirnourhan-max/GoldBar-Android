package com.amirnourhan.goldbar;

import android.app.Activity;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.Locale;

public class QuickCalcActivity extends Activity {
    private static final int BG = Color.rgb(8, 9, 11);
    private static final int CARD = Color.rgb(24, 27, 33);
    private static final int GOLD = Color.rgb(247, 211, 112);
    private static final int TEXT = Color.rgb(246, 244, 237);
    private static final int MUTED = Color.rgb(155, 161, 173);
    private final DecimalFormat df = new DecimalFormat("0.###", DecimalFormatSymbols.getInstance(Locale.US));
    private EditText a, b, c;
    private TextView result;
    private String mode;

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        mode = getIntent().getStringExtra("mode");
        if (mode == null) mode = "split";
        setContentView(buildUi());
    }

    private LinearLayout buildUi() {
        SharedPreferences p = getSharedPreferences(QuickCalcWidget.PREFS, MODE_PRIVATE);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(20), dp(20), dp(20), dp(20));
        root.setBackgroundColor(BG);
        root.setLayoutDirection(LinearLayout.LAYOUT_DIRECTION_RTL);

        TextView title = text(mode.equals("split") ? "تقسیم ۳۶.۷۹٪ / ۶۳.۲۱٪" : "اصلاح وزن برای افت عیار", 20, GOLD, true);
        root.addView(title);
        TextView hint = text("اعداد را وارد کن؛ نتیجه همان لحظه محاسبه می‌شود.", 12, MUTED, false);
        hint.setPadding(0, dp(5), 0, dp(14));
        root.addView(hint);

        if (mode.equals("split")) {
            a = input(p.getString("split_base", "800"), "عدد پایه");
            root.addView(labelField("عدد پایه", a));
        } else {
            a = input(p.getString("corr_weight", "250"), "وزن پایه");
            b = input(p.getString("corr_target", "750"), "عیار هدف");
            c = input(p.getString("corr_drop", "1"), "افت عیار");
            a.setImeOptions(EditorInfo.IME_ACTION_NEXT);
            b.setImeOptions(EditorInfo.IME_ACTION_NEXT);
            c.setImeOptions(EditorInfo.IME_ACTION_DONE);
            root.addView(labelField("وزن پایه", a));
            root.addView(labelField("عیار هدف", b));
            root.addView(labelField("افت عیار", c));
        }

        result = text("—", 18, GOLD, true);
        result.setGravity(Gravity.CENTER);
        result.setPadding(dp(12), dp(16), dp(12), dp(16));
        result.setBackground(round(CARD, 16));
        LinearLayout.LayoutParams rp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rp.topMargin = dp(12);
        root.addView(result, rp);

        Button save = new Button(this);
        save.setText("ذخیره روی ویجت");
        save.setAllCaps(false);
        save.setTextColor(Color.rgb(22, 16, 3));
        save.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        save.setBackground(round(GOLD, 16));
        save.setOnClickListener(v -> saveAndClose());
        LinearLayout.LayoutParams sp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(54));
        sp.topMargin = dp(14);
        root.addView(save, sp);

        TextWatcher watcher = new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int st, int count, int after) {}
            public void onTextChanged(CharSequence s, int st, int before, int count) { recalc(); }
            public void afterTextChanged(Editable e) {}
        };
        a.addTextChangedListener(watcher);
        if (b != null) b.addTextChangedListener(watcher);
        if (c != null) c.addTextChangedListener(watcher);
        recalc();
        a.requestFocus();
        return root;
    }

    private void recalc() {
        if (result == null) return;
        if (mode.equals("split")) {
            double base = parse(a, 800);
            double part = GoldCalculator.split3679(base);
            result.setText("۳۶.۷۹٪ = " + n(part) + "\n۶۳.۲۱٪ = " + n(base - part));
        } else {
            double weight = parse(a, 250), target = parse(b, 750), drop = parse(c, 1);
            double add = GoldCalculator.correctionAddition(weight, target, drop);
            result.setText("بار افزوده = " + n(add) + " g\nجمع وزن = " + n(weight + add) + " g");
        }
    }

    private void saveAndClose() {
        SharedPreferences.Editor e = getSharedPreferences(QuickCalcWidget.PREFS, MODE_PRIVATE).edit();
        if (mode.equals("split")) e.putString("split_base", a.getText().toString());
        else e.putString("corr_weight", a.getText().toString()).putString("corr_target", b.getText().toString()).putString("corr_drop", c.getText().toString());
        e.apply();
        QuickCalcWidget.updateAll(this);
        finish();
    }

    private EditText input(String initial, String hint) {
        EditText e = new EditText(this);
        e.setText(initial);
        e.setHint(hint);
        e.setTextColor(TEXT);
        e.setHintTextColor(MUTED);
        e.setTextSize(16);
        e.setSingleLine(true);
        e.setGravity(Gravity.RIGHT | Gravity.CENTER_VERTICAL);
        e.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_DECIMAL | InputType.TYPE_NUMBER_FLAG_SIGNED);
        e.setPadding(dp(12), 0, dp(12), 0);
        e.setBackground(round(CARD, 14));
        e.setSelectAllOnFocus(true);
        return e;
    }

    private LinearLayout labelField(String label, EditText input) {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        TextView l = text(label, 12, MUTED, false);
        l.setPadding(dp(3), dp(8), dp(3), dp(5));
        box.addView(l);
        box.addView(input, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(52)));
        return box;
    }

    private TextView text(String s, float size, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(size);
        t.setTextColor(color);
        t.setTypeface(Typeface.DEFAULT, bold ? Typeface.BOLD : Typeface.NORMAL);
        t.setGravity(Gravity.RIGHT);
        return t;
    }

    private GradientDrawable round(int color, int radius) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(color);
        g.setCornerRadius(dp(radius));
        return g;
    }

    private double parse(EditText e, double fallback) {
        try { return Double.parseDouble(e.getText().toString().trim().replace(',', '.')); }
        catch (Exception ex) { return fallback; }
    }

    private String n(double v) {
        if (!Double.isFinite(v)) return "—";
        if (Math.abs(v) < 0.0000001) v = 0;
        return df.format(v);
    }

    private int dp(float v) { return Math.round(v * getResources().getDisplayMetrics().density); }
}
