from __future__ import annotations

import json
import math
import os
import re
import sys
import threading
import time
from datetime import datetime
from pathlib import Path
from typing import Any

import serial
from PySide6.QtCore import QObject, QTimer, QUrl, Signal, Slot
from PySide6.QtGui import QDesktopServices, QIcon
from PySide6.QtWebChannel import QWebChannel
from PySide6.QtWebEngineWidgets import QWebEngineView
from PySide6.QtWidgets import QApplication, QFileDialog, QMainWindow, QMessageBox, QSplashScreen

VERSION = "1.6.0"


def local_appdata() -> Path:
    return Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))


APP_DIR = local_appdata() / "GoldBar"
SETTINGS_PATH = APP_DIR / "settings.json"
ENTRIES_PATH = APP_DIR / "entries.json"


def default_report_folder() -> str:
    docs = Path.home() / "Documents"
    return str(docs / "GoldBar Reports")


DEFAULT_SETTINGS: dict[str, Any] = {
    "SettingsVersion": 7,
    "ReportFolder": default_report_folder(),
    "ScaleModel": "A&D",
    "PortName": "COM1",
    "BaudRate": 2400,
    "DataBits": 7,
    "Parity": "Even",
    "StopBits": "Two",
    "Handshake": "None",
    "DecimalSeparator": ".",
    "CharactersBeforeDecimal": 4,
    "CharactersAfterDecimal": 8,
    "MinimumAfterDecimal": 2,
    "ReceivePrintKey": True,
    "AutoRead": False,
    "ReadOnUpArrow": True,
    "ShowRawText": False,
    "StableAutoReadOnly": True,
    "StableSampleCount": 3,
    "StableToleranceGrams": 0.02,
    "SendQueryOnUpArrow": True,
    "QueryCommand": "Q",
    "QueryLineEnding": "CRLF",
    "ReadTimeoutMs": 1800,
}


def safe_float(value: Any, fallback: float = math.nan) -> float:
    try:
        v = float(value)
        return v if math.isfinite(v) else fallback
    except Exception:
        return fallback


def rounddown_toward_zero(value: float, digits: int) -> float:
    if not math.isfinite(value):
        return math.nan
    factor = 10 ** digits
    scaled = value * factor
    truncated = math.floor(scaled) if scaled >= 0 else math.ceil(scaled)
    return truncated / factor


def num(value: float, decimals: int = 3) -> str:
    if not math.isfinite(value):
        return "—"
    text = f"{value:.{decimals}f}".rstrip("0").rstrip(".")
    return text if text else "0"


class GoldMath:
    @staticmethod
    def summary(entries: list[dict[str, Any]]) -> dict[str, float | int]:
        valid = []
        for e in entries:
            w = safe_float(e.get("Weight", e.get("weight")))
            a = safe_float(e.get("Assay", e.get("assay")))
            if w > 0 and a > 0:
                valid.append((w, a))
        weight = sum(w for w, _ in valid)
        weighted = sum(w * a for w, a in valid)
        avg = weighted / weight if weight > 0 else math.nan
        return {"count": len(valid), "weight": weight, "weighted": weighted, "average": avg}

    @staticmethod
    def raise_assay(summary: dict[str, Any], target: float, bar_assay: float) -> dict[str, float]:
        weight = safe_float(summary.get("weight"), 0)
        avg = safe_float(summary.get("average"))
        if weight <= 0 or not math.isfinite(avg) or target <= 0 or bar_assay <= target:
            return {"difference": math.nan, "required": math.nan}
        diff = target - avg
        if diff <= 0:
            return {"difference": 0.0, "required": 0.0}
        required = rounddown_toward_zero(weight * diff / (bar_assay - target), 1)
        return {"difference": diff, "required": max(0.0, required)}

    @staticmethod
    def lower_assay(summary: dict[str, Any], target: float, silver_percent: float) -> dict[str, float]:
        weight = safe_float(summary.get("weight"), 0)
        avg = safe_float(summary.get("average"))
        if weight <= 0 or not math.isfinite(avg) or target <= 0 or silver_percent < 0:
            return {"total": math.nan, "silver": math.nan, "other": math.nan, "after": math.nan}
        if avg <= target:
            return {"total": 0.0, "silver": 0.0, "other": 0.0, "after": weight}
        total = weight * avg / target - weight
        silver = silver_percent / 100.0 * total
        return {"total": total, "silver": silver, "other": total - silver, "after": weight + total}

    @staticmethod
    def correction(base_weight: float, target_assay: float, assay_drop: float) -> dict[str, float]:
        denominator = target_assay - assay_drop
        if denominator == 0:
            return {"add": math.nan, "total": math.nan}
        add = base_weight * target_assay / denominator - base_weight
        return {"add": add, "total": base_weight + add}


class Backend(QObject):
    stateChanged = Signal(str)
    scaleChanged = Signal(str)
    toast = Signal(str, str)

    def __init__(self) -> None:
        super().__init__()
        APP_DIR.mkdir(parents=True, exist_ok=True)
        self._settings = self._load_settings()
        self._entries = self._load_entries()
        self._scale_lock = threading.RLock()
        self._stop_auto = threading.Event()
        self._auto_thread: threading.Thread | None = None
        self._last_scale: float | None = None
        self._scale_status = "آماده"
        self._scale_connected = False
        self._start_auto_if_needed()

    def _load_settings(self) -> dict[str, Any]:
        data: dict[str, Any] = {}
        try:
            if SETTINGS_PATH.exists():
                loaded = json.loads(SETTINGS_PATH.read_text(encoding="utf-8-sig"))
                if isinstance(loaded, dict):
                    data.update(loaded)
        except Exception:
            pass
        merged = DEFAULT_SETTINGS.copy()
        merged.update(data)
        merged["SettingsVersion"] = 7
        merged["StableSampleCount"] = max(2, min(10, int(merged.get("StableSampleCount", 3))))
        merged["StableToleranceGrams"] = max(0.001, min(5.0, safe_float(merged.get("StableToleranceGrams"), 0.02)))
        return merged

    def _save_settings(self) -> None:
        APP_DIR.mkdir(parents=True, exist_ok=True)
        SETTINGS_PATH.write_text(json.dumps(self._settings, ensure_ascii=False, indent=2), encoding="utf-8")

    def _load_entries(self) -> list[dict[str, Any]]:
        try:
            if ENTRIES_PATH.exists():
                raw = json.loads(ENTRIES_PATH.read_text(encoding="utf-8-sig"))
                if isinstance(raw, list):
                    result: list[dict[str, Any]] = []
                    for e in raw:
                        if not isinstance(e, dict):
                            continue
                        w = safe_float(e.get("Weight", e.get("weight")))
                        a = safe_float(e.get("Assay", e.get("assay")))
                        if w > 0 and a > 0:
                            result.append({
                                "Weight": w,
                                "Assay": a,
                                "Note": str(e.get("Note", e.get("note", "")) or ""),
                                "CreatedAt": str(e.get("CreatedAt", e.get("createdAt", "")) or ""),
                            })
                    return result
        except Exception:
            pass
        return []

    def _save_entries(self) -> None:
        APP_DIR.mkdir(parents=True, exist_ok=True)
        ENTRIES_PATH.write_text(json.dumps(self._entries, ensure_ascii=False, indent=2), encoding="utf-8")

    def _state(self) -> dict[str, Any]:
        summary = GoldMath.summary(self._entries)
        lower = GoldMath.lower_assay(summary, 746, 32)
        recent = []
        for idx, e in list(enumerate(self._entries))[-5:][::-1]:
            recent.append({**e, "index": idx})
        return {
            "version": VERSION,
            "summary": summary,
            "totalAlloy": lower.get("total", math.nan),
            "entries": self._entries,
            "recent": recent,
            "settings": self._settings,
            "scale": {
                "weight": self._last_scale,
                "status": self._scale_status,
                "connected": self._scale_connected,
            },
        }

    def _emit_state(self) -> None:
        self.stateChanged.emit(json.dumps(self._state(), ensure_ascii=False, allow_nan=False, default=lambda _: None))

    @Slot(result=str)
    def getState(self) -> str:
        return json.dumps(self._state(), ensure_ascii=False, allow_nan=False, default=lambda _: None)

    @Slot(str, str, str, result=str)
    def saveEntry(self, weight_text: str, assay_text: str, note: str) -> str:
        w = safe_float(str(weight_text).replace(",", "."))
        a = safe_float(str(assay_text).replace(",", "."))
        if not (w > 0):
            return json.dumps({"ok": False, "message": "وزن آبشده معتبر نیست."}, ensure_ascii=False)
        if not (0 < a <= 1000):
            return json.dumps({"ok": False, "message": "عیار باید بین 1 تا 1000 باشد."}, ensure_ascii=False)
        self._entries.append({
            "Weight": w,
            "Assay": a,
            "Note": (note or "").strip(),
            "CreatedAt": datetime.now().isoformat(timespec="minutes"),
        })
        self._save_entries()
        self._emit_state()
        return json.dumps({"ok": True, "message": "آبشده ثبت شد."}, ensure_ascii=False)

    @Slot(int, result=str)
    def deleteEntry(self, index: int) -> str:
        if 0 <= index < len(self._entries):
            self._entries.pop(index)
            self._save_entries()
            self._emit_state()
            return json.dumps({"ok": True}, ensure_ascii=False)
        return json.dumps({"ok": False}, ensure_ascii=False)

    @Slot(result=str)
    def clearEntries(self) -> str:
        self._entries.clear()
        self._save_entries()
        self._emit_state()
        return json.dumps({"ok": True}, ensure_ascii=False)

    @Slot(str, str, result=str)
    def calcRaise(self, target_text: str, bar_text: str) -> str:
        result = GoldMath.raise_assay(
            GoldMath.summary(self._entries),
            safe_float(target_text.replace(",", ".")),
            safe_float(bar_text.replace(",", ".")),
        )
        return json.dumps(result, allow_nan=False, default=lambda _: None)

    @Slot(str, str, result=str)
    def calcLower(self, target_text: str, silver_text: str) -> str:
        result = GoldMath.lower_assay(
            GoldMath.summary(self._entries),
            safe_float(target_text.replace(",", ".")),
            safe_float(silver_text.replace(",", ".")),
        )
        return json.dumps(result, allow_nan=False, default=lambda _: None)

    @Slot(str, result=str)
    def calcSplit(self, value_text: str) -> str:
        v = safe_float(value_text.replace(",", "."), 0)
        return json.dumps({"a": v * 0.3679, "b": v * 0.6321})

    @Slot(str, str, str, result=str)
    def calcCorrection(self, weight_text: str, target_text: str, drop_text: str) -> str:
        result = GoldMath.correction(
            safe_float(weight_text.replace(",", "."), 0),
            safe_float(target_text.replace(",", "."), 0),
            safe_float(drop_text.replace(",", "."), 0),
        )
        return json.dumps(result, allow_nan=False, default=lambda _: None)

    @Slot(result=str)
    def chooseReportFolder(self) -> str:
        selected = QFileDialog.getExistingDirectory(None, "انتخاب پوشه گزارش", self._settings.get("ReportFolder") or default_report_folder())
        if selected:
            self._settings["ReportFolder"] = selected
            self._save_settings()
            self._emit_state()
        return selected or ""

    @Slot(str, result=str)
    def saveSettings(self, payload: str) -> str:
        try:
            incoming = json.loads(payload)
            allowed = set(DEFAULT_SETTINGS)
            for key, value in incoming.items():
                if key in allowed:
                    self._settings[key] = value
            self._settings["SettingsVersion"] = 7
            self._settings["AutoRead"] = bool(self._settings.get("AutoRead", False))
            self._settings["StableSampleCount"] = max(2, min(10, int(self._settings.get("StableSampleCount", 3))))
            self._settings["StableToleranceGrams"] = max(0.001, min(5.0, safe_float(self._settings.get("StableToleranceGrams"), 0.02)))
            self._save_settings()
            self._restart_auto()
            self._emit_state()
            return json.dumps({"ok": True}, ensure_ascii=False)
        except Exception as ex:
            return json.dumps({"ok": False, "message": str(ex)}, ensure_ascii=False)

    @Slot(result=str)
    def saveReport(self) -> str:
        try:
            folder = Path(self._settings.get("ReportFolder") or default_report_folder())
            folder.mkdir(parents=True, exist_ok=True)
            summary = GoldMath.summary(self._entries)
            raise_result = GoldMath.raise_assay(summary, 747, 995)
            lower_result = GoldMath.lower_assay(summary, 746, 32)
            stamp = datetime.now()
            path = folder / f"GoldBar_{stamp:%Y-%m-%d_%H-%M-%S}.txt"
            melts = " | ".join(f"{num(e['Weight'])}g @ {num(e['Assay'], 0)}‰" for e in self._entries) or "—"
            lines = [
                "GOLD BAR — Amirnourhan",
                f"تاریخ/زمان: {stamp:%Y-%m-%d %H:%M:%S}",
                "",
                f"آبشده‌ها: {melts}",
                f"وزن کل: {num(summary['weight'])} g",
                f"عیار میانگین: {num(summary['average'])} ‰",
                f"تعداد آبشده: {summary['count']}",
                "",
                f"بالا بردن تا 747 با شمش 995: {num(raise_result['required'])} g",
                f"پایین آوردن تا 746 — کل بار: {num(lower_result['total'])} g",
                f"نقره 32٪: {num(lower_result['silver'])} g",
                f"بار بدون نقره: {num(lower_result['other'])} g",
                f"وزن پس از بار: {num(lower_result['after'])} g",
            ]
            path.write_text("\n".join(lines), encoding="utf-8")
            return json.dumps({"ok": True, "path": str(path)}, ensure_ascii=False)
        except Exception as ex:
            return json.dumps({"ok": False, "message": str(ex)}, ensure_ascii=False)

    @Slot()
    def openInstagram(self) -> None:
        QDesktopServices.openUrl(QUrl("https://www.instagram.com/4mirnourhan/"))

    @Slot()
    def requestWeight(self) -> None:
        threading.Thread(target=self._read_weight_once, daemon=True).start()

    @Slot()
    def testScale(self) -> None:
        threading.Thread(target=self._read_weight_once, kwargs={"test_only": True}, daemon=True).start()

    def _serial_kwargs(self) -> dict[str, Any]:
        s = self._settings
        parity_map = {
            "None": serial.PARITY_NONE,
            "Even": serial.PARITY_EVEN,
            "Odd": serial.PARITY_ODD,
            "Mark": serial.PARITY_MARK,
            "Space": serial.PARITY_SPACE,
        }
        stop_map = {
            "One": serial.STOPBITS_ONE,
            "OnePointFive": serial.STOPBITS_ONE_POINT_FIVE,
            "Two": serial.STOPBITS_TWO,
            "1": serial.STOPBITS_ONE,
            "1.5": serial.STOPBITS_ONE_POINT_FIVE,
            "2": serial.STOPBITS_TWO,
        }
        hand = str(s.get("Handshake", "None"))
        return {
            "port": str(s.get("PortName", "COM1")),
            "baudrate": int(s.get("BaudRate", 2400)),
            "bytesize": serial.SEVENBITS if int(s.get("DataBits", 7)) == 7 else serial.EIGHTBITS,
            "parity": parity_map.get(str(s.get("Parity", "Even")), serial.PARITY_EVEN),
            "stopbits": stop_map.get(str(s.get("StopBits", "Two")), serial.STOPBITS_TWO),
            "timeout": max(0.35, int(s.get("ReadTimeoutMs", 1800)) / 1000.0),
            "write_timeout": 0.8,
            "xonxoff": hand in ("XOnXOff", "RequestToSendXOnXOff"),
            "rtscts": hand in ("RequestToSend", "RequestToSendXOnXOff"),
        }

    def _query_bytes(self) -> bytes:
        s = self._settings
        ending = {"CR": "\r", "LF": "\n", "CRLF": "\r\n", "None": ""}.get(str(s.get("QueryLineEnding", "CRLF")), "\r\n")
        return (str(s.get("QueryCommand", "Q")) + ending).encode("ascii", errors="ignore")

    def _parse_weight(self, raw: str) -> float | None:
        text = raw.strip()
        sep = str(self._settings.get("DecimalSeparator", "."))
        if sep and sep != ".":
            text = text.replace(sep, ".")
        text = text.replace(",", ".")
        matches = re.findall(r"[-+]?\d+(?:\.\d+)?", text)
        for token in reversed(matches):
            try:
                v = float(token)
                if math.isfinite(v):
                    return v
            except Exception:
                pass
        return None

    def _read_weight_once(self, test_only: bool = False) -> None:
        with self._scale_lock:
            self._set_scale_state("در حال اتصال…", False)
            try:
                with serial.Serial(**self._serial_kwargs()) as port:
                    self._set_scale_state(f"متصل • {self._settings.get('PortName', 'COM1')}", True)
                    if self._settings.get("SendQueryOnUpArrow", True) and self._settings.get("QueryCommand"):
                        port.write(self._query_bytes())
                        port.flush()
                    deadline = time.monotonic() + max(0.5, int(self._settings.get("ReadTimeoutMs", 1800)) / 1000.0)
                    buffer = ""
                    value: float | None = None
                    while time.monotonic() < deadline:
                        chunk = port.read_until(b"\n")
                        if chunk:
                            buffer += chunk.decode("ascii", errors="ignore")
                            parsed = self._parse_weight(buffer)
                            if parsed is not None:
                                value = parsed
                                break
                    if value is None:
                        raise TimeoutError("وزنی از ترازو دریافت نشد.")
                    if not test_only:
                        self._last_scale = value
                    self._set_scale_state(f"وزن دریافت شد: {num(value)} g", True, value)
                    if test_only:
                        self.toast.emit("success", f"تست ترازو موفق بود: {num(value)} g")
            except Exception as ex:
                self._set_scale_state(f"ترازو: {ex}", False)
                self.toast.emit("error", str(ex))

    def _set_scale_state(self, status: str, connected: bool, weight: float | None = None) -> None:
        self._scale_status = status
        self._scale_connected = connected
        if weight is not None:
            self._last_scale = weight
        payload = {
            "status": status,
            "connected": connected,
            "weight": self._last_scale,
        }
        self.scaleChanged.emit(json.dumps(payload, ensure_ascii=False))
        self._emit_state()

    def _restart_auto(self) -> None:
        self._stop_auto.set()
        if self._auto_thread and self._auto_thread.is_alive():
            self._auto_thread.join(timeout=0.25)
        self._stop_auto = threading.Event()
        self._auto_thread = None
        self._start_auto_if_needed()

    def _start_auto_if_needed(self) -> None:
        if not bool(self._settings.get("AutoRead", False)):
            return
        self._auto_thread = threading.Thread(target=self._auto_loop, daemon=True)
        self._auto_thread.start()

    def _auto_loop(self) -> None:
        samples: list[float] = []
        required = max(2, int(self._settings.get("StableSampleCount", 3)))
        tolerance = max(0.001, safe_float(self._settings.get("StableToleranceGrams"), 0.02))
        while not self._stop_auto.is_set():
            try:
                with serial.Serial(**self._serial_kwargs()) as port:
                    self._set_scale_state(f"متصل • {self._settings.get('PortName', 'COM1')}", True)
                    while not self._stop_auto.is_set():
                        raw = port.read_until(b"\n")
                        if not raw:
                            continue
                        value = self._parse_weight(raw.decode("ascii", errors="ignore"))
                        if value is None:
                            continue
                        samples.append(value)
                        samples = samples[-required:]
                        if len(samples) == required and max(samples) - min(samples) <= tolerance:
                            stable = sum(samples) / len(samples)
                            if self._last_scale is None or abs(stable - self._last_scale) >= 0.001:
                                self._last_scale = stable
                                self._set_scale_state(f"پایدار • {num(stable)} g", True, stable)
            except Exception as ex:
                self._set_scale_state(f"ترازو: {ex}", False)
                self._stop_auto.wait(1.0)

    def shutdown(self) -> None:
        self._stop_auto.set()


class MainWindow(QMainWindow):
    def __init__(self, backend: Backend) -> None:
        super().__init__()
        self.backend = backend
        self.setWindowTitle("GOLD BAR (by:Amirnourhan)")
        self.resize(1540, 940)
        self.setMinimumSize(1180, 760)

        icon_path = resource_path("AppIcon.ico")
        if icon_path.exists():
            self.setWindowIcon(QIcon(str(icon_path)))

        self.web = QWebEngineView(self)
        self.setCentralWidget(self.web)
        self.channel = QWebChannel(self.web.page())
        self.channel.registerObject("backend", self.backend)
        self.web.page().setWebChannel(self.channel)
        self.web.setUrl(QUrl.fromLocalFile(str(resource_path("web/index.html"))))

    def closeEvent(self, event) -> None:  # type: ignore[override]
        self.backend.shutdown()
        super().closeEvent(event)


def resource_path(relative: str) -> Path:
    root = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return root / relative


def run_self_test() -> int:
    entries = [
        {"Weight": 183.95, "Assay": 750},
        {"Weight": 316.05, "Assay": 720},
    ]
    summary = GoldMath.summary(entries)
    if abs(summary["weight"] - 500.0) > 1e-9:
        raise RuntimeError("summary weight failed")
    if abs(summary["average"] - 731.037) > 1e-6:
        raise RuntimeError("average assay failed")
    raise_result = GoldMath.raise_assay(summary, 747, 995)
    if abs(raise_result["required"] - 32.1) > 1e-9:
        raise RuntimeError("raise assay failed")
    lower_result = GoldMath.lower_assay(summary, 746, 32)
    if lower_result["total"] != 0:
        raise RuntimeError("lower assay must not be negative")
    if DEFAULT_SETTINGS["AutoRead"] is not False:
        raise RuntimeError("AutoRead must be off by default")
    print("PYTHON CORE SELFTEST PASS")
    return 0


def main() -> int:
    if os.environ.get("GOLDBAR_PY_SELFTEST") == "1":
        return run_self_test()

    app = QApplication(sys.argv)
    app.setApplicationName("Gold Bar")
    app.setOrganizationName("Amirnourhan")

    backend = Backend()
    window = MainWindow(backend)

    splash_pix = resource_path("web/splash.png")
    splash = QSplashScreen()
    if splash_pix.exists():
        from PySide6.QtGui import QPixmap
        splash.setPixmap(QPixmap(str(splash_pix)))
    splash.showMessage("در حال بارگذاری…", 0x0084 | 0x0004, 0xFFF1C1)  # AlignCenter | AlignBottom
    splash.show()
    app.processEvents()

    def show_main() -> None:
        window.show()
        if not os.environ.get("GOLDBAR_UI_SIZE"):
            window.showMaximized()
        splash.finish(window)
        capture = os.environ.get("GOLDBAR_UI_SCREENSHOT")
        if capture:
            QTimer.singleShot(1800, lambda: capture_window(window, Path(capture)))

    QTimer.singleShot(320, show_main)
    return app.exec()


def capture_window(window: MainWindow, path: Path) -> None:
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        screen = QApplication.primaryScreen()
        pixmap = screen.grabWindow(int(window.winId()))
        pixmap.save(str(path), "PNG")
    finally:
        if os.environ.get("GOLDBAR_SCREENSHOT_EXIT") == "1":
            QApplication.quit()


if __name__ == "__main__":
    raise SystemExit(main())
