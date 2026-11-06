# -*- coding: utf-8 -*-
"""moon.py のテスト。標準ライブラリの unittest だけで動く。

実行：リポジトリのルートで  python -m unittest discover -s tests -v

正解の日付は、国立天文台「令和8年（2026）暦要項」の朔（新月）と望（満月）の日（日本時間）。
https://eco.mtk.nao.ac.jp/koyomi/yoko/2026/rekiyou263.html
平均の朔望月で計算しているので、実際の月とは最大で半日ほどずれる。
そのため「その日の正午（日本時間）に、正しい玉に丸められるか」で確かめる。
"""

import os
import sys
import unittest
from datetime import datetime, timedelta, timezone

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from moon import (  # noqa: E402
    KNOWN_NEW_MOON,
    PHASE_NAMES,
    SYNODIC_MONTH,
    moon_age,
    moon_phase_value,
    phase_label,
    phase_step,
)


JST = timezone(timedelta(hours=9))


def noon_utc(y, m, d):
    return datetime(y, m, d, 12, 0, tzinfo=timezone.utc)


def noon_jst(y, m, d):
    return datetime(y, m, d, 12, 0, tzinfo=JST)


# 2026年の新月と満月の日（日本時間、国立天文台の暦要項）
NEW_MOONS_2026 = [(1, 19), (2, 17), (3, 19), (4, 17), (5, 17), (6, 15),
                  (7, 14), (8, 13), (9, 11), (10, 11), (11, 9), (12, 9)]
FULL_MOONS_2026 = [(1, 3), (2, 2), (3, 3), (4, 2), (5, 2), (5, 31),
                   (6, 30), (7, 29), (8, 28), (9, 27), (10, 26), (11, 24), (12, 24)]


class 月齢の計算(unittest.TestCase):
    def test_起点の新月では月齢が0になる(self):
        self.assertAlmostEqual(moon_age(KNOWN_NEW_MOON), 0.0, places=6)

    def test_朔望月ぶん進むと月齢は元に戻る(self):
        t = noon_utc(2026, 11, 6)
        later = t + timedelta(days=SYNODIC_MONTH)
        self.assertAlmostEqual(moon_age(t), moon_age(later), places=4)

    def test_月齢はいつも0以上で朔望月未満(self):
        t = noon_utc(2020, 1, 1)
        for i in range(0, 3000, 7):
            age = moon_age(t + timedelta(days=i))
            self.assertGreaterEqual(age, 0.0)
            self.assertLess(age, SYNODIC_MONTH)

    def test_起点より前の日付でも計算できる(self):
        age = moon_age(datetime(1999, 1, 1, tzinfo=timezone.utc))
        self.assertTrue(0.0 <= age < SYNODIC_MONTH)

    def test_タイムゾーンのない日時はエラーにする(self):
        with self.assertRaises(ValueError):
            moon_age(datetime(2026, 11, 6, 12, 0))

    def test_日本時間で渡してもUTCと同じ結果になる(self):
        a = moon_age(datetime(2026, 11, 6, 21, 0, tzinfo=JST))
        b = moon_age(datetime(2026, 11, 6, 12, 0, tzinfo=timezone.utc))
        self.assertAlmostEqual(a, b, places=9)


class 冠の9段階への丸め(unittest.TestCase):
    def test_2026年の新月の日は両端の新月の玉になる(self):
        for m, d in NEW_MOONS_2026:
            with self.subTest(月=m, 日=d):
                value, _ = moon_phase_value(noon_jst(2026, m, d))
                self.assertIn(phase_step(value), (0, 8))

    def test_2026年の満月の日はてっぺんの満月の玉になる(self):
        for m, d in FULL_MOONS_2026:
            with self.subTest(月=m, 日=d):
                value, _ = moon_phase_value(noon_jst(2026, m, d))
                self.assertEqual(phase_step(value), 4)

    def test_丸めた値は8分の1刻みになる(self):
        t = noon_utc(2026, 1, 1)
        for i in range(60):
            value, _ = moon_phase_value(t + timedelta(hours=12 * i))
            self.assertAlmostEqual(value * 8, round(value * 8), places=9)
            self.assertTrue(0.0 <= value <= 1.0)

    def test_丸めないときは0から1の連続した値を返す(self):
        value, age = moon_phase_value(noon_utc(2026, 11, 6), snap=False)
        self.assertAlmostEqual(value, age / SYNODIC_MONTH, places=9)

    def test_玉の番号は0から8に収まる(self):
        self.assertEqual(phase_step(0.0), 0)
        self.assertEqual(phase_step(0.5), 4)
        self.assertEqual(phase_step(1.0), 8)


class 表示の文字(unittest.TestCase):
    def test_名前は9個で両端が新月(self):
        self.assertEqual(len(PHASE_NAMES), 9)
        self.assertEqual(PHASE_NAMES[0], "新月")
        self.assertEqual(PHASE_NAMES[-1], "新月")
        self.assertEqual(PHASE_NAMES[4], "満月")

    def test_満月の日は満月と月齢が出る(self):
        label = phase_label(noon_jst(2026, 11, 24))
        self.assertTrue(label.startswith("満月（月齢 "))
        self.assertTrue(label.endswith("日）"))


if __name__ == "__main__":
    unittest.main()
