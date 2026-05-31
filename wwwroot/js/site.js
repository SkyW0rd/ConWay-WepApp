let timer = null;
let selectedPattern = null;
let selectedCells = [];

let draftPattern = null;
let draftCells = [];
let draftX = 0;
let draftY = 0;
let lastState = null;

async function getJson(url) {
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return await res.json();
}

async function postJson(url) {
  const res = await fetch(url, { method: 'POST', cache: 'no-store' });
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return await res.json();
}

function normalize(cells) {
  const minX = Math.min(...cells.map(c => c[0]));
  const minY = Math.min(...cells.map(c => c[1]));
  return cells.map(([x, y]) => [x - minX, y - minY]);
}

function rotateRight(cells) {
  return normalize(cells.map(([x, y]) => [y, -x]));
}

function rotateLeft(cells) {
  return normalize(cells.map(([x, y]) => [-y, x]));
}

function flipH(cells) {
  return normalize(cells.map(([x, y]) => [-x, y]));
}

function flipV(cells) {
  return normalize(cells.map(([x, y]) => [x, -y]));
}

function selectPattern(pattern) {
  selectedPattern = pattern;
  selectedCells = normalize(pattern.cells);

  draftPattern = pattern;
  draftCells = normalize(pattern.cells);
  draftX = 0;
  draftY = 0;

  document.querySelectorAll('.pattern-item').forEach(x => x.classList.remove('active'));

  const active = document.querySelector(`[data-pattern="${pattern.name}"]`);
  if (active) active.classList.add('active');

  if (lastState) render(lastState);
}

async function placePattern(startX, startY) {
  if (!selectedPattern) return;

  for (const [dx, dy] of selectedCells) {
    await postJson(`/api/alive/${startX + dx}/${startY + dy}`);
  }

  const state = await getJson('/api/state');
  render(state);
}

function renderPatternList() {
  const list = document.getElementById('patternList');
  const search = document.getElementById('patternSearch').value.toLowerCase();

  list.innerHTML = '';

  patterns
    .filter(p => p.name.toLowerCase().includes(search))
    .forEach(pattern => {
      const item = document.createElement('div');
      item.className = 'pattern-item';
      item.textContent = pattern.name;
      item.draggable = true;
      item.dataset.pattern = pattern.name;

      item.onclick = () => selectPattern(pattern);

      item.ondragstart = e => {
        selectPattern(pattern);
        e.dataTransfer.setData('text/plain', pattern.name);
      };

      list.appendChild(item);
    });
}

function render(data) {
  lastState = data;

  document.getElementById('population').textContent = data.population;
  document.getElementById('generation').textContent = data.generation;

  const grid = document.getElementById('grid');
  grid.innerHTML = '';
  grid.style.gridTemplateColumns = `repeat(${data.width}, 22px)`;
  grid.style.gridTemplateRows = `repeat(${data.height}, 22px)`;

  for (let i = 0; i < data.cells.length; i++) {
    const x = i % data.width;
    const y = Math.floor(i / data.width);

    const isAliveCell = data.cells[i] === 1;
    const isPreviewCell = hasDraftCell(x, y);

    const div = document.createElement('div');
    div.className =
      'cell' +
      (isAliveCell ? ' alive' : '') +
      (isPreviewCell ? ' preview' : '');

    div.onclick = async () => {
      if (selectedPattern) {
        createDraftAt(x, y);
      } else {
        const state = await postJson(`/api/toggle/${x}/${y}`);
        render(state);
      }
    };

    div.ondragover = e => e.preventDefault();

    div.ondrop = e => {
      e.preventDefault();
      createDraftAt(x, y);
    };

    grid.appendChild(div);
  }
}

async function refresh() {
  const state = await getJson('/api/state');
  render(state);
}

function hasDraftCell(x, y) {
  if (!draftPattern) return false;

  return draftCells.some(([dx, dy]) => {
    return draftX + dx === x && draftY + dy === y;
  });
}

function createDraftAt(x, y) {
  if (!selectedPattern) return;

  draftPattern = selectedPattern;
  draftCells = [...selectedCells];
  draftX = x;
  draftY = y;

  if (lastState) render(lastState);
}

function moveDraft(dx, dy) {
  if (!draftPattern) return;

  draftX += dx;
  draftY += dy;

  if (lastState) render(lastState);
}

function rotateDraftLeft() {
  if (!draftPattern) return;

  draftCells = rotateLeft(draftCells);

  if (lastState) render(lastState);
}

function rotateDraftRight() {
  if (!draftPattern) return;

  draftCells = rotateRight(draftCells);

  if (lastState) render(lastState);
}

function flipDraftH() {
  if (!draftPattern) return;

  draftCells = flipH(draftCells);

  if (lastState) render(lastState);
}

function flipDraftV() {
  if (!draftPattern) return;

  draftCells = flipV(draftCells);

  if (lastState) render(lastState);
}

async function commitDraft() {
  if (!draftPattern) return;

  for (const [dx, dy] of draftCells) {
    await postJson(`/api/alive/${draftX + dx}/${draftY + dy}`);
  }

  draftPattern = null;
  draftCells = [];

  const state = await getJson('/api/state');
  render(state);
}

function cancelDraft() {
  draftPattern = null;
  draftCells = [];

  if (lastState) render(lastState);
}

window.addEventListener('DOMContentLoaded', () => {
  renderPatternList();

  document.getElementById('patternSearch').oninput = renderPatternList;

  document.getElementById('stepBtn').onclick = async () => {
    const state = await postJson('/api/step');
    render(state);
  };

  document.getElementById('randomBtn').onclick = async () => {
    const state = await postJson('/api/random');
    render(state);
  };

  document.getElementById('clearBtn').onclick = async () => {
    const state = await postJson('/api/clear');
    render(state);
  };

  document.getElementById('runBtn').onclick = () => {
    if (timer) return;

    timer = setInterval(async () => {
      const state = await postJson('/api/step');
      render(state);
    }, 300);
  };

  document.getElementById('stopBtn').onclick = () => {
    clearInterval(timer);
    timer = null;
  };

  document.getElementById('moveUpBtn').onclick = () => moveDraft(0, -1);
  document.getElementById('moveDownBtn').onclick = () => moveDraft(0, 1);
  document.getElementById('moveLeftBtn').onclick = () => moveDraft(-1, 0);
  document.getElementById('moveRightBtn').onclick = () => moveDraft(1, 0);

  document.getElementById('rotateLeftBtn').onclick = rotateDraftLeft;
  document.getElementById('rotateRightBtn').onclick = rotateDraftRight;
  document.getElementById('flipHBtn').onclick = flipDraftH;
  document.getElementById('flipVBtn').onclick = flipDraftV;

  document.getElementById('placeBtn').onclick = commitDraft;
  document.getElementById('cancelPatternBtn').onclick = cancelDraft;

  refresh();
});
