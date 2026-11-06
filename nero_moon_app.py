# -*- coding: utf-8 -*-
"""
NeroMoon - ネロの冠の月齢を、今日の日付から自動で合わせる常駐アプリ

・起動すると画面右下（タスクトレイ）に月のアイコンが出て、裏で動き続けます
・60秒ごとに VRChat の OSC へ /avatar/parameters/MoonPhase を送ります
・初回起動時に「Windows起動時に自動で起動」を自動で登録します（アイコンの右クリックで解除可）
・二重起動はしません
・月齢の計算は moon.py にある（Windows がなくてもテストできるように分けている）
"""

import ctypes
import json
import os
import sys
import threading
from datetime import datetime, timezone

from PIL import Image, ImageDraw
from pythonosc.udp_client import SimpleUDPClient
import pystray

from moon import moon_phase_value, phase_label

APP_NAME = "NeroMoon"

# ===== OSC の設定 =====
VRCHAT_IP = "127.0.0.1"
VRCHAT_PORT = 9000
PARAM = "/avatar/parameters/MoonPhase"
SEND_INTERVAL_SEC = 60

CONFIG_DIR = os.path.join(os.environ.get("APPDATA", os.path.expanduser("~")), APP_NAME)
CONFIG_PATH = os.path.join(CONFIG_DIR, "config.json")
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"


# ===== 設定ファイル =====
def load_config():
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_config(cfg):
    os.makedirs(CONFIG_DIR, exist_ok=True)
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)


# ===== Windows起動時の自動起動（レジストリの Run に登録） =====
def launch_command():
    if getattr(sys, "frozen", False):          # exe として動いているとき
        return f'"{sys.executable}"'
    pyw = sys.executable.replace("python.exe", "pythonw.exe")
    return f'"{pyw}" "{os.path.abspath(__file__)}"'


def is_startup_enabled():
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_READ) as k:
            winreg.QueryValueEx(k, APP_NAME)
            return True
    except Exception:
        return False


def set_startup(enabled):
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_SET_VALUE) as k:
        if enabled:
            winreg.SetValueEx(k, APP_NAME, 0, winreg.REG_SZ, launch_command())
        else:
            try:
                winreg.DeleteValue(k, APP_NAME)
            except FileNotFoundError:
                pass


# ===== 二重起動の防止 =====
def already_running():
    if os.name != "nt":
        return False
    ctypes.windll.kernel32.CreateMutexW(None, False, "Global\\NeroMoonOSC_Mutex")
    return ctypes.windll.kernel32.GetLastError() == 183  # ERROR_ALREADY_EXISTS


# ===== トレイアイコンの絵（紺の円にシアンの三日月） =====
def make_icon_image():
    s = 64
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([2, 2, s - 2, s - 2], fill=(16, 23, 42, 255))
    d.ellipse([12, 10, 52, 50], fill=(63, 227, 232, 255))
    d.ellipse([20, 6, 60, 46], fill=(16, 23, 42, 255))
    return img


class App:
    def __init__(self):
        self.client = SimpleUDPClient(VRCHAT_IP, VRCHAT_PORT)
        self.label = "準備中…"
        self.stop = threading.Event()
        self.icon = pystray.Icon(
            APP_NAME,
            make_icon_image(),
            "ネロの月齢",
            menu=pystray.Menu(
                pystray.MenuItem(lambda item: f"いまの月: {self.label}", None, enabled=False),
                pystray.MenuItem("今すぐ送信", lambda icon, item: self.send()),
                pystray.Menu.SEPARATOR,
                pystray.MenuItem(
                    "Windows起動時に自動で起動",
                    self.toggle_startup,
                    checked=lambda item: is_startup_enabled(),
                ),
                pystray.MenuItem("終了", self.quit),
            ),
        )

    def send(self):
        now = datetime.now(timezone.utc)
        value, _ = moon_phase_value(now)
        try:
            self.client.send_message(PARAM, float(value))
        except Exception:
            pass
        self.label = phase_label(now)
        self.icon.title = f"ネロの月齢: {self.label}"
        try:
            self.icon.update_menu()
        except Exception:
            pass

    def loop(self):
        while not self.stop.is_set():
            self.send()
            self.stop.wait(SEND_INTERVAL_SEC)

    def toggle_startup(self, icon, item):
        set_startup(not is_startup_enabled())
        cfg = load_config()
        cfg["startup_asked"] = True
        save_config(cfg)
        icon.update_menu()

    def quit(self, icon, item):
        self.stop.set()
        icon.stop()

    def on_ready(self, icon):
        icon.visible = True
        cfg = load_config()
        if not cfg.get("startup_asked"):
            try:
                set_startup(True)
            except Exception:
                pass
            cfg["startup_asked"] = True
            save_config(cfg)
            try:
                icon.notify(
                    "ネロの月齢アプリが起動しました。\n"
                    "次回からはPCの起動時に自動で動きます。\n"
                    "止めたいときは右下の月アイコンを右クリック →「終了」",
                    "NeroMoon",
                )
            except Exception:
                pass
        threading.Thread(target=self.loop, daemon=True).start()

    def run(self):
        self.icon.run(setup=self.on_ready)


if __name__ == "__main__":
    if already_running():
        sys.exit(0)
    App().run()
