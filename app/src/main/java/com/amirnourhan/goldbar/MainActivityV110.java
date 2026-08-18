package com.amirnourhan.goldbar;

import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

/**
 * Gold Bar v1.0.10 UI cleanup:
 * - removes 0.4% total-weight metric from the lower-assay card
 * - removes final-other-alloy metric from the lower-assay card
 * The underlying calculator is unchanged so all remaining outputs stay identical.
 */
public class MainActivityV110 extends MainActivityV109 {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        hideMetric("۰.۴٪ کل وزن (g)");
        hideMetric("بار نهایی دیگر (g)");
    }

    private void hideMetric(String labelText) {
        TextView label = findTextExact(
                findViewById(android.R.id.content), labelText);
        if (label == null || !(label.getParent() instanceof View)) return;

        View metricBox = (View) label.getParent();
        metricBox.setVisibility(View.GONE);
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
}
