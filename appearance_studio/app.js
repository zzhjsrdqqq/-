const state = {
  skins: [],
  selectedSkin: null,
  mode: "idle",
  frame: 0,
  timer: null,
};

const skinList = document.querySelector("#skinList");
const previewImage = document.querySelector("#previewImage");
const skinName = document.querySelector("#skinName");
const selectedState = document.querySelector("#selectedState");
const readyState = document.querySelector("#readyState");
const tagRow = document.querySelector("#tagRow");
const skinDescription = document.querySelector("#skinDescription");
const selectButton = document.querySelector("#selectButton");
const saveState = document.querySelector("#saveState");
const modeButtons = document.querySelector("#modeButtons");
const refreshButton = document.querySelector("#refreshButton");
const timeline = document.querySelector("#timeline");

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`);
  }
  return response.json();
}

function cacheBust(url) {
  const joiner = url.includes("?") ? "&" : "?";
  return `${url}${joiner}v=${Date.now()}`;
}

function modeFrames(skin) {
  if (!skin) return [];
  return skin.assets[state.mode] || skin.assets.idle || [];
}

function setAccent(skin) {
  document.documentElement.style.setProperty("--accent", skin?.accent || "#5f7f63");
}

function renderSkins() {
  skinList.innerHTML = "";
  for (const skin of state.skins) {
    const item = document.createElement("button");
    item.className = `skin-item${skin.id === state.selectedSkin?.id ? " active" : ""}`;
    item.type = "button";
    item.addEventListener("click", () => {
      state.selectedSkin = skin;
      state.frame = 0;
      setAccent(skin);
      render();
    });

    const thumb = document.createElement("div");
    thumb.className = "skin-thumb";
    const img = document.createElement("img");
    img.alt = skin.name;
    img.src = cacheBust(skin.preview);
    thumb.append(img);

    const text = document.createElement("div");
    const title = document.createElement("p");
    title.className = "skin-title";
    title.textContent = skin.name;
    const subtitle = document.createElement("p");
    subtitle.className = "skin-subtitle";
    subtitle.textContent = skin.selected ? "当前主宠" : skin.ready ? "可切换" : "资源缺失";
    text.append(title, subtitle);

    item.append(thumb, text);
    skinList.append(item);
  }
}

function renderTimeline(frames) {
  timeline.innerHTML = "";
  frames.forEach((url, index) => {
    const chip = document.createElement("div");
    chip.className = `frame-chip${index === state.frame % frames.length ? " active" : ""}`;
    const img = document.createElement("img");
    img.alt = `第 ${index + 1} 帧`;
    img.src = cacheBust(url);
    chip.append(img);
    timeline.append(chip);
  });
}

function renderDetails() {
  const skin = state.selectedSkin;
  if (!skin) return;
  skinName.textContent = skin.name;
  selectedState.textContent = skin.selected ? "当前主宠" : "未启用";
  readyState.textContent = skin.ready ? "资源完整" : "资源缺失";
  readyState.className = `status-pill ${skin.ready ? "ready" : "warning"}`;
  selectButton.disabled = skin.selected || !skin.ready;
  selectButton.textContent = skin.selected ? "已是主宠" : "设为主宠";
  skinDescription.textContent = skin.description || "";

  tagRow.innerHTML = "";
  for (const tag of skin.tags || []) {
    const item = document.createElement("span");
    item.className = "tag";
    item.textContent = tag;
    tagRow.append(item);
  }
}

function renderPreview() {
  const frames = modeFrames(state.selectedSkin);
  if (!frames.length) return;
  state.frame %= frames.length;
  previewImage.src = cacheBust(frames[state.frame]);
  renderTimeline(frames);
}

function renderModeButtons() {
  modeButtons.querySelectorAll("button").forEach((button) => {
    button.classList.toggle("active", button.dataset.mode === state.mode);
  });
}

function render() {
  renderSkins();
  renderDetails();
  renderPreview();
  renderModeButtons();
}

function startAnimation() {
  if (state.timer) window.clearInterval(state.timer);
  state.timer = window.setInterval(() => {
    const frames = modeFrames(state.selectedSkin);
    if (!frames.length) return;
    state.frame = (state.frame + 1) % frames.length;
    renderPreview();
  }, state.mode === "blink" ? 95 : 150);
}

async function loadSkins() {
  saveState.textContent = "";
  const payload = await api("/api/skins");
  state.skins = payload.skins;
  state.selectedSkin = state.skins.find((skin) => skin.selected) || state.skins[0] || null;
  state.frame = 0;
  setAccent(state.selectedSkin);
  render();
  startAnimation();
}

modeButtons.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-mode]");
  if (!button) return;
  state.mode = button.dataset.mode;
  state.frame = 0;
  render();
  startAnimation();
});

selectButton.addEventListener("click", async () => {
  const skin = state.selectedSkin;
  if (!skin || skin.selected || !skin.ready) return;
  selectButton.disabled = true;
  saveState.textContent = "正在保存";
  try {
    await api("/api/select", {
      method: "POST",
      body: JSON.stringify({ skin_id: skin.id }),
    });
    saveState.textContent = "已保存，重启桌宠后生效";
    await loadSkins();
  } catch (error) {
    saveState.textContent = "保存失败";
    selectButton.disabled = false;
  }
});

refreshButton.addEventListener("click", loadSkins);

loadSkins().catch(() => {
  skinName.textContent = "读取失败";
  readyState.textContent = "离线";
  readyState.className = "status-pill warning";
});
