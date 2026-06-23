from __future__ import annotations

import argparse
import json
import math
import os
import random
import sys
import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageTk


ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets"
SKINS = ROOT / "skins"
SETTINGS_FILE = ROOT / "settings.json"
PHRASES_FILE = ROOT / "phrases.json"
PET_IMAGE = ASSETS / "pet.png"
PET_BLINK_25_IMAGE = ASSETS / "pet-blink-25.png"
PET_HALF_BLINK_IMAGE = ASSETS / "pet-blink-half.png"
PET_BLINK_75_IMAGE = ASSETS / "pet-blink-75.png"
PET_BLINK_IMAGE = ASSETS / "pet-blink.png"
PET_HAPPY_IMAGE = ASSETS / "pet-happy.png"
PET_SLEEPY_IMAGE = ASSETS / "pet-sleepy.png"
PET_WRONGED_IMAGE = ASSETS / "pet-wronged.png"
PET_SURPRISED_IMAGE = ASSETS / "pet-surprised.png"
RUN_RIGHT_IMAGES = tuple(ASSETS / f"pet-run-right-{index}.png" for index in range(1, 4))
RUN_LEFT_IMAGES = tuple(ASSETS / f"pet-run-left-{index}.png" for index in range(1, 4))
EXPRESSION_NAMES = ("neutral", "happy", "sleepy", "wronged", "surprised")
EXPRESSION_FRAME_PATHS = {
    name: tuple(ASSETS / f"pet-{name}-{frame}.png" for frame in range(1, 4)) for name in EXPRESSION_NAMES
}
EXPRESSION_LOOP = (1, 2, 3, 2)
RUN_LOOP = (1, 2, 3, 2)
TRANSPARENT = "#010203"
DEFAULT_SKIN_ID = "current"

OPACITY_OPTIONS = (("75%", 0.75), ("85%", 0.85), ("95%", 0.95), ("100%", 1.0))
MIN_SCALE = 0.16
MAX_SCALE = 1.15
BUBBLE_WIDTH_MIN = 170
BUBBLE_WIDTH_MAX = 520
BUBBLE_FONT_MIN = 9
BUBBLE_FONT_MAX = 18
MOOD_CARD_HOLD_FRAMES = 160
MOOD_CARD_FADE_FRAMES = 34

DEFAULT_SETTINGS: dict[str, Any] = {
    "x": None,
    "y": None,
    "scale": 0.32,
    "bubble_width": 220,
    "bubble_font_size": 11,
    "opacity": 1.0,
    "topmost": True,
    "mood": 72,
    "startup": False,
    "quiet_mode": False,
    "lock_position": False,
    "skin_id": DEFAULT_SKIN_ID,
}

DEFAULT_PHRASES: dict[str, list[str]] = {
    "startup": ["我高清重制回来了。", "今天也在桌面待命。", "开机成功，开始陪伴。"],
    "happy": ["今天状态不错。", "可以，再摸一下。", "我感觉电量很足。"],
    "calm": ["我在桌面待命。", "需要我陪你工作吗？", "先安静陪你一会儿。"],
    "sleepy": ["有点困了。", "我先低功耗待机。", "你也休息一下吧。"],
    "wronged": ["别一直戳我嘛。", "我有一点委屈。", "好吧，我先缩一下。"],
    "pat": ["头发别揉太乱。", "嗯，摸头通过。", "这下精神一点了。"],
    "poke": ["诶，戳到了。", "我看见你的鼠标了。", "轻点，我只是个桌宠。"],
    "drag": ["拎起来了。", "新位置不错。", "我换个地方继续陪你。"],
    "rest": ["进入省电陪伴模式。", "我先眯一会儿。", "休息一下也很好。"],
    "settings": ["设置保存好了。", "收到，已经记住。"],
    "feed": ["能量补上来了。", "这口续航不错。", "好了，精神恢复一点。"],
    "locked": ["位置锁住了。", "我先站在这里不乱跑。", "锁定中，拖不动我。"],
    "snap": ["贴边站好了。", "靠边一点，不挡你。", "我挪到边上了。"],
}


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def load_json(path: Path, default: dict[str, Any]) -> dict[str, Any]:
    if not path.exists():
        write_json(path, default)
        return dict(default)
    try:
        loaded = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return dict(default)
    if not isinstance(loaded, dict):
        return dict(default)
    merged = dict(default)
    merged.update(loaded)
    return merged


def load_phrases() -> dict[str, list[str]]:
    loaded = load_json(PHRASES_FILE, DEFAULT_PHRASES)
    phrases: dict[str, list[str]] = {}
    changed = False
    for key, fallback in DEFAULT_PHRASES.items():
        value = loaded.get(key)
        if isinstance(value, list) and all(isinstance(item, str) for item in value) and value:
            phrases[key] = value
        else:
            phrases[key] = fallback
            changed = True
    if changed:
        write_json(PHRASES_FILE, {**loaded, **phrases})
    return phrases


def load_settings() -> dict[str, Any]:
    settings = load_json(SETTINGS_FILE, DEFAULT_SETTINGS)
    settings["scale"] = clamp(float(settings.get("scale", DEFAULT_SETTINGS["scale"])), MIN_SCALE, MAX_SCALE)
    settings["bubble_width"] = int(
        clamp(float(settings.get("bubble_width", DEFAULT_SETTINGS["bubble_width"])), BUBBLE_WIDTH_MIN, BUBBLE_WIDTH_MAX)
    )
    settings["bubble_font_size"] = int(
        clamp(float(settings.get("bubble_font_size", DEFAULT_SETTINGS["bubble_font_size"])), BUBBLE_FONT_MIN, BUBBLE_FONT_MAX)
    )
    settings["opacity"] = clamp(float(settings.get("opacity", DEFAULT_SETTINGS["opacity"])), 0.65, 1.0)
    settings["mood"] = int(clamp(float(settings.get("mood", DEFAULT_SETTINGS["mood"])), 0, 100))
    settings["topmost"] = bool(settings.get("topmost", True))
    settings["startup"] = bool(settings.get("startup", False))
    settings["quiet_mode"] = bool(settings.get("quiet_mode", False))
    settings["lock_position"] = bool(settings.get("lock_position", False))
    skin_id = str(settings.get("skin_id", DEFAULT_SKIN_ID)).strip() or DEFAULT_SKIN_ID
    if not (SKINS / skin_id).exists():
        skin_id = DEFAULT_SKIN_ID
    settings["skin_id"] = skin_id
    return settings


def window_safe_image(image: Image.Image) -> Image.Image:
    """Avoid chroma-key halos from semi-transparent pixels in Tk windows."""
    safe = image.convert("RGBA")
    alpha = safe.getchannel("A")
    alpha = alpha.point(lambda value: 255 if value >= 112 else 0)
    safe.putalpha(alpha)
    return safe


def startup_script_path() -> Path:
    appdata = os.environ.get("APPDATA")
    if appdata:
        startup = Path(appdata) / "Microsoft" / "Windows" / "Start Menu" / "Programs" / "Startup"
    else:
        startup = Path.home() / "AppData" / "Roaming" / "Microsoft" / "Windows" / "Start Menu" / "Programs" / "Startup"
    return startup / "启动电子桌宠.vbs"


def pythonw_path() -> Path:
    current = Path(sys.executable)
    if current.name.lower() == "python.exe":
        sibling = current.with_name("pythonw.exe")
        if sibling.exists():
            return sibling
    return current


@dataclass
class DragState:
    start_x: int = 0
    start_y: int = 0
    window_x: int = 0
    window_y: int = 0
    last_x: int = 0
    last_y: int = 0
    direction: str = "right"
    moved: bool = False


@dataclass
class Particle:
    x: float
    y: float
    vx: float
    vy: float
    life: int
    max_life: int
    kind: str
    color: str
    size: int


class DesktopPet:
    def __init__(self) -> None:
        self.phrases = load_phrases()
        self.settings = load_settings()
        self.startup_path = startup_script_path()
        self.settings["startup"] = self.startup_path.exists()

        self.root = tk.Tk()
        self.root.title("Zhihao Desktop Pet")
        self.root.overrideredirect(True)
        self.root.attributes("-topmost", self.settings["topmost"])
        self.root.attributes("-alpha", self.settings["opacity"])
        self.root.configure(bg=TRANSPARENT)

        try:
            self.root.wm_attributes("-transparentcolor", TRANSPARENT)
        except tk.TclError:
            pass

        self.source_images = {
            "open": Image.open(self.asset_path("pet.png")).convert("RGBA"),
            "blink25": Image.open(self.asset_path("pet-blink-25.png")).convert("RGBA"),
            "half": Image.open(self.asset_path("pet-blink-half.png")).convert("RGBA"),
            "blink75": Image.open(self.asset_path("pet-blink-75.png")).convert("RGBA"),
            "closed": Image.open(self.asset_path("pet-blink.png")).convert("RGBA"),
            "happy": Image.open(self.asset_path("pet-happy.png")).convert("RGBA"),
            "sleepy": Image.open(self.asset_path("pet-sleepy.png")).convert("RGBA"),
            "wronged": Image.open(self.asset_path("pet-wronged.png")).convert("RGBA"),
            "surprised": Image.open(self.asset_path("pet-surprised.png")).convert("RGBA"),
        }
        for frame_index, path in enumerate((self.asset_path(f"pet-run-right-{index}.png") for index in range(1, 4)), start=1):
            self.source_images[f"run_right_{frame_index}"] = Image.open(path).convert("RGBA")
        for frame_index, path in enumerate((self.asset_path(f"pet-run-left-{index}.png") for index in range(1, 4)), start=1):
            self.source_images[f"run_left_{frame_index}"] = Image.open(path).convert("RGBA")
        for expression, paths in EXPRESSION_FRAME_PATHS.items():
            for frame_index, path in enumerate((self.asset_path(path.name) for path in paths), start=1):
                self.source_images[f"{expression}_{frame_index}"] = Image.open(path).convert("RGBA")
        self.scale = float(self.settings["scale"])
        self.bubble_width = int(self.settings["bubble_width"])
        self.bubble_font_size = int(self.settings["bubble_font_size"])
        self.settings_window: tk.Toplevel | None = None
        self.images: dict[str, Image.Image] = {}
        self.photos: dict[str, ImageTk.PhotoImage] = {}
        self.pet_width = 0
        self.pet_height = 0
        self.width = 0
        self.height = 0
        self.pet_x = 0
        self.pet_base_y = 0
        self.current_pet_x = 0
        self.current_pet_y = 0
        self.rescale_images()

        self.canvas = tk.Canvas(
            self.root,
            width=self.width,
            height=self.height,
            bg=TRANSPARENT,
            highlightthickness=0,
            bd=0,
        )
        self.canvas.pack(fill="both", expand=True)

        self.tick = 0
        self.mood = int(self.settings["mood"])
        self.speech = ""
        self.bubble_after: str | None = None
        self.blink_frame = "open"
        self.blink_sequence: list[str] = []
        self.expression_frame = "neutral"
        self.expression_frames = 0
        self.reaction_frames = 0
        self.reaction_total = 1
        self.mood_card_frames = 0
        self.drag = DragState()
        self.is_dragging = False
        self.drag_hint_shown = False
        self.particles: list[Particle] = []
        self.wronged_frames = 0
        self.poke_combo = 0
        self.poke_reset_after: str | None = None
        self.idle_action = "idle"
        self.idle_frames = 0
        self.idle_total = 1
        self.idle_direction = 1

        self.topmost_var = tk.BooleanVar(value=bool(self.settings["topmost"]))
        self.startup_var = tk.BooleanVar(value=self.startup_path.exists())
        self.quiet_var = tk.BooleanVar(value=bool(self.settings["quiet_mode"]))
        self.lock_var = tk.BooleanVar(value=bool(self.settings["lock_position"]))
        self.opacity_var = tk.StringVar(value=self.opacity_value(float(self.settings["opacity"])))
        self.menu = self.build_menu()

        self.canvas.bind("<ButtonPress-1>", self.start_drag)
        self.canvas.bind("<B1-Motion>", self.drag_window)
        self.canvas.bind("<ButtonRelease-1>", self.end_drag)
        self.canvas.bind("<Button-3>", self.show_menu)
        self.canvas.bind("<Control-MouseWheel>", self.adjust_scale_with_wheel)
        self.canvas.bind("<Shift-MouseWheel>", self.adjust_bubble_with_wheel)
        self.root.bind("<Escape>", lambda _event: self.close())
        self.root.bind("<space>", lambda _event: self.greet())
        self.root.protocol("WM_DELETE_WINDOW", self.close)

        self.place_initial_window()
        if not self.quiet_var.get():
            self.say(random.choice(self.phrases["startup"]), hold_ms=2200)
        self.animate()
        self.schedule_blink()
        self.tick_mood()
        self.schedule_idle_chatter()
        self.schedule_idle_motion()
        self.save_settings()

    def asset_path(self, filename: str) -> Path:
        skin_id = str(self.settings.get("skin_id", DEFAULT_SKIN_ID))
        skin_asset = SKINS / skin_id / "assets" / filename
        if skin_asset.exists():
            return skin_asset
        return ASSETS / filename

    def opacity_value(self, value: float) -> str:
        nearest = min(OPACITY_OPTIONS, key=lambda option: abs(option[1] - value))
        return f"{nearest[1]:.2f}"

    def build_menu(self) -> tk.Menu:
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label="打招呼", command=self.greet)
        menu.add_command(label="摸摸头", command=self.pat_head)
        menu.add_command(label="补充能量", command=self.feed)
        menu.add_command(label="休息一下", command=self.rest)
        menu.add_command(label="贴到最近侧边", command=self.snap_to_side)
        menu.add_command(label="回到右下角", command=self.reset_position)
        menu.add_command(label="显示调节...", command=self.open_display_settings)

        expression_menu = tk.Menu(menu, tearoff=0)
        expression_menu.add_command(label="自然", command=lambda: self.switch_expression("neutral"))
        expression_menu.add_command(label="开心", command=lambda: self.switch_expression("happy"))
        expression_menu.add_command(label="犯困", command=lambda: self.switch_expression("sleepy"))
        expression_menu.add_command(label="委屈", command=lambda: self.switch_expression("wronged"))
        expression_menu.add_command(label="惊讶", command=lambda: self.switch_expression("surprised"))
        menu.add_cascade(label="切换表情", menu=expression_menu)

        opacity_menu = tk.Menu(menu, tearoff=0)
        for label, value in OPACITY_OPTIONS:
            opacity_menu.add_radiobutton(
                label=label,
                variable=self.opacity_var,
                value=f"{value:.2f}",
                command=lambda value=value: self.set_opacity(value),
            )
        menu.add_cascade(label="透明度", menu=opacity_menu)

        menu.add_checkbutton(label="保持置顶", variable=self.topmost_var, command=self.toggle_topmost)
        menu.add_checkbutton(label="锁定位置", variable=self.lock_var, command=self.toggle_lock_position)
        menu.add_checkbutton(label="少说话模式", variable=self.quiet_var, command=self.toggle_quiet_mode)
        menu.add_checkbutton(label="开机自启动", variable=self.startup_var, command=self.toggle_startup)
        menu.add_separator()
        menu.add_command(label="打开台词文件", command=self.open_phrases_file)
        menu.add_command(label="打开项目文件夹", command=self.open_project_folder)
        menu.add_separator()
        menu.add_command(label="退出桌宠", command=self.close)
        return menu

    def rescale_images(self) -> None:
        self.images.clear()
        self.photos.clear()
        for name, image in self.source_images.items():
            width = max(1, int(image.width * self.scale))
            height = max(1, int(image.height * self.scale))
            resized = image.resize((width, height), Image.Resampling.LANCZOS)
            resized = window_safe_image(resized)
            self.images[name] = resized
            self.photos[name] = ImageTk.PhotoImage(resized)
        self.pet_width, self.pet_height = self.images["open"].size
        max_image_width = max(image.width for image in self.images.values())
        max_image_height = max(image.height for image in self.images.values())
        side_padding = max(82, int(165 * self.scale))
        top_padding = max(112, int(176 * self.scale))
        bottom_padding = max(76, int(86 * self.scale))
        self.width = max(300, max_image_width + side_padding, self.bubble_width + 36)
        self.height = top_padding + max_image_height + bottom_padding
        self.pet_x = self.width // 2
        self.pet_base_y = top_padding
        self.current_pet_x = self.pet_x
        self.current_pet_y = self.pet_base_y

    def place_initial_window(self) -> None:
        x = self.settings.get("x")
        y = self.settings.get("y")
        if isinstance(x, (int, float)) and isinstance(y, (int, float)):
            self.set_geometry(int(x), int(y))
        else:
            self.reset_position(silent=True)

    def set_geometry(self, x: int, y: int) -> None:
        x, y = self.clamp_position(x, y)
        self.root.geometry(f"{self.width}x{self.height}+{x}+{y}")

    def clamp_position(self, x: int, y: int) -> tuple[int, int]:
        screen_w = self.root.winfo_screenwidth()
        screen_h = self.root.winfo_screenheight()
        max_x = max(0, screen_w - 80)
        max_y = max(0, screen_h - 80)
        return max(0, min(x, max_x)), max(0, min(y, max_y))

    def default_position(self) -> tuple[int, int]:
        screen_w = self.root.winfo_screenwidth()
        screen_h = self.root.winfo_screenheight()
        return max(40, screen_w - self.width - 110), max(40, screen_h - self.height - 70)

    def reset_position(self, silent: bool = False) -> None:
        x, y = self.default_position()
        self.set_geometry(x, y)
        if not silent:
            self.say("我回到角落了。", hold_ms=1500)
            self.save_settings()

    def show_menu(self, event: tk.Event) -> None:
        try:
            self.menu.tk_popup(event.x_root, event.y_root)
        finally:
            self.menu.grab_release()

    def toggle_topmost(self) -> None:
        self.root.attributes("-topmost", bool(self.topmost_var.get()))
        if self.settings_window is not None and self.settings_window.winfo_exists():
            self.settings_window.attributes("-topmost", bool(self.topmost_var.get()))
        self.settings["topmost"] = bool(self.topmost_var.get())
        self.say(random.choice(self.phrases["settings"]), hold_ms=1500)
        self.save_settings()

    def toggle_lock_position(self) -> None:
        self.settings["lock_position"] = bool(self.lock_var.get())
        self.say(self.phrase("locked") if self.lock_var.get() else "位置解锁了。", hold_ms=1500)
        self.save_settings()

    def toggle_quiet_mode(self) -> None:
        self.settings["quiet_mode"] = bool(self.quiet_var.get())
        message = "我会少说一点。" if self.quiet_var.get() else "我恢复正常陪伴。"
        self.say(message, hold_ms=1500)
        self.save_settings()

    def snap_to_side(self) -> None:
        screen_w = self.root.winfo_screenwidth()
        x = self.root.winfo_x()
        y = self.root.winfo_y()
        left_distance = x
        right_distance = abs(screen_w - (x + self.width))
        target_x = 12 if left_distance <= right_distance else max(12, screen_w - self.width - 12)
        self.set_geometry(target_x, y)
        self.say(self.phrase("snap"), hold_ms=1500)
        self.save_settings()

    def open_phrases_file(self) -> None:
        try:
            os.startfile(PHRASES_FILE)
            self.say("台词文件打开了。", hold_ms=1400)
        except OSError:
            self.say("台词文件暂时打不开。", hold_ms=1700)

    def open_project_folder(self) -> None:
        try:
            os.startfile(ROOT)
            self.say("项目文件夹打开了。", hold_ms=1400)
        except OSError:
            self.say("项目文件夹暂时打不开。", hold_ms=1700)

    def adjust_scale_with_wheel(self, event: tk.Event) -> str:
        step = 0.018 if event.delta > 0 else -0.018
        self.set_scale(self.scale + step, announce=False)
        return "break"

    def adjust_bubble_with_wheel(self, event: tk.Event) -> str:
        step = 24 if event.delta > 0 else -24
        self.set_bubble_width(self.bubble_width + step, announce=False)
        return "break"

    def open_display_settings(self) -> None:
        if self.settings_window is not None and self.settings_window.winfo_exists():
            self.settings_window.lift()
            return

        window = tk.Toplevel(self.root)
        self.settings_window = window
        window.title("显示调节")
        window.resizable(False, False)
        window.configure(bg="#fffdf7", padx=16, pady=14)
        window.attributes("-topmost", bool(self.topmost_var.get()))
        window.geometry(f"+{self.root.winfo_x() + 36}+{self.root.winfo_y() + 48}")

        def close_window() -> None:
            self.settings_window = None
            window.destroy()

        window.protocol("WM_DELETE_WINDOW", close_window)

        tk.Label(
            window,
            text="显示调节",
            bg="#fffdf7",
            fg="#2f2b27",
            font=("Microsoft YaHei UI", 12, "bold"),
        ).grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, 10))

        def slider(
            row: int,
            title: str,
            minimum: int,
            maximum: int,
            value: int,
            suffix: str,
            command: Any,
        ) -> None:
            label_var = tk.StringVar(value=f"{value}{suffix}")
            tk.Label(
                window,
                text=title,
                bg="#fffdf7",
                fg="#2f2b27",
                font=("Microsoft YaHei UI", 10),
            ).grid(row=row, column=0, sticky="w", pady=6)
            tk.Label(
                window,
                textvariable=label_var,
                width=7,
                anchor="e",
                bg="#fffdf7",
                fg="#5c5149",
                font=("Microsoft YaHei UI", 10),
            ).grid(row=row, column=1, sticky="e", pady=6)

            def on_change(raw: str) -> None:
                current = int(float(raw))
                label_var.set(f"{current}{suffix}")
                command(current)

            scale_widget = tk.Scale(
                window,
                from_=minimum,
                to=maximum,
                orient="horizontal",
                length=310,
                showvalue=False,
                resolution=1,
                command=on_change,
                bg="#fffdf7",
                troughcolor="#e8e1da",
                highlightthickness=0,
            )
            scale_widget.grid(row=row + 1, column=0, columnspan=2, sticky="ew", pady=(0, 4))
            scale_widget.set(value)

        slider(
            1,
            "小人大小",
            int(MIN_SCALE * 100),
            int(MAX_SCALE * 100),
            int(self.scale * 100),
            "%",
            lambda value: self.set_scale(value / 100, announce=False),
        )
        slider(
            3,
            "文字泡宽度",
            BUBBLE_WIDTH_MIN,
            BUBBLE_WIDTH_MAX,
            self.bubble_width,
            "px",
            lambda value: self.set_bubble_width(value, announce=False),
        )
        slider(
            5,
            "文字字号",
            BUBBLE_FONT_MIN,
            BUBBLE_FONT_MAX,
            self.bubble_font_size,
            "px",
            lambda value: self.set_bubble_font_size(value, announce=False),
        )
        slider(7, "整体透明度", 65, 100, int(float(self.root.attributes("-alpha")) * 100), "%", lambda value: self.set_opacity(value / 100, announce=False))

        buttons = tk.Frame(window, bg="#fffdf7")
        buttons.grid(row=9, column=0, columnspan=2, sticky="ew", pady=(12, 0))
        tk.Button(buttons, text="恢复默认", command=self.reset_display_settings).pack(side="left")
        tk.Button(buttons, text="完成", command=close_window).pack(side="right")

    def reset_display_settings(self) -> None:
        self.set_scale(float(DEFAULT_SETTINGS["scale"]), announce=False)
        self.set_bubble_width(int(DEFAULT_SETTINGS["bubble_width"]), announce=False)
        self.set_bubble_font_size(int(DEFAULT_SETTINGS["bubble_font_size"]), announce=False)
        self.set_opacity(float(DEFAULT_SETTINGS["opacity"]), announce=False)
        self.say("显示设置已恢复默认。", hold_ms=1600)

    def set_scale(self, value: float, announce: bool = True) -> None:
        x, y = self.root.winfo_x(), self.root.winfo_y()
        self.scale = clamp(value, MIN_SCALE, MAX_SCALE)
        self.settings["scale"] = self.scale
        self.rescale_images()
        self.canvas.config(width=self.width, height=self.height)
        self.set_geometry(x, y)
        if announce:
            self.say("大小已调整。", hold_ms=1400)
        self.save_settings()

    def set_bubble_width(self, value: int, announce: bool = True) -> None:
        x, y = self.root.winfo_x(), self.root.winfo_y()
        self.bubble_width = int(clamp(value, BUBBLE_WIDTH_MIN, BUBBLE_WIDTH_MAX))
        self.settings["bubble_width"] = self.bubble_width
        side_padding = max(82, int(165 * self.scale))
        self.width = max(300, self.pet_width + side_padding, self.bubble_width + 30)
        self.pet_x = self.width // 2
        self.canvas.config(width=self.width, height=self.height)
        self.set_geometry(x, y)
        if announce:
            self.say("文字泡宽度已调整。", hold_ms=1400)
        self.save_settings()

    def set_bubble_font_size(self, value: int, announce: bool = True) -> None:
        self.bubble_font_size = int(clamp(value, BUBBLE_FONT_MIN, BUBBLE_FONT_MAX))
        self.settings["bubble_font_size"] = self.bubble_font_size
        if announce:
            self.say("文字字号已调整。", hold_ms=1400)
        self.save_settings()

    def set_opacity(self, value: float, announce: bool = True) -> None:
        value = clamp(value, 0.65, 1.0)
        self.root.attributes("-alpha", value)
        self.settings["opacity"] = value
        self.opacity_var.set(self.opacity_value(value))
        if announce:
            self.say("透明度已调整。", hold_ms=1400)
        self.save_settings()

    def toggle_startup(self) -> None:
        enabled = bool(self.startup_var.get())
        if enabled:
            self.enable_startup()
            self.say("已开启开机自启动。", hold_ms=1800)
        else:
            self.disable_startup()
            self.say("已关闭开机自启动。", hold_ms=1800)
        self.settings["startup"] = enabled
        self.save_settings()

    def enable_startup(self) -> None:
        self.startup_path.parent.mkdir(parents=True, exist_ok=True)
        command = f'"{pythonw_path()}" "{ROOT / "pet.py"}"'
        escaped = command.replace('"', '""')
        content = f'Set shell = CreateObject("WScript.Shell")\nshell.Run "{escaped}", 0, False\n'
        self.startup_path.write_text(content, encoding="utf-8")

    def disable_startup(self) -> None:
        if self.startup_path.exists():
            self.startup_path.unlink()

    def mood_name(self) -> str:
        if self.wronged_frames > 0:
            return "wronged"
        if self.mood >= 78:
            return "happy"
        if self.mood <= 36:
            return "sleepy"
        return "calm"

    def mood_label(self) -> str:
        return {"happy": "开心", "calm": "稳定", "sleepy": "犯困", "wronged": "委屈"}[self.mood_name()]

    def expression_for_mood(self) -> str:
        return {"happy": "happy", "calm": "neutral", "sleepy": "sleepy", "wronged": "wronged"}[self.mood_name()]

    def phrase(self, key: str) -> str:
        return random.choice(self.phrases.get(key, DEFAULT_PHRASES[key]))

    def start_drag(self, event: tk.Event) -> None:
        self.drag = DragState(
            start_x=event.x_root,
            start_y=event.y_root,
            window_x=self.root.winfo_x(),
            window_y=self.root.winfo_y(),
            last_x=event.x_root,
            last_y=event.y_root,
            direction=self.drag.direction,
        )
        self.drag_hint_shown = False

    def drag_window(self, event: tk.Event) -> None:
        dx = event.x_root - self.drag.start_x
        dy = event.y_root - self.drag.start_y
        step_dx = event.x_root - self.drag.last_x
        if abs(dx) + abs(dy) > 4:
            self.drag.moved = True
        if self.lock_var.get():
            if self.drag.moved and not self.drag_hint_shown:
                self.drag_hint_shown = True
                self.say(self.phrase("locked"), hold_ms=1100)
            return
        if self.drag.moved:
            self.is_dragging = True
            if abs(step_dx) >= 2:
                self.drag.direction = "right" if step_dx > 0 else "left"
            elif abs(dx) >= 10:
                self.drag.direction = "right" if dx > 0 else "left"
        if self.drag.moved and not self.drag_hint_shown:
            self.drag_hint_shown = True
            self.say(self.phrase("drag"), hold_ms=1200)
        self.root.geometry(f"+{self.drag.window_x + dx}+{self.drag.window_y + dy}")
        self.drag.last_x = event.x_root
        self.drag.last_y = event.y_root

    def end_drag(self, event: tk.Event) -> None:
        if self.drag.moved:
            self.is_dragging = False
            if self.lock_var.get():
                self.spawn_particles("poke")
                return
            self.mood = min(100, self.mood + 2)
            self.show_mood_card()
            self.say(self.phrase("drag"), hold_ms=1700)
            self.spawn_particles("move")
            self.save_settings()
            return

        region = self.hit_region(event.x, event.y)
        if region == "head":
            self.pat_head()
        elif region == "body":
            self.poke()
        else:
            self.greet(quiet=True)

    def hit_region(self, x: int, y: int) -> str | None:
        rel_x = int(x - (self.current_pet_x - self.pet_width // 2))
        rel_y = int(y - self.current_pet_y)
        if not (0 <= rel_x < self.pet_width and 0 <= rel_y < self.pet_height):
            return None
        alpha = self.images["open"].getpixel((rel_x, rel_y))[3]
        if alpha < 32:
            return None
        if rel_y < int(self.pet_height * 0.53):
            return "head"
        return "body"

    def greet(self, quiet: bool = False) -> None:
        if not quiet:
            self.mood = min(100, self.mood + 3)
        self.show_mood_card()
        self.set_expression(self.expression_for_mood(), 80)
        self.say(self.phrase(self.mood_name()), hold_ms=2300)

    def pat_head(self) -> None:
        self.poke_combo = 0
        self.wronged_frames = max(0, self.wronged_frames - 80)
        self.mood = min(100, self.mood + 12)
        self.set_reaction(20)
        self.show_mood_card()
        self.set_expression("happy", 96)
        self.say(self.phrase("pat"), hold_ms=2300)
        self.spawn_particles("pat")

    def feed(self) -> None:
        self.poke_combo = 0
        self.wronged_frames = max(0, self.wronged_frames - 140)
        self.mood = min(100, self.mood + 22)
        self.idle_action = "bounce"
        self.idle_frames = 54
        self.idle_total = 54
        self.set_reaction(18)
        self.show_mood_card()
        self.set_expression("happy", 110)
        self.say(self.phrase("feed"), hold_ms=2200)
        self.spawn_particles("feed")
        self.save_settings()

    def poke(self) -> None:
        self.poke_combo += 1
        if self.poke_reset_after:
            self.root.after_cancel(self.poke_reset_after)
        self.poke_reset_after = self.root.after(4800, self.reset_poke_combo)

        if self.poke_combo >= 3:
            self.poke_combo = 0
            self.wronged_frames = 210
            self.idle_action = "sulk"
            self.idle_frames = 90
            self.idle_total = 90
            self.mood = max(5, self.mood - 16)
            self.show_mood_card()
            self.set_expression("wronged", 120)
            self.say(self.phrase("wronged"), hold_ms=2600)
            self.spawn_particles("wronged")
        else:
            self.mood = max(0, self.mood - 4)
            self.show_mood_card()
            self.set_expression("surprised", 58)
            self.say(self.phrase("poke"), hold_ms=1900)
            self.spawn_particles("poke")
        self.set_reaction(12)

    def reset_poke_combo(self) -> None:
        self.poke_combo = 0
        self.poke_reset_after = None

    def rest(self) -> None:
        self.mood = max(24, self.mood - 14)
        self.idle_action = "doze"
        self.idle_frames = 130
        self.idle_total = 130
        self.show_mood_card()
        self.set_expression("sleepy", 150)
        self.say(self.phrase("rest"), hold_ms=2200)
        self.blink_sequence = ["blink25", "half", "blink75", "closed", "closed", "blink75", "half", "blink25", "open"]
        self.advance_blink()

    def switch_expression(self, name: str) -> None:
        labels = {"neutral": "自然", "open": "自然", "happy": "开心", "sleepy": "犯困", "wronged": "委屈", "surprised": "惊讶"}
        self.set_expression(name, 170)
        self.show_mood_card()
        self.say(f"切到{labels.get(name, '自然')}表情。", hold_ms=1400)

    def set_expression(self, name: str, frames: int) -> None:
        if name == "open":
            name = "neutral"
        if name not in EXPRESSION_NAMES:
            name = "neutral"
        self.expression_frame = name
        self.expression_frames = max(0, frames)

    def show_mood_card(self) -> None:
        self.mood_card_frames = MOOD_CARD_HOLD_FRAMES + MOOD_CARD_FADE_FRAMES

    def set_reaction(self, frames: int) -> None:
        self.reaction_frames = frames
        self.reaction_total = max(1, frames)

    def say(self, text: str, hold_ms: int = 2400) -> None:
        if self.bubble_after:
            self.root.after_cancel(self.bubble_after)
        self.speech = text
        self.bubble_after = self.root.after(hold_ms, self.clear_speech)

    def clear_speech(self) -> None:
        self.speech = ""
        self.bubble_after = None

    def schedule_blink(self) -> None:
        delay = random.randint(3200, 7600)
        if self.mood_name() == "sleepy":
            delay = random.randint(1800, 4200)
        self.root.after(delay, self.start_blink)

    def start_blink(self) -> None:
        if self.is_dragging:
            self.schedule_blink()
            return
        if self.mood_name() == "sleepy":
            self.blink_sequence = ["blink25", "half", "blink75", "closed", "closed", "blink75", "half", "blink25", "open"]
        else:
            self.blink_sequence = ["blink25", "half", "blink75", "closed", "blink75", "half", "blink25", "open"]
        self.advance_blink()

    def advance_blink(self) -> None:
        if not self.blink_sequence:
            self.blink_frame = "open"
            self.schedule_blink()
            return
        self.blink_frame = self.blink_sequence.pop(0)
        hold = 72 if self.blink_frame == "closed" else 34
        self.root.after(hold, self.advance_blink)

    def tick_mood(self) -> None:
        if self.mood_name() == "happy":
            self.mood = max(18, self.mood - 2)
        else:
            self.mood = max(18, self.mood - 1)
        if self.mood in (36, 68):
            self.say(self.phrase(self.mood_name()), hold_ms=1800)
        self.root.after(9000, self.tick_mood)

    def schedule_idle_chatter(self) -> None:
        if self.quiet_var.get():
            delay = random.randint(65000, 125000)
        else:
            delay = random.randint(24000, 48000)
        self.root.after(delay, self.idle_chatter)

    def idle_chatter(self) -> None:
        if self.quiet_var.get() and random.random() < 0.72:
            self.schedule_idle_chatter()
            return
        if not self.speech and not self.is_dragging:
            self.say(self.phrase(self.mood_name()), hold_ms=2100)
        self.schedule_idle_chatter()

    def schedule_idle_motion(self) -> None:
        delay = random.randint(6500, 12500)
        self.root.after(delay, self.start_idle_motion)

    def start_idle_motion(self) -> None:
        if self.is_dragging or self.idle_frames > 0:
            self.schedule_idle_motion()
            return
        mood = self.mood_name()
        if mood == "sleepy":
            action = "doze"
            frames = random.randint(90, 145)
        elif mood == "wronged":
            action = "sulk"
            frames = random.randint(70, 105)
        elif mood == "happy":
            action = random.choice(["look", "stretch", "bounce"])
            frames = random.randint(46, 76)
        else:
            action = random.choice(["look", "stretch"])
            frames = random.randint(50, 90)
        self.idle_action = action
        self.idle_frames = frames
        self.idle_total = frames
        self.idle_direction = random.choice([-1, 1])
        self.schedule_idle_motion()

    def animate(self) -> None:
        self.tick += 1
        if self.reaction_frames:
            self.reaction_frames -= 1
        if self.expression_frames:
            self.expression_frames -= 1
            if self.expression_frames <= 0:
                self.expression_frame = "open"
        if self.mood_card_frames:
            self.mood_card_frames -= 1
        if self.wronged_frames:
            self.wronged_frames -= 1
        if self.idle_frames:
            self.idle_frames -= 1
            if self.idle_frames <= 0:
                self.idle_action = "idle"
        self.update_particles()
        self.render()
        self.root.after(50, self.animate)

    def bob_offset(self) -> int:
        if self.is_dragging:
            return 0
        base = 3 if self.mood_name() == "sleepy" else 5
        return int(math.sin(self.tick / 11) * base)

    def run_bob_offset(self) -> int:
        if not self.is_dragging:
            return 0
        amplitude = max(1, int(6 * self.scale))
        return -int(abs(math.sin(self.tick / 4.0)) * amplitude)

    def action_offsets(self) -> tuple[int, int]:
        if not self.idle_frames:
            return 0, 0
        progress = 1 - self.idle_frames / max(1, self.idle_total)
        wave = math.sin(progress * math.pi)
        if self.idle_action == "look":
            return int(self.idle_direction * wave * 12), 0
        if self.idle_action == "stretch":
            return 0, -int(wave * 16)
        if self.idle_action == "bounce":
            return 0, -int(abs(math.sin(progress * math.pi * 2)) * 14)
        if self.idle_action == "doze":
            return 0, int(wave * 12)
        if self.idle_action == "sulk":
            return -int(self.idle_direction * 5), int(wave * 8) + 4
        return 0, 0

    def reaction_offset(self) -> int:
        if not self.reaction_frames:
            return 0
        progress = 1 - self.reaction_frames / max(1, self.reaction_total)
        return -int(math.sin(progress * math.pi) * 12)

    def expression_key(self, name: str, speed: int = 7) -> str:
        if name == "open":
            name = "neutral"
        if name not in EXPRESSION_NAMES:
            name = "neutral"
        frame = EXPRESSION_LOOP[(self.tick // max(1, speed)) % len(EXPRESSION_LOOP)]
        return f"{name}_{frame}"

    def stable_expression_key(self, name: str) -> str:
        if name == "open" or name == "neutral":
            return "open"
        if name in {"happy", "sleepy", "wronged", "surprised"}:
            return name
        return "open"

    def run_frame_key(self) -> str:
        direction = self.drag.direction if self.drag.direction in {"left", "right"} else "right"
        frame = RUN_LOOP[(self.tick // 2) % len(RUN_LOOP)]
        return f"run_{direction}_{frame}"

    def current_frame(self) -> str:
        if self.is_dragging:
            return self.run_frame_key()
        if self.blink_frame != "open":
            return self.blink_frame
        if self.expression_frames > 0:
            return self.expression_key(self.expression_frame, speed=5)
        if self.idle_action == "doze":
            return "closed" if self.idle_frames % 82 > 46 else self.expression_key("sleepy", speed=10)
        mood = self.mood_name()
        if mood == "wronged" and self.tick % 150 > 112:
            return self.expression_key("wronged", speed=9)
        if mood == "sleepy" and self.tick % 150 > 112:
            return self.expression_key("sleepy", speed=10)
        if mood == "happy" and self.tick % 140 < 34:
            return self.expression_key("happy", speed=6)
        return "open"

    def render(self) -> None:
        self.canvas.delete("all")

        action_x, action_y = self.action_offsets()
        self.current_pet_x = self.pet_x + action_x
        self.current_pet_y = self.pet_base_y + self.bob_offset() + self.run_bob_offset() + self.reaction_offset() + action_y
        image = self.photos[self.current_frame()]

        self.draw_shadow(self.current_pet_y)
        if self.is_dragging:
            self.draw_run_effects()
        self.canvas.create_image(self.current_pet_x, self.current_pet_y, image=image, anchor="n")
        self.draw_mood_marks()
        self.render_particles()
        self.bubble_bottom = 0
        if self.speech:
            self.bubble_bottom = self.draw_bubble(self.speech)
        self.draw_mood_card()

    def draw_shadow(self, pet_y: int) -> None:
        x = self.current_pet_x
        y = pet_y + self.pet_height + max(1, int(5 * self.scale))
        base_width = max(18, int(self.pet_width * 0.27))
        base_height = max(3, int(self.pet_height * 0.012))
        pulse = max(1, int(8 * self.scale)) if self.reaction_frames else 0
        shrink = max(1, int(12 * self.scale)) if self.idle_action == "stretch" else 0
        self.canvas.create_oval(
            x - base_width - pulse + shrink,
            y - base_height,
            x + base_width + pulse - shrink,
            y + base_height * 2,
            fill="#5d5149",
            outline="",
            stipple="gray50",
        )

    def draw_run_effects(self) -> None:
        direction = self.drag.direction if self.drag.direction in {"left", "right"} else "right"
        sign = -1 if direction == "right" else 1
        behind_x = self.current_pet_x + sign * self.pet_width * 0.33
        line_color = "#d7cfc6"
        dust_color = "#c9bfb4"
        phase = self.tick % 9
        for index in range(3):
            length = max(8, int((24 - index * 5 + phase) * self.scale))
            y = self.current_pet_y + self.pet_height * (0.43 + index * 0.095)
            x1 = int(behind_x + sign * index * 12 * self.scale)
            x2 = int(x1 + sign * length)
            self.canvas.create_line(x1, int(y), x2, int(y - 2 * self.scale), fill=line_color, width=max(1, int(3 * self.scale)))

        foot_y = self.current_pet_y + self.pet_height * 0.88
        for index in range(2):
            radius = max(2, int((5 + index * 2) * self.scale))
            x = int(self.current_pet_x + sign * self.pet_width * (0.17 + index * 0.08))
            y = int(foot_y + index * 5 * self.scale)
            self.canvas.create_oval(x - radius, y - radius // 2, x + radius, y + radius // 2, outline=dust_color, width=1)

    def draw_mood_marks(self) -> None:
        mood = self.mood_name()
        if mood == "wronged":
            self.canvas.create_text(
                self.current_pet_x + self.pet_width * 0.28,
                self.current_pet_y + self.pet_height * 0.31,
                text="...",
                fill="#7d8da8",
                font=("Microsoft YaHei UI", max(12, int(17 * self.scale)), "bold"),
            )
        elif mood == "happy" and self.tick % 60 < 28:
            self.canvas.create_text(
                self.current_pet_x + self.pet_width * 0.36,
                self.current_pet_y + self.pet_height * 0.24,
                text="+",
                fill="#ff86a7",
                font=("Arial", max(14, int(22 * self.scale)), "bold"),
            )

    def mood_card_alpha(self) -> float:
        if self.mood_card_frames <= 0:
            return 0.0
        if self.mood_card_frames > MOOD_CARD_FADE_FRAMES:
            return 1.0
        return clamp(self.mood_card_frames / MOOD_CARD_FADE_FRAMES, 0.0, 1.0)

    def fade_stipple(self, alpha: float) -> str | None:
        if alpha >= 0.82:
            return None
        if alpha >= 0.56:
            return "gray75"
        if alpha >= 0.30:
            return "gray50"
        return "gray25"

    def blend_hex(self, foreground: str, background: str, amount: float) -> str:
        amount = clamp(amount, 0, 1)
        fg = foreground.lstrip("#")
        bg = background.lstrip("#")
        fr, fg_g, fb = int(fg[0:2], 16), int(fg[2:4], 16), int(fg[4:6], 16)
        br, bg_g, bb = int(bg[0:2], 16), int(bg[2:4], 16), int(bg[4:6], 16)
        r = int(fr + (br - fr) * amount)
        g = int(fg_g + (bg_g - fg_g) * amount)
        b = int(fb + (bb - fb) * amount)
        return f"#{r:02x}{g:02x}{b:02x}"

    def draw_mood_card(self) -> None:
        alpha = self.mood_card_alpha()
        if alpha <= 0:
            return

        mood = self.mood_name()
        colors = {"happy": "#ff86a7", "calm": "#74b9ff", "sleepy": "#a99ee0", "wronged": "#8fa4c9"}
        ui_scale = clamp(self.scale / 0.32, 0.55, 1.18)
        card_width = max(112, int(132 * ui_scale))
        card_height = max(30, int(34 * ui_scale))
        radius = max(9, int(12 * ui_scale))
        font_size = max(8, int(9 * ui_scale))

        y1 = max(12, getattr(self, "bubble_bottom", 0) + 6) if self.speech else 12
        x1 = 14
        x2 = x1 + card_width
        y2 = y1 + card_height
        fade = 1 - alpha
        stipple = self.fade_stipple(alpha)
        bg = "#fffef9"

        card_style: dict[str, Any] = {
            "fill": bg,
            "outline": self.blend_hex("#2e2a26", bg, fade),
            "width": 1,
        }
        if stipple:
            card_style["stipple"] = stipple
        self.rounded_rect(x1, y1, x2, y2, radius, **card_style)

        text_style: dict[str, Any] = {
            "text": self.mood_label(),
            "fill": self.blend_hex("#2b2826", bg, fade),
            "font": ("Microsoft YaHei UI", font_size, "bold"),
            "anchor": "w",
        }
        if stipple:
            text_style["stipple"] = stipple
        self.canvas.create_text(x1 + 12, y1 + card_height // 2, **text_style)

        bar_x1 = x1 + max(56, int(62 * ui_scale))
        bar_y1 = y1 + card_height // 2 - max(3, int(4 * ui_scale))
        bar_x2 = x2 - 10
        bar_y2 = bar_y1 + max(6, int(8 * ui_scale))
        track_style: dict[str, Any] = {"fill": self.blend_hex("#e8e1da", bg, fade), "outline": ""}
        fill_style: dict[str, Any] = {"fill": self.blend_hex(colors[mood], bg, fade), "outline": ""}
        if stipple:
            track_style["stipple"] = stipple
            fill_style["stipple"] = stipple
        self.rounded_rect(bar_x1, bar_y1, bar_x2, bar_y2, 4, **track_style)
        fill = bar_x1 + int((bar_x2 - bar_x1) * (self.mood / 100))
        self.rounded_rect(bar_x1, bar_y1, max(bar_x1 + 3, fill), bar_y2, 4, **fill_style)

    def draw_bubble(self, text: str) -> int:
        bubble_width = min(self.bubble_width, self.width - 30)
        font_size = self.bubble_font_size
        chars_per_line = max(7, int((bubble_width - 28) / max(8, font_size)))
        line_count = max(1, math.ceil(len(text) / chars_per_line))
        bubble_height = max(46, 24 + line_count * (font_size + 7))

        x1 = int(clamp(self.current_pet_x - bubble_width / 2, 14, self.width - bubble_width - 14))
        y1 = 12
        x2 = x1 + bubble_width
        y2 = y1 + bubble_height
        self.rounded_rect(x1 + 2, y1 + 2, x2 + 2, y2 + 2, 14, fill="#6d625a", outline="", stipple="gray50")
        self.rounded_rect(x1, y1, x2, y2, 14, fill="#fffdf7", outline="#2f2b27", width=2)
        tail_x = int(clamp(self.current_pet_x - 42, x1 + 24, x2 - 38))
        self.canvas.create_polygon(
            tail_x,
            y2 - 2,
            tail_x + 15,
            y2 + 14,
            tail_x + 24,
            y2 - 3,
            fill="#fffdf7",
            outline="#2f2b27",
            width=2,
        )
        self.canvas.create_text(
            (x1 + x2) // 2,
            (y1 + y2) // 2,
            text=text,
            fill="#2f2b27",
            width=x2 - x1 - 24,
            font=("Microsoft YaHei UI", font_size, "bold"),
        )
        return y2 + 14

    def spawn_particles(self, kind: str) -> None:
        def add(
            x: float,
            y: float,
            vx: float,
            vy: float,
            life: int,
            particle_kind: str,
            color: str,
            size: int,
        ) -> None:
            self.particles.append(
                Particle(
                    x=x,
                    y=y,
                    vx=vx,
                    vy=vy,
                    life=life,
                    max_life=life,
                    kind=particle_kind,
                    color=color,
                    size=size,
                )
            )

        head_y = self.current_pet_y + self.pet_height * 0.28
        body_y = self.current_pet_y + self.pet_height * 0.50
        foot_y = self.current_pet_y + self.pet_height * 0.70

        if kind == "pat":
            palette = ["#ff8faf", "#ffd1dc", "#ffffff"]
            for _ in range(3):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.20, self.pet_width * 0.20),
                    head_y + random.uniform(-18, 18),
                    random.uniform(-0.25, 0.25),
                    random.uniform(-0.55, -0.20),
                    random.randint(22, 30),
                    "ring",
                    random.choice(palette),
                    random.randint(10, 16),
                )
            for _ in range(7):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.28, self.pet_width * 0.28),
                    head_y + random.uniform(-28, 8),
                    random.uniform(-0.55, 0.55),
                    random.uniform(-1.35, -0.55),
                    random.randint(18, 28),
                    random.choice(["glint", "dot"]),
                    random.choice(palette),
                    random.randint(4, 8),
                )
        elif kind == "move":
            palette = ["#8fc9ff", "#cfe9ff", "#ffffff"]
            for _ in range(10):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.32, self.pet_width * 0.32),
                    foot_y + random.uniform(-8, 22),
                    random.uniform(-1.1, 1.1),
                    random.uniform(-0.65, 0.15),
                    random.randint(14, 24),
                    "dot",
                    random.choice(palette),
                    random.randint(3, 7),
                )
        elif kind == "wronged":
            palette = ["#9fb3d8", "#c9d3ea"]
            for _ in range(5):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.18, self.pet_width * 0.18),
                    body_y + random.uniform(-18, 12),
                    random.uniform(-0.28, 0.28),
                    random.uniform(0.15, 0.55),
                    random.randint(28, 42),
                    "wisp",
                    random.choice(palette),
                    random.randint(5, 9),
                )
        else:
            palette = ["#ffc857", "#ffe6a7", "#ffffff"]
            for _ in range(2):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.16, self.pet_width * 0.16),
                    body_y + random.uniform(-12, 12),
                    random.uniform(-0.25, 0.25),
                    random.uniform(-0.25, 0.10),
                    random.randint(16, 22),
                    "ring",
                    random.choice(palette),
                    random.randint(9, 14),
                )
            for _ in range(6):
                add(
                    self.current_pet_x + random.uniform(-self.pet_width * 0.22, self.pet_width * 0.22),
                    body_y + random.uniform(-18, 16),
                    random.uniform(-0.65, 0.65),
                    random.uniform(-1.1, -0.25),
                    random.randint(16, 24),
                    random.choice(["glint", "dot"]),
                    random.choice(palette),
                    random.randint(4, 7),
                )

    def update_particles(self) -> None:
        kept: list[Particle] = []
        for particle in self.particles:
            particle.x += particle.vx
            particle.y += particle.vy
            if particle.kind != "wisp":
                particle.vy += 0.035
            particle.vx *= 0.985
            particle.life -= 1
            if particle.life > 0:
                kept.append(particle)
        self.particles = kept

    def render_particles(self) -> None:
        for particle in self.particles:
            progress = 1 - particle.life / max(1, particle.max_life)
            color = self.fade_color(particle.color, progress)
            x = int(particle.x)
            y = int(particle.y)

            if particle.kind == "ring":
                radius = int(particle.size * (0.65 + progress * 1.55))
                width = 2 if progress < 0.58 else 1
                self.canvas.create_oval(x - radius, y - radius, x + radius, y + radius, outline=color, width=width)
            elif particle.kind == "glint":
                radius = int(particle.size * (1.05 - progress * 0.28))
                self.canvas.create_line(x - radius, y, x + radius, y, fill=color, width=2)
                self.canvas.create_line(x, y - radius, x, y + radius, fill=color, width=2)
            elif particle.kind == "wisp":
                radius = max(2, int(particle.size * (1 - progress * 0.45)))
                self.canvas.create_oval(x - radius, y - radius // 2, x + radius, y + radius // 2, outline=color, width=1)
            else:
                radius = max(2, int(particle.size * (1 - progress * 0.35)))
                self.canvas.create_oval(x - radius, y - radius, x + radius, y + radius, fill=color, outline="")

    def fade_color(self, color: str, progress: float) -> str:
        color = color.lstrip("#")
        r = int(color[0:2], 16)
        g = int(color[2:4], 16)
        b = int(color[4:6], 16)
        blend = clamp(progress * 0.58, 0, 1)
        r = int(r + (245 - r) * blend)
        g = int(g + (245 - g) * blend)
        b = int(b + (245 - b) * blend)
        return f"#{r:02x}{g:02x}{b:02x}"

    def rounded_rect(self, x1: int, y1: int, x2: int, y2: int, radius: int, **kwargs: Any) -> None:
        points = [
            x1 + radius,
            y1,
            x2 - radius,
            y1,
            x2,
            y1,
            x2,
            y1 + radius,
            x2,
            y2 - radius,
            x2,
            y2,
            x2 - radius,
            y2,
            x1 + radius,
            y2,
            x1,
            y2,
            x1,
            y2 - radius,
            x1,
            y1 + radius,
            x1,
            y1,
        ]
        self.canvas.create_polygon(points, smooth=True, **kwargs)

    def save_settings(self) -> None:
        self.settings.update(
            {
                "x": self.root.winfo_x(),
                "y": self.root.winfo_y(),
                "scale": self.scale,
                "opacity": float(self.root.attributes("-alpha")),
                "topmost": bool(self.topmost_var.get()),
                "mood": self.mood,
                "startup": self.startup_path.exists(),
                "quiet_mode": bool(self.quiet_var.get()),
                "lock_position": bool(self.lock_var.get()),
                "skin_id": str(self.settings.get("skin_id", DEFAULT_SKIN_ID)),
            }
        )
        write_json(SETTINGS_FILE, self.settings)

    def close(self) -> None:
        self.save_settings()
        self.root.destroy()

    def run(self) -> None:
        self.root.mainloop()


def self_test() -> int:
    core_required = (
        PET_IMAGE,
        PET_BLINK_25_IMAGE,
        PET_HALF_BLINK_IMAGE,
        PET_BLINK_75_IMAGE,
        PET_BLINK_IMAGE,
        PET_HAPPY_IMAGE,
        PET_SLEEPY_IMAGE,
        PET_WRONGED_IMAGE,
        PET_SURPRISED_IMAGE,
    )
    run_required = (
        *RUN_RIGHT_IMAGES,
        *RUN_LEFT_IMAGES,
    )
    expression_required = tuple(path for paths in EXPRESSION_FRAME_PATHS.values() for path in paths)
    required = (*core_required, *run_required, *expression_required)
    missing = [path for path in required if not path.exists()]
    if missing:
        print("Missing assets:")
        for path in missing:
            print(f"  {path}")
        return 1

    sizes: dict[Path, tuple[int, int]] = {}
    for path in required:
        image = Image.open(path)
        sizes[path] = image.size
        if image.mode != "RGBA":
            print(f"{path.name} is not RGBA")
            return 1
        if image.height < 1080:
            print(f"{path.name} is not high-resolution enough: {image.size}")
            return 1
        if not image.getchannel("A").getbbox():
            print(f"{path.name} has no visible alpha content")
            return 1
        safe = window_safe_image(image.resize((max(1, image.width // 2), max(1, image.height // 2)), Image.Resampling.LANCZOS))
        alpha_histogram = safe.getchannel("A").histogram()
        semi = sum(alpha_histogram[1:255]) > 0
        if semi:
            print(f"{path.name} still has semi-transparent display pixels")
            return 1
    core_sizes = {sizes[path] for path in core_required}
    if len(core_sizes) != 1:
        print(f"Core frames have mismatched sizes: {core_sizes}")
        return 1

    open_bbox = Image.open(PET_IMAGE).getchannel("A").getbbox()
    for path in expression_required:
        expression_bbox = Image.open(path).getchannel("A").getbbox()
        if expression_bbox != open_bbox:
            print(f"{path.name} does not match static pet bbox: {expression_bbox} != {open_bbox}")
            return 1

    run_sizes = {sizes[path] for path in run_required}
    if len(run_sizes) != 1:
        print(f"Run frames have mismatched sizes: {run_sizes}")
        return 1

    phrases = load_phrases()
    if "wronged" not in phrases or not phrases["wronged"]:
        print("phrases.json is missing the wronged mood lines")
        return 1
    settings = load_settings()
    if not (MIN_SCALE <= float(settings["scale"]) <= MAX_SCALE):
        print("settings.json has invalid scale")
        return 1
    if not (BUBBLE_WIDTH_MIN <= int(settings["bubble_width"]) <= BUBBLE_WIDTH_MAX):
        print("settings.json has invalid bubble_width")
        return 1
    if not (BUBBLE_FONT_MIN <= int(settings["bubble_font_size"]) <= BUBBLE_FONT_MAX):
        print("settings.json has invalid bubble_font_size")
        return 1
    skin_assets = SKINS / str(settings.get("skin_id", DEFAULT_SKIN_ID)) / "assets"
    if skin_assets.exists():
        missing_skin_assets = [path.name for path in required if not (skin_assets / path.name).exists()]
        if missing_skin_assets:
            print(f"Selected skin is missing assets: {missing_skin_assets}")
            return 1

    print("Desktop pet self-test passed.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the transparent desktop pet.")
    parser.add_argument("--self-test", action="store_true", help="validate assets without opening the desktop window")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    DesktopPet().run()
    return 0


if __name__ == "__main__":
    sys.exit(main())
