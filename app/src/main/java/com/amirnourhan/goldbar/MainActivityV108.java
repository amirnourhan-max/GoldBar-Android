package com.amirnourhan.goldbar;

import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * Gold Bar v1.0.8 UI additions:
 * - keeps v1.0.7 summary-after-alloy mirror
 * - adds a compact "clear all melted batches" shortcut inside Quick Entry
 */
public class MainActivityV108 extends MainActivityV107 {
    private static final int CARD2 = Color.rgb(26, 29, 35);
    private static final int RED = Color.rgb(255, 105, 105);

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        addCompactClearAllToQuickEntry();
    }

    private void addCompactClearAllToQuickEntry() {
        View root = findViewById(android.R.id.content);

        // Reuse the already-tested delete confirmation/action from the list card.
        Button originalClear = findButtonExact(root, "پاک‌کردن همه");
        TextView quickEntryTitle = findTextExact(root, "ثبت سریع آبشده");

        if (originalClear == null
                || quickEntryTitle == null
                || !(quickEntryTitle.getParent() instanceof LinearLayout)) {
            return;
        }

        LinearLayout quickEntryCard = (LinearLayout) quickEntryTitle.getParent();

        Button compactClear = new Button(this);
        compactClear.setText("پاک‌کردن همه آبشده‌ها");
        compactClear.setContentDescription("clear-all-quick-button");
        compactClear.setTextSize(11);
        compactClear.setAllCaps(false);
        compactClear.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        compactClear.setTextColor(RED);
        compactClear.setGravity(Gravity.CENTER);
        compactClear.setMinHeight(0);
        compactClear.setMinimumHeight(0);
        compactClear.setMinWidth(0);
        compactClear.setMinimumWidth(0);
        compactClear.setPadding(dpLocal(10), 0, dpLocal(10), 0);

        GradientDrawable bg = new GradientDrawable();
        bg.setColor(CARD2);
        bg.setCornerRadius(dpLocal(12));
        bg.setStroke(dpLocal(1), RED);
        compactClear.setBackground(bg);

        compactClear.setOnClickListener(v -> originalClear.performClick());

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                dpLocal(178), dpLocal(38));
        lp.gravity = Gravity.CENTER_HORIZONTAL;
        lp.topMargin = dpLocal(9);
        quickEntryCard.addView(compactClear, lp);
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

    private int dpLocal(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
