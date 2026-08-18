package com.amirnourhan.goldbar;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.widget.RemoteViews;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.Locale;

public class QuickCalcWidget extends AppWidgetProvider {
    public static final String PREFS = "quick_widget";

    @Override
    public void onUpdate(Context context, AppWidgetManager manager, int[] appWidgetIds) {
        for (int id : appWidgetIds) updateOne(context, manager, id);
    }

    public static void updateAll(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        int[] ids = manager.getAppWidgetIds(
                new ComponentName(context, QuickCalcWidget.class));
        for (int id : ids) updateOne(context, manager, id);
    }

    private static void updateOne(
            Context context, AppWidgetManager manager, int id) {
        SharedPreferences p =
                context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        double splitBase = parse(p.getString("split_base", "800"), 800);
        double corrWeight = parse(p.getString("corr_weight", "250"), 250);
        double corrTarget = parse(p.getString("corr_target", "750"), 750);
        double corrDrop = parse(p.getString("corr_drop", "1"), 1);

        double split = GoldCalculator.split3679(splitBase);
        double add = GoldCalculator.correctionAddition(
                corrWeight, corrTarget, corrDrop);

        RemoteViews rv = new RemoteViews(
                context.getPackageName(), R.layout.widget_quick_calc);
        rv.setTextViewText(
                R.id.widget_split_result,
                "پایه " + n(splitBase)
                        + "  |  " + n(split)
                        + " / " + n(splitBase - split));
        rv.setTextViewText(
                R.id.widget_correction_result,
                "افزوده " + n(add)
                        + " g  |  جمع " + n(corrWeight + add) + " g");

        Intent openApp = new Intent(
                context, MainActivityV106Fixed.class);
        PendingIntent openPi = PendingIntent.getActivity(
                context,
                1060,
                openApp,
                PendingIntent.FLAG_UPDATE_CURRENT
                        | PendingIntent.FLAG_IMMUTABLE);
        rv.setOnClickPendingIntent(R.id.widget_header, openPi);

        Intent splitIntent = new Intent(
                context, QuickCalcActivity.class)
                .putExtra("mode", "split")
                .setAction("com.amirnourhan.goldbar.WIDGET_SPLIT");
        PendingIntent splitPi = PendingIntent.getActivity(
                context,
                1061,
                splitIntent,
                PendingIntent.FLAG_UPDATE_CURRENT
                        | PendingIntent.FLAG_IMMUTABLE);
        rv.setOnClickPendingIntent(R.id.widget_split_card, splitPi);

        Intent correctionIntent = new Intent(
                context, QuickCalcActivity.class)
                .putExtra("mode", "correction")
                .setAction("com.amirnourhan.goldbar.WIDGET_CORRECTION");
        PendingIntent correctionPi = PendingIntent.getActivity(
                context,
                1062,
                correctionIntent,
                PendingIntent.FLAG_UPDATE_CURRENT
                        | PendingIntent.FLAG_IMMUTABLE);
        rv.setOnClickPendingIntent(
                R.id.widget_correction_card, correctionPi);

        manager.updateAppWidget(id, rv);
    }

    private static double parse(String value, double fallback) {
        try {
            String s = value == null || value.trim().isEmpty()
                    ? String.valueOf(fallback)
                    : value.replace(',', '.');
            return Double.parseDouble(s);
        } catch (Exception e) {
            return fallback;
        }
    }

    private static String n(double value) {
        if (!Double.isFinite(value)) return "—";
        DecimalFormat df = new DecimalFormat(
                "0.###", DecimalFormatSymbols.getInstance(Locale.US));
        if (Math.abs(value) < 0.0000001) value = 0;
        return df.format(value);
    }
}
