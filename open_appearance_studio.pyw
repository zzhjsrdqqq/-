from __future__ import annotations

import subprocess
import sys
import time
import webbrowser
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parent
URL = "http://127.0.0.1:8765/"


def server_ready() -> bool:
    try:
        with urlopen(URL, timeout=0.45) as response:
            return 200 <= response.status < 500
    except (OSError, URLError):
        return False


def main() -> None:
    if not server_ready():
        subprocess.Popen(
            [sys.executable, str(ROOT / "appearance_studio.py"), "--no-browser"],
            cwd=ROOT,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        for _ in range(18):
            if server_ready():
                break
            time.sleep(0.18)

    webbrowser.open(URL)


if __name__ == "__main__":
    main()
