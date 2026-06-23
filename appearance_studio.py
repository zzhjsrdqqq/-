from __future__ import annotations

import argparse
import json
import mimetypes
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlparse


ROOT = Path(__file__).resolve().parent
SKINS = ROOT / "skins"
SETTINGS_FILE = ROOT / "settings.json"
WEB_ROOT = ROOT / "appearance_studio"
DEFAULT_SKIN_ID = "current"

REQUIRED_ASSETS = (
    "pet.png",
    "pet-blink-25.png",
    "pet-blink-half.png",
    "pet-blink-75.png",
    "pet-blink.png",
    "pet-happy.png",
    "pet-sleepy.png",
    "pet-wronged.png",
    "pet-surprised.png",
    "pet-neutral-1.png",
    "pet-neutral-2.png",
    "pet-neutral-3.png",
    "pet-happy-1.png",
    "pet-happy-2.png",
    "pet-happy-3.png",
    "pet-sleepy-1.png",
    "pet-sleepy-2.png",
    "pet-sleepy-3.png",
    "pet-wronged-1.png",
    "pet-wronged-2.png",
    "pet-wronged-3.png",
    "pet-surprised-1.png",
    "pet-surprised-2.png",
    "pet-surprised-3.png",
    "pet-run-right-1.png",
    "pet-run-right-2.png",
    "pet-run-right-3.png",
    "pet-run-left-1.png",
    "pet-run-left-2.png",
    "pet-run-left-3.png",
)


def read_json(path: Path, fallback: dict[str, Any]) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return dict(fallback)
    return data if isinstance(data, dict) else dict(fallback)


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def current_skin_id() -> str:
    settings = read_json(SETTINGS_FILE, {})
    skin_id = str(settings.get("skin_id", DEFAULT_SKIN_ID)).strip() or DEFAULT_SKIN_ID
    return skin_id if (SKINS / skin_id).exists() else DEFAULT_SKIN_ID


def safe_skin_id(value: str) -> str:
    skin_id = value.strip()
    if not skin_id or any(char in skin_id for char in "\\/.:*?\"<>|"):
        raise ValueError("Invalid skin id")
    if not (SKINS / skin_id / "manifest.json").exists():
        raise ValueError("Skin does not exist")
    return skin_id


def safe_asset_name(value: str) -> str:
    filename = unquote(value).strip()
    if not filename or Path(filename).name != filename:
        raise ValueError("Invalid asset name")
    return filename


def skin_manifest(skin_dir: Path) -> dict[str, Any]:
    manifest = read_json(skin_dir / "manifest.json", {})
    skin_id = skin_dir.name
    manifest.setdefault("id", skin_id)
    manifest.setdefault("name", skin_id)
    manifest.setdefault("description", "")
    manifest.setdefault("tags", [])
    manifest.setdefault("preview", "pet.png")
    manifest.setdefault("accent", "#5f7f63")
    return manifest


def skin_payload(skin_dir: Path, selected_id: str) -> dict[str, Any]:
    manifest = skin_manifest(skin_dir)
    skin_id = str(manifest["id"])
    assets_dir = skin_dir / "assets"
    missing = [filename for filename in REQUIRED_ASSETS if not (assets_dir / filename).exists()]

    def asset_url(filename: str) -> str:
        return f"/api/skins/{skin_id}/asset/{filename}"

    return {
        "id": skin_id,
        "name": manifest["name"],
        "description": manifest["description"],
        "tags": manifest["tags"],
        "accent": manifest["accent"],
        "selected": skin_id == selected_id,
        "ready": not missing,
        "missing": missing,
        "preview": asset_url(str(manifest.get("preview", "pet.png"))),
        "assets": {
            "idle": [asset_url("pet.png")],
            "happy": [asset_url(f"pet-happy-{index}.png") for index in range(1, 4)],
            "sleepy": [asset_url(f"pet-sleepy-{index}.png") for index in range(1, 4)],
            "wronged": [asset_url(f"pet-wronged-{index}.png") for index in range(1, 4)],
            "surprised": [asset_url(f"pet-surprised-{index}.png") for index in range(1, 4)],
            "blink": [
                asset_url("pet.png"),
                asset_url("pet-blink-25.png"),
                asset_url("pet-blink-half.png"),
                asset_url("pet-blink-75.png"),
                asset_url("pet-blink.png"),
                asset_url("pet-blink-75.png"),
                asset_url("pet-blink-half.png"),
                asset_url("pet-blink-25.png"),
            ],
            "run": [asset_url(f"pet-run-right-{index}.png") for index in range(1, 4)],
        },
    }


class AppearanceHandler(BaseHTTPRequestHandler):
    server_version = "DesktopPetAppearanceStudio/1.0"

    def log_message(self, format: str, *args: Any) -> None:
        return

    def send_json(self, payload: Any, status: int = 200) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def send_file(self, path: Path) -> None:
        if not path.exists() or not path.is_file():
            self.send_error(404)
            return
        content_type = mimetypes.guess_type(str(path))[0] or "application/octet-stream"
        body = path.read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-cache")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        path = unquote(parsed.path)
        if path == "/api/skins":
            selected_id = current_skin_id()
            skins = [
                skin_payload(item, selected_id)
                for item in sorted(SKINS.iterdir(), key=lambda entry: entry.name.lower())
                if item.is_dir() and (item / "manifest.json").exists()
            ]
            self.send_json({"selected": selected_id, "skins": skins})
            return

        if path == "/api/settings":
            settings = read_json(SETTINGS_FILE, {})
            settings["skin_id"] = current_skin_id()
            self.send_json(settings)
            return

        if path.startswith("/api/skins/") and "/asset/" in path:
            parts = path.split("/")
            try:
                skin_id = safe_skin_id(parts[3])
                filename = safe_asset_name(parts[5])
            except (IndexError, ValueError):
                self.send_error(404)
                return
            self.send_file(SKINS / skin_id / "assets" / filename)
            return

        if path in ("/", ""):
            self.send_file(WEB_ROOT / "index.html")
            return

        static_path = (WEB_ROOT / path.lstrip("/")).resolve()
        if WEB_ROOT.resolve() in static_path.parents:
            self.send_file(static_path)
            return
        self.send_error(404)

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path != "/api/select":
            self.send_error(404)
            return

        length = int(self.headers.get("Content-Length", "0") or 0)
        try:
            body = json.loads(self.rfile.read(length).decode("utf-8"))
            skin_id = safe_skin_id(str(body.get("skin_id", "")))
        except (json.JSONDecodeError, ValueError):
            self.send_json({"ok": False, "error": "无法切换到这个形象"}, status=400)
            return

        settings = read_json(SETTINGS_FILE, {})
        settings["skin_id"] = skin_id
        write_json(SETTINGS_FILE, settings)
        self.send_json({"ok": True, "skin_id": skin_id})


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the desktop pet appearance studio.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), AppearanceHandler)
    url = f"http://{args.host}:{args.port}/"
    if not args.no_browser:
        threading.Timer(0.35, lambda: webbrowser.open(url)).start()
    print(f"Appearance studio running at {url}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
