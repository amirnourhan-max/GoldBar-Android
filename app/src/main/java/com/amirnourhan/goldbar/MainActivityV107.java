package com.amirnourhan.goldbar;

import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * Gold Bar v1.0.7 UI addition:
 * mirrors "وزن پس از بار" into the top summary card and keeps it live.
 */
public class MainActivityV107 extends MainActivityV106Fixed {
    private static final int CARD2 = Color.rgb(26, 29, 35);
    private static final int GOLD = Color.rgb(247, 211, 112);
    private static final int MUTED = Color.rgb(155, 161, 173);
    private static final int STROKE = Color.rgb(52, 55, 63);

    private TextView summaryAfterAlloy;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        addAfterAlloyToSummary();
    }

    private void addAfterAlloyToSummary() {
        View root = findViewById(android.R.id.content);

        // Resolve the original lower-card value before adding the duplicate label to summary.
        TextView lowerLabel = findTextExact(root, "وزن پس از بار (g)");
        TextView lowerValue = metricValueForLabel(lowerLabel);
        TextView countLabel = findTextExact(root, "تعداد آبشده");

        if (countLabel == null || !(countLabel.getParent() instanceof ViewGroup)) {
            return;
        }

        ViewGroup countMetric = (ViewGroup) countLabel.getParent();
        if (!(countMetric.getParent() instanceof LinearLayout)) {
            return;
        }

        LinearLayout summaryCard = (LinearLayout) countMetric.getParent();

        LinearLayout metric = new LinearLayout(this);
        metric.setOrientation(LinearLayout.VERTICAL);
        metric.setGravity(Gravity.CENTER);
        metric.setPadding(dpLocal(8), dpLocal(10), dpLocal(8), dpLocal(10));
        metric.setBackground(round(CARD2, 15, STROKE, 1));

        TextView label = new TextView(this);
        label.setText("وزن پس از بار (g)");
        label.setTextSize(11);
        label.setTextColor(MUTED);
        label.setGravity(Gravity.CENTER);
        label.setLayoutDirection(View.LAYOUT_DIRECTION_RTL);

        summaryAfterAlloy = new TextView(this);
        summaryAfterAlloy.setText("—");
        summaryAfterAlloy.setTextSize(20);
        summaryAfterAlloy.setTextColor(GOLD);
        summaryAfterAlloy.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        summaryAfterAlloy.setGravity(Gravity.CENTER);
        summaryAfterAlloy.setSingleLine(true);
        summaryAfterAlloy.setContentDescription("summary-after-alloy");

        metric.addView(label);
        metric.addView(summaryAfterAlloy);

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dpLocal(8);
        summaryCard.addView(metric, lp);

        if (lowerValue != null) {
            summaryAfterAlloy.setText(lowerValue.getText());
            lowerValue.addTextChangedListener(new TextWatcher() {
                @Override
                public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

                @Override
                public void onTextChanged(CharSequence s, int start, int before, int count) {
                    summaryAfterAlloy.setText(s);
                }

                @Override
                public void afterTextChanged(Editable s) {}
            });
        }
    }

    private TextView metricValueForLabel(TextView label) {
        if (label == null || !(label.getParent() instanceof ViewGroup)) return null;
        ViewGroup parent = (ViewGroup) label.getParent();
        for (int i = 0; i < parent.getChildCount(); i++) {
            View child = parent.getChildAt(i);
            if (child instanceof TextView && child != label) {
                return (TextView) child;
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

    private GradientDrawable round(int fill, int radius, int stroke, int strokeWidth) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(fill);
        g.setCornerRadius(dpLocal(radius));
        if (strokeWidth > 0) g.setStroke(dpLocal(strokeWidth), stroke);
        return g;
    }

    private int dpLocal(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
