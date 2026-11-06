# -*- coding: utf-8 -*-
"""
月齢の計算だけを集めたモジュール。

トレイ表示や OSC 送信（nero_moon_app.py）から切り離してあるのは、
Windows や VRChat がなくてもテストできるようにするため。
外部ライブラリは使わず、標準ライブラリの datetime だけで動く。
"""

from datetime import datetime, timezone

# 朔望月（新月から次の新月までの平均の長さ、日）
SYNODIC_MONTH = 29.530588853

# 計算の起点にする、実際にあった新月の日時（2000年1月6日 18:14 UTC）
KNOWN_NEW_MOON = datetime(2000, 1, 6, 18, 14, tzinfo=timezone.utc)

# 冠の9個の玉に対応する名前（正面から見て左から右。三面図「月齢の冠の並び」と同じ）
PHASE_NAMES = ["新月", "三日月", "上弦", "十三夜", "満月", "寝待月", "下弦", "有明月", "新月"]

STEPS = len(PHASE_NAMES) - 1  # 0〜8 の 9段階 → 区切りは 8


def moon_age(now_utc):
    """月齢（新月からの経過日数、0 以上 29.53 未満）を返す。

    タイムゾーンのない datetime を渡すと、日本時間かUTCかで最大9時間ずれるので、
    黙って計算せずにエラーにする。
    """
    if now_utc.tzinfo is None:
        raise ValueError("タイムゾーン付きの datetime を渡してください（例：datetime.now(timezone.utc)）")
    days = (now_utc - KNOWN_NEW_MOON).total_seconds() / 86400.0
    return days % SYNODIC_MONTH


def moon_phase_value(now_utc, snap=True):
    """VRChat の MoonPhase パラメーターに送る値（0.0〜1.0）と月齢を返す。

    snap=True のときは 9段階（0, 0.125, … , 1.0）に丸める。
    冠の玉は 9個しかなく、間の値だと隣り合う2つの玉が半分ずつ光ってしまうため。
    """
    age = moon_age(now_utc)
    value = age / SYNODIC_MONTH
    if snap:
        value = round(value * STEPS) / STEPS
    return value, age


def phase_step(value):
    """MoonPhase の値（0.0〜1.0）を、玉の番号（0〜8）に直す。"""
    return int(round(value * STEPS))


def phase_label(now_utc):
    """トレイに出す文字（例：「満月（月齢 14.8日）」）を返す。"""
    value, age = moon_phase_value(now_utc)
    return f"{PHASE_NAMES[phase_step(value)]}（月齢 {age:.1f}日）"
