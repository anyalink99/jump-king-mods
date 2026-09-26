const body = [
  "............", "............", ".....DD.....", "....DHHDD...",
  "...DHLMLD...", "...DLMMMD...", "...DBCCBD...", "...DBBBBD...",
  "...DSMMMD...", "...DSMMMD...", "...DSMSSD...", "...DMMMMDD..",
  "...DMMSSSD..", "....DSSSD...", ".....DDD....", "....DDDDD...",
  ".....DDD...."
];

const flames = [
  ["....ywy....","...oywyo...","...oywyo...","...oyyyo...","..ooyyyoo..","...oyyyo...","...ooyoo...","..rooyoor..","...rooor...","...rooor...","..rrooor...","...rroor...","...rroor...","....rorr...","....rrr....","....rrr....",".....rr....",".....r.....",".....r.....","...........","..........."],
  ["...oywyo...","...oywyo...","..ooywyoo..","...oyyyo...","...oyyyo...","..ooyyyo...","...ooyoo...","...rooor...","..rrooorr..","...rooor...","...rroor...","....roor...","...rroor...","....rror...","....rrr....",".....rr....","....rr.....",".....r.....","....r......","...........","..........."],
  ["....ywy....","...oywyo...","..oywwwyo..","...oywyo...","..ooyyyoo..","..ooyyyoo..","...ooyoo...","..roooyoor.","...rooor...","..rrooor...","...rroorr..","...rroor...","....roor...","...rroor...","....rrr....","....rr.....",".....rr....","....rr.....",".....r.....",".....r.....","..........."],
  ["...oywyo...","...oywyo...","...oywyo...","..ooyyyoo..","...oyyyo...","..ooyyyoo..","...ooyoo...","...rooor...","..rrooor...","...rooor...","..rrooor...","...rroor...","...rroor...","....rror...","...rrr.....","....rr.....","....rr.....",".....rr....",".....r.....","......r....","..........."]
];

const colors = {
  D: "#191d24", S: "#34414c", M: "#526771", L: "#7e999d",
  H: "#bbcfc6", B: "#5b2327", C: "#ab372b", r: "#7e2019",
  o: "#dc4a1c", y: "#ffa423", w: "#ffee8f"
};

const stage = document.querySelector("#stage");
const stageContext = stage.getContext("2d");
const filmstrip = document.querySelector("#filmstrip");
const xInput = document.querySelector("#x-input");
const yInput = document.querySelector("#y-input");
const xReadout = document.querySelector("#x-readout");
const yReadout = document.querySelector("#y-readout");
const poseName = document.querySelector("#pose-name");
const poseIndex = document.querySelector("#pose-index");
const moveAll = document.querySelector("#move-all");
const showFlame = document.querySelector("#show-flame");
const flipPreview = document.querySelector("#flip-preview");
const undoButton = document.querySelector("#undo");
const redoButton = document.querySelector("#redo");
const saveButton = document.querySelector("#save");
const reloadButton = document.querySelector("#reload");
const saveStatus = document.querySelector("#save-status");

let poses = [];
let selected = 0;
let savedSnapshot = "";
let undoStack = [];
let redoStack = [];
let drag = null;
let atlas = null;

const clonePoses = () => poses.map(pose => ({ ...pose }));
const serialize = () => JSON.stringify(poses);

function setStatus(text, state = "") {
  saveStatus.textContent = text;
  saveStatus.className = `save-status ${state}`;
}

function markDirty() {
  if (serialize() === savedSnapshot) setStatus("No unsaved changes");
  else setStatus("Unsaved changes", "dirty");
}

function pushHistory() {
  const snapshot = serialize();
  if (undoStack[undoStack.length - 1] !== snapshot) undoStack.push(snapshot);
  if (undoStack.length > 100) undoStack.shift();
  redoStack = [];
  updateHistoryButtons();
}

function restoreSnapshot(snapshot) {
  poses = JSON.parse(snapshot);
  renderAll();
  markDirty();
}

function undo() {
  if (!undoStack.length) return;
  redoStack.push(serialize());
  restoreSnapshot(undoStack.pop());
  updateHistoryButtons();
}

function redo() {
  if (!redoStack.length) return;
  undoStack.push(serialize());
  restoreSnapshot(redoStack.pop());
  updateHistoryButtons();
}

function updateHistoryButtons() {
  undoButton.disabled = undoStack.length === 0;
  redoButton.disabled = redoStack.length === 0;
}

function setPixel(context, x, y, value) {
  const color = colors[value];
  if (!color) return;
  context.fillStyle = color;
  context.fillRect(x, y, 1, 1);
}

function drawPose(context, pose, width, height, flameFrame = 0, flipped = false) {
  context.clearRect(0, 0, width, height);
  context.imageSmoothingEnabled = false;
  const originX = width === 64 ? 8 : 0;
  const originY = width === 64 ? 4 : 0;

  context.save();
  if (flipped) {
    context.translate(width, 0);
    context.scale(-1, 1);
  }

  if (showFlame.checked && !pose.rotated) {
    const nozzleX = pose.x + 6;
    const nozzleY = pose.y + body.length;
    flames[flameFrame].forEach((row, y) => {
      [...row].forEach((value, x) => {
        setPixel(context, originX + nozzleX - 5 + x, originY + nozzleY + y, value);
      });
    });
  }

  body.forEach((row, y) => {
    [...row].forEach((value, x) => {
      if (value === ".") return;
      const transformedX = pose.rotated ? body.length - 1 - y : x;
      const transformedY = pose.rotated ? x : y;
      setPixel(context, originX + pose.x + transformedX, originY + pose.y + transformedY, value);
    });
  });

  context.drawImage(atlas, pose.cellX, pose.cellY, 48, 48, originX, originY, 48, 48);
  context.restore();
}

function renderStage() {
  if (!poses.length || !atlas) return;
  drawPose(stageContext, poses[selected], 64, 80, selected % flames.length, flipPreview.checked);
}

function renderFilmstrip() {
  filmstrip.innerHTML = "";
  poses.forEach((pose, index) => {
    const button = document.createElement("button");
    button.className = `frame-button${index === selected ? " active" : ""}`;
    button.type = "button";
    button.setAttribute("role", "option");
    button.setAttribute("aria-selected", index === selected ? "true" : "false");
    button.title = pose.name;
    const canvas = document.createElement("canvas");
    canvas.width = 48;
    canvas.height = 60;
    drawPose(canvas.getContext("2d"), pose, 48, 60, index % flames.length, false);
    const label = document.createElement("span");
    label.textContent = pose.name;
    button.append(canvas, label);
    button.addEventListener("click", () => selectPose(index));
    filmstrip.append(button);
  });
}

function syncControls() {
  const pose = poses[selected];
  xInput.value = pose.x;
  yInput.value = pose.y;
  xReadout.textContent = pose.x;
  yReadout.textContent = pose.y;
  poseName.textContent = pose.name;
  poseIndex.textContent = `${String(selected + 1).padStart(2, "0")} / ${poses.length}`;
}

function renderAll() {
  renderStage();
  renderFilmstrip();
  syncControls();
}

function selectPose(index) {
  selected = index;
  renderAll();
}

function moveBy(dx, dy, record = true) {
  if (!dx && !dy) return;
  if (record) pushHistory();
  if (moveAll.checked) {
    poses.forEach(pose => {
      pose.x += dx;
      pose.y += dy;
    });
  } else {
    poses[selected].x += dx;
    poses[selected].y += dy;
  }
  renderAll();
  markDirty();
}

function setCoordinate(axis, value) {
  if (!Number.isFinite(value)) return;
  const delta = value - poses[selected][axis];
  if (!delta) return;
  pushHistory();
  if (moveAll.checked) poses.forEach(pose => { pose[axis] += delta; });
  else poses[selected][axis] = value;
  renderAll();
  markDirty();
}

stage.addEventListener("pointerdown", event => {
  event.preventDefault();
  stage.setPointerCapture(event.pointerId);
  stage.classList.add("dragging");
  drag = {
    pointerId: event.pointerId,
    startX: event.clientX,
    startY: event.clientY,
    positions: clonePoses(),
    recorded: false
  };
});

stage.addEventListener("pointermove", event => {
  if (!drag || event.pointerId !== drag.pointerId) return;
  const rect = stage.getBoundingClientRect();
  const dx = Math.round((event.clientX - drag.startX) * stage.width / rect.width);
  const dy = Math.round((event.clientY - drag.startY) * stage.height / rect.height);
  if ((dx || dy) && !drag.recorded) {
    pushHistory();
    drag.recorded = true;
  }
  if (moveAll.checked) {
    poses.forEach((pose, index) => {
      pose.x = drag.positions[index].x + dx;
      pose.y = drag.positions[index].y + dy;
    });
  } else {
    poses[selected].x = drag.positions[selected].x + dx;
    poses[selected].y = drag.positions[selected].y + dy;
  }
  renderStage();
  syncControls();
  markDirty();
});

function endDrag(event) {
  if (!drag || event.pointerId !== drag.pointerId) return;
  if (drag.recorded) renderFilmstrip();
  drag = null;
  stage.classList.remove("dragging");
}
stage.addEventListener("pointerup", endDrag);
stage.addEventListener("pointercancel", endDrag);

xInput.addEventListener("input", () => {
  if (xInput.value !== "") setCoordinate("x", Number(xInput.value));
});
yInput.addEventListener("input", () => {
  if (yInput.value !== "") setCoordinate("y", Number(yInput.value));
});
showFlame.addEventListener("change", renderAll);
flipPreview.addEventListener("change", renderStage);
undoButton.addEventListener("click", undo);
redoButton.addEventListener("click", redo);

document.addEventListener("keydown", event => {
  const command = event.ctrlKey || event.metaKey;
  if (command && event.key.toLowerCase() === "z") {
    event.preventDefault();
    event.shiftKey ? redo() : undo();
    return;
  }
  if (command && event.key.toLowerCase() === "y") {
    event.preventDefault();
    redo();
    return;
  }
  if (event.target.matches("input")) return;
  const amount = event.shiftKey ? 4 : 1;
  const movement = {
    ArrowLeft: [-amount, 0], ArrowRight: [amount, 0],
    ArrowUp: [0, -amount], ArrowDown: [0, amount]
  }[event.key];
  if (movement) {
    event.preventDefault();
    moveBy(...movement);
  }
});

async function loadPoses() {
  const response = await fetch("/api/poses", { cache: "no-store" });
  if (!response.ok) throw new Error(await response.text());
  poses = await response.json();
  selected = Math.min(selected, poses.length - 1);
  savedSnapshot = serialize();
  undoStack = [];
  redoStack = [];
  updateHistoryButtons();
  renderAll();
  setStatus("No unsaved changes");
}

saveButton.addEventListener("click", async () => {
  saveButton.disabled = true;
  setStatus("Saving…");
  try {
    const response = await fetch("/api/poses", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: serialize()
    });
    if (!response.ok) throw new Error(await response.text());
    savedSnapshot = serialize();
    setStatus("Positions saved", "saved");
  } catch (error) {
    setStatus(`Could not save: ${error.message}`, "error");
  } finally {
    saveButton.disabled = false;
  }
});

reloadButton.addEventListener("click", async () => {
  if (serialize() !== savedSnapshot && !confirm("Discard all unsaved changes?")) return;
  try {
    await loadPoses();
  } catch (error) {
    setStatus(`Could not load: ${error.message}`, "error");
  }
});

async function start() {
  atlas = new Image();
  atlas.src = `/atlas.png?t=${Date.now()}`;
  await atlas.decode();
  await loadPoses();
}

start().catch(error => setStatus(`Editor failed to start: ${error.message}`, "error"));
