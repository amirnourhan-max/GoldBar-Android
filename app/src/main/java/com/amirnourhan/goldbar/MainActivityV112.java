package com.amirnourhan.goldbar;

import android.content.ClipData;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.view.DragEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Gold Bar v1.0.12
 * Adds persistent drag-and-drop ordering for the main calculation cards.
 */
public class MainActivityV112 extends MainActivityV110 {
    private static final String LAYOUT_PREFS = "gold_bar_layout";
    private static final String KEY_ORDER = "section_order";
    private static final String KEY_HINT = "reorder_hint_shown";

    private static final LinkedHashMap<String, String> SECTIONS = new LinkedHashMap<>();
    static {
        SECTIONS.put("summary", "خلاصه آبشده‌ها");
        SECTIONS.put("entry", "ثبت سریع آبشده");
        SECTIONS.put("raise", "بالا بردن عیار با شمش ۹۹۵");
        SECTIONS.put("lower", "پایین آوردن عیار با بار ریخته‌گری");
        SECTIONS.put("list", "لیست آبشده‌ها");
        SECTIONS.put("quick", "محاسبه سریع");
    }

    private ScrollView mainScroll;
    private LinearLayout body;
    private final Map<View, String> idsByCard = new HashMap<>();
    private final Map<String, View> cardsById = new HashMap<>();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setupReorderableSections();
    }

    private void setupReorderableSections() {
        mainScroll = findFirst(findViewById(android.R.id.content), ScrollView.class);
        if (mainScroll == null || mainScroll.getChildCount() == 0
                || !(mainScroll.getChildAt(0) instanceof LinearLayout)) {
            return;
        }
        body = (LinearLayout) mainScroll.getChildAt(0);

        collectCards();
        restoreOrder();
        installDragHandlers();
        showHintOnce();
    }

    private void collectCards() {
        idsByCard.clear();
        cardsById.clear();

        for (Map.Entry<String, String> e : SECTIONS.entrySet()) {
            TextView title = findTextExact(body, e.getValue());
            if (title == null) continue;
            View card = directChildOfBody(title);
            if (card == null) continue;

            idsByCard.put(card, e.getKey());
            cardsById.put(e.getKey(), card);
            title.setContentDescription("draggable-section-" + e.getKey());
        }
    }

    private View directChildOfBody(View child) {
        View current = child;
        while (current != null && current.getParent() instanceof View) {
            View parent = (View) current.getParent();
            if (parent == body) return current;
            current = parent;
        }
        return null;
    }

    private void restoreOrder() {
        SharedPreferences p = getSharedPreferences(LAYOUT_PREFS, MODE_PRIVATE);
        String saved = p.getString(KEY_ORDER, "");
        if (saved == null || saved.trim().isEmpty()) return;

        List<String> order = new ArrayList<>();
        for (String id : saved.split(",")) {
            if (SECTIONS.containsKey(id) && !order.contains(id)) order.add(id);
        }
        for (String id : SECTIONS.keySet()) {
            if (!order.contains(id)) order.add(id);
        }

        int firstIndex = Integer.MAX_VALUE;
        Map<View, ViewGroup.LayoutParams> params = new HashMap<>();
        for (View card : new ArrayList<>(idsByCard.keySet())) {
            firstIndex = Math.min(firstIndex, body.indexOfChild(card));
            params.put(card, card.getLayoutParams());
        }
        if (firstIndex == Integer.MAX_VALUE) return;

        for (View card : new ArrayList<>(idsByCard.keySet())) {
            body.removeView(card);
        }

        int index = firstIndex;
        for (String id : order) {
            View card = cardsById.get(id);
            if (card != null) {
                body.addView(card, index++, params.get(card));
            }
        }
    }

    private void installDragHandlers() {
        for (Map.Entry<String, String> e : SECTIONS.entrySet()) {
            TextView title = findTextExact(body, e.getValue());
            View card = cardsById.get(e.getKey());
            if (title == null || card == null) continue;

            title.setOnLongClickListener(v -> {
                ClipData data = ClipData.newPlainText("goldbar-section", e.getKey());
                card.setAlpha(0.55f);
                card.startDragAndDrop(
                        data,
                        new View.DragShadowBuilder(card),
                        card,
                        0);
                return true;
            });
        }

        body.setOnDragListener((v, event) -> {
            Object local = event.getLocalState();
            if (!(local instanceof View) || !idsByCard.containsKey((View) local)) {
                return false;
            }

            View source = (View) local;
            switch (event.getAction()) {
                case DragEvent.ACTION_DRAG_STARTED:
                    return true;

                case DragEvent.ACTION_DRAG_LOCATION:
                    autoScroll(event.getY());
                    return true;

                case DragEvent.ACTION_DROP:
                    moveCardToY(source, event.getY());
                    saveOrder();
                    source.setAlpha(1f);
                    return true;

                case DragEvent.ACTION_DRAG_ENDED:
                    source.setAlpha(1f);
                    return true;

                default:
                    return true;
            }
        });
    }

    private void moveCardToY(View source, float dropY) {
        List<View> remaining = new ArrayList<>();
        for (int i = 0; i < body.getChildCount(); i++) {
            View child = body.getChildAt(i);
            if (child != source && idsByCard.containsKey(child)) remaining.add(child);
        }

        int sectionPosition = 0;
        for (View card : remaining) {
            float center = card.getTop() + card.getHeight() / 2f;
            if (dropY > center) sectionPosition++;
        }

        ViewGroup.LayoutParams lp = source.getLayoutParams();
        body.removeView(source);

        int insertIndex;
        if (sectionPosition >= remaining.size()) {
            insertIndex = firstNonSectionIndex();
        } else {
            insertIndex = body.indexOfChild(remaining.get(sectionPosition));
        }
        if (insertIndex < 0) insertIndex = body.getChildCount();
        body.addView(source, insertIndex, lp);
    }

    private int firstNonSectionIndex() {
        int lastSection = -1;
        for (int i = 0; i < body.getChildCount(); i++) {
            if (idsByCard.containsKey(body.getChildAt(i))) lastSection = i;
        }
        return lastSection + 1;
    }

    private void autoScroll(float bodyY) {
        if (mainScroll == null) return;
        int top = mainScroll.getScrollY();
        int bottom = top + mainScroll.getHeight();
        int edge = dpLocal(110);
        if (bodyY < top + edge) {
            mainScroll.smoothScrollBy(0, -dpLocal(130));
        } else if (bodyY > bottom - edge) {
            mainScroll.smoothScrollBy(0, dpLocal(130));
        }
    }

    private void saveOrder() {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < body.getChildCount(); i++) {
            String id = idsByCard.get(body.getChildAt(i));
            if (id == null) continue;
            if (b.length() > 0) b.append(',');
            b.append(id);
        }
        getSharedPreferences(LAYOUT_PREFS, MODE_PRIVATE)
                .edit()
                .putString(KEY_ORDER, b.toString())
                .apply();
    }

    private void showHintOnce() {
        SharedPreferences p = getSharedPreferences(LAYOUT_PREFS, MODE_PRIVATE);
        if (p.getBoolean(KEY_HINT, false)) return;
        Toast.makeText(
                this,
                "برای جابه‌جایی بخش‌ها، عنوان هر کارت را نگه دار و بالا یا پایین بکش.",
                Toast.LENGTH_LONG).show();
        p.edit().putBoolean(KEY_HINT, true).apply();
    }

    private TextView findTextExact(View root, String text) {
        if (root instanceof TextView) {
            CharSequence value = ((TextView) root).getText();
            if (value != null && text.contentEquals(value)) return (TextView) root;
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

    private <T extends View> T findFirst(View root, Class<T> type) {
        if (type.isInstance(root)) return type.cast(root);
        if (root instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                T found = findFirst(group.getChildAt(i), type);
                if (found != null) return found;
            }
        }
        return null;
    }

    private int dpLocal(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
