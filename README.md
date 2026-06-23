# AI Desktop Pet

一个基于 Python 和 Tkinter 的透明桌面宠物项目。项目包含桌宠主程序、外观皮肤管理工具、动作素材处理脚本和多套角色资源，可用于展示桌面交互、状态管理、本地配置、资源处理和小型工具开发能力。

## Features

- Transparent always-on-top desktop pet window
- Click, drag, pat, poke and rest interactions
- Mood value, speech bubble, opacity, scale and position persistence
- Idle, blink, expression and running animation frames
- Skin switching through a local appearance studio
- External phrase configuration through `phrases.json`
- Asset preparation scripts for transparent PNG animation frames
- Self-test mode for checking required image assets

## Tech Stack

- Python 3
- Tkinter
- Pillow
- HTML/CSS/JavaScript for the appearance studio UI
- PowerShell launch scripts for Windows

## Project Structure

```text
desktop-pet/
  pet.py                         # Main desktop pet app
  appearance_studio.py            # Local skin-management server
  appearance_studio/              # Browser UI for skin preview and selection
  assets/                         # Runtime pet image assets
  skins/                          # Optional skin packages
  tools/                          # Asset generation and processing scripts
  phrases.json                    # Speech lines
  run_pet.ps1                     # Start the desktop pet on Windows
  run_appearance_studio.ps1       # Start the appearance studio
```

## Getting Started

Install dependencies:

```powershell
pip install -r requirements.txt
```

Run the desktop pet:

```powershell
.\run_pet.ps1
```

Run the appearance studio:

```powershell
.\run_appearance_studio.ps1
```

Run the asset self-test:

```powershell
python .\pet.py --self-test
```

## GitHub Resume Description

AI Desktop Pet: built a Python/Tkinter transparent desktop companion with animation states, configurable dialogue, local settings persistence, skin switching, and an asset-processing pipeline for generated character frames.

## Git Practice Covered

This project is suitable for demonstrating Git-based version control skills, including:

- `git init`
- `git add`
- `git commit`
- `git branch`
- `git remote add`
- `git push`
- Pulling and merging future updates
