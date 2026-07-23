// ============================================================
//  FILTER STATE
// ============================================================
let activeChips = new Set(); // official dex filter
let myTab = 'all';
let searchQ = '';

function onStateChanged() {
  render();
}

// When exactly one game is unambiguously "in focus" (a specific "MEU X"
// tab, or exactly one JOGOS chip), tapping a card should just toggle
// that game directly instead of opening the full modal - that's the
// whole point of narrowing the view to one game first. Any other state
// (TODOS, NAO RASTR., multiple chips) keeps the old open-modal behavior.
function activeSingleGame() {
  if (myTab !== 'all' && myTab !== 'no') return myTab;
  if (activeChips.size === 1) return [...activeChips][0];
  return null;
}

// Retorna { num, prefix } ou null. Z-A tem duas dexes: a principal
// (Lumiose City) e a do Mega Dimension (DLC, numeracao propria) - se o
// pokemon nao esta na principal mas esta na do DLC, mostra com prefixo
// "MD" pra nao confundir os dois numeros.
function regionalNumber(gid, pid) {
  if (REGIONAL_DEX[gid] && REGIONAL_DEX[gid][pid] !== undefined) {
    return { num: REGIONAL_DEX[gid][pid], prefix: '' };
  }
  if (gid === 'za' && REGIONAL_DEX.zaMega && REGIONAL_DEX.zaMega[pid] !== undefined) {
    return { num: REGIONAL_DEX.zaMega[pid], prefix: 'MD' };
  }
  return null;
}

let toastTimer = null;
function showToast(msg) {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('show'), 1600);
}

function quickToggle(id, gid) {
  const p = POKEMON.find(x => x.id === id);
  const g = GAMES.find(x => x.id === gid);
  const v = !has(id, gid);
  setG(id, gid, v);
  showToast((v ? '✓ ' : '✗ ') + p.n + ' - ' + g.name);
}

// ============================================================
//  CHIP FILTER
// ============================================================
function toggleChip(gid) {
  activeChips.has(gid) ? activeChips.delete(gid) : activeChips.add(gid);
  CHIP_IDS.forEach(id => {
    const el = document.getElementById('chip-' + id);
    el.classList.toggle('on', activeChips.has(id));
    el.style.background = activeChips.has(id) ? CHIP_COLORS[id] : '';
  });
  updateLabel(); render();
}

// ============================================================
//  TAB
// ============================================================
function setTab(t) {
  myTab = t;
  document.querySelectorAll('.tab').forEach(el => el.classList.toggle('on', el.dataset.t === t));
  updateLabel(); render();
}

function updateLabel() {
  const el = document.getElementById('slbl');
  if (myTab !== 'all') {
    const map = { bd: 'MEU BRILLIANT DIAMOND', home: 'MEU POKEMON HOME', arceus: 'MEU LEGENDS: ARCEUS', za: 'MEU LEGENDS: Z-A', go: 'MEU POKEMON GO', no: 'NAO RASTREADOS' };
    el.textContent = map[myTab] || myTab.toUpperCase(); return;
  }
  if (activeChips.size === 0) { el.textContent = 'TODOS OS POKEMON'; return; }
  const names = [...activeChips].map(id => GAMES.find(g => g.id === id)?.label || id.toUpperCase());
  el.textContent = 'POKEDEX: ' + names.join(' + ');
}

// ============================================================
//  SPRITES - local PNGs first (img/poke/<id>.png), fall back to
//  PokeAPI online artwork, then the small online sprite. Keeps the
//  app fully usable offline for whatever's already downloaded.
// ============================================================
function localArt(id) { return `img/poke/${id}.png`; } // resolved relative to www/index.html
function onlineArt(id) { return `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/${id}.png`; }
function onlineSprite(id) { return `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/${id}.png`; }

function spriteFallbackChain(imgEl, id) {
  let stage = 0;
  imgEl.onerror = () => {
    stage++;
    if (stage === 1) imgEl.src = onlineArt(id);
    else if (stage === 2) imgEl.src = onlineSprite(id);
    else imgEl.onerror = null;
  };
}

// ============================================================
//  RENDER
// ============================================================
function render() {
  const grid = document.getElementById('grid');
  const empty = document.getElementById('empty');
  const q = searchQ.toLowerCase();

  const list = POKEMON.filter(p => {
    if (q && !p.n.includes(q) && !String(p.id).includes(q)) return false;
    if (myTab === 'no') return myLocs(p.id).length === 0;
    if (myTab !== 'all') return has(p.id, myTab);
    if (activeChips.size > 0) {
      for (const gid of activeChips) { if (inDex(p.id, gid)) return true; }
      return false;
    }
    return true;
  });

  if (!list.length) { grid.innerHTML = ''; empty.style.display = ''; updateStats(); return; }
  empty.style.display = 'none';

  const singleGame = activeSingleGame();

  grid.innerHTML = list.map(p => {
    const availGames = CHIP_IDS.filter(gid => inDex(p.id, gid));
    const tags = availGames.map(gid => {
      const g = GAMES.find(x => x.id === gid);
      return `<span class="ctag" style="background:${g.color}">${g.label}</span>`;
    }).join('');

    const ml = myLocs(p.id);
    const dotClr = ml.length === 0 ? '#e0d0e0' : ml.length > 1 ? '#d040a0' : (GAMES.find(g => g.id === ml[0].id)?.color || '#aaa');

    const regional = singleGame ? regionalNumber(singleGame, p.id) : null;
    const numLabel = regional ? regional.prefix + '#' + String(regional.num).padStart(3, '0') : '#' + String(p.id).padStart(4, '0');
    const onClick = singleGame ? `quickToggle(${p.id},'${singleGame}')` : `openModal(${p.id})`;
    const quickOn = singleGame ? has(p.id, singleGame) : false;

    return `<div class="card${singleGame && quickOn ? ' quick-on' : ''}" onclick="${onClick}">
      <div class="ctop">
        <span class="cnum">${numLabel}</span>
        <div class="ctags">${tags}</div>
        <div class="cimgw">
          <img class="cimg" data-id="${p.id}" src="${localArt(p.id)}" alt="${p.n}" loading="lazy">
        </div>
      </div>
      <div class="cbot">
        <div class="cname">${p.n}</div>
        <div class="ctrow">${p.t.map(t => `<span class="ctp t-${t}">${t}</span>`).join('')}</div>
        <div class="chave" style="background:${dotClr}"></div>
      </div>
    </div>`;
  }).join('');

  grid.querySelectorAll('img.cimg').forEach(img => spriteFallbackChain(img, img.dataset.id));

  updateStats();
}

function updateStats() {
  const total = POKEMON.length;
  const inHome = POKEMON.filter(p => has(p.id, 'home')).length;
  const pct = Math.round(inHome / total * 100);
  document.getElementById('s-total').textContent = total + ' pokemon';
  document.getElementById('s-home').textContent = inHome + ' no HOME';
  document.getElementById('gbar').style.width = pct + '%';
  document.getElementById('pct').textContent = pct + '%';
}

// ============================================================
//  MODAL
// ============================================================
function openModal(id) {
  const p = POKEMON.find(x => x.id === id);
  document.getElementById('m-title').textContent = p.n.toUpperCase();
  const mi = document.getElementById('m-img');
  mi.src = localArt(id);
  spriteFallbackChain(mi, id);
  document.getElementById('m-num').textContent = '#' + String(id).padStart(4, '0');
  document.getElementById('m-name').textContent = p.n;
  document.getElementById('m-types').innerHTML = p.t.map(t => `<span class="ctp t-${t}">${t}</span>`).join('');

  document.getElementById('m-avail').innerHTML = CHIP_IDS.map(gid => {
    const g = GAMES.find(x => x.id === gid);
    const ok = inDex(id, gid);
    return `<span class="avchip${ok ? '' : ' no'}" style="${ok ? 'background:' + g.color : ''}">${g.label}</span>`;
  }).join('');

  refreshRows(id);
  document.getElementById('ov').classList.add('open');
}

function refreshRows(id) {
  document.getElementById('m-rows').innerHTML = GAMES.map(g => {
    const on = has(id, g.id);
    return `<div class="grow${on ? ' on' : ''}" style="--gc:${g.color}" onclick="toggleGame(${id},'${g.id}',this)">
      <div class="gdot">${on ? '&#10003;' : ''}</div>
      <div class="ginfo">
        <div class="gname">${g.name}</div>
        <div class="gsub">${g.sub}</div>
      </div>
      ${g.oneway ? '<span class="owbadge">1-WAY</span>' : ''}
    </div>`;
  }).join('');
  refreshWarn(id);
}

function toggleGame(pid, gid, el) {
  const v = !has(pid, gid);
  setG(pid, gid, v);
  el.classList.toggle('on', v);
  el.querySelector('.gdot').innerHTML = v ? '&#10003;' : '';
  refreshWarn(pid);
  render();
}

function refreshWarn(id) {
  const w = document.getElementById('m-warn');
  if (has(id, 'za')) {
    w.innerHTML = '<strong>No Z-A:</strong> transferencia permanente - nao pode voltar para BD, Arceus, GO ou HOME.';
    w.style.display = '';
  } else { w.style.display = 'none'; }
}

document.getElementById('mx').onclick = () => document.getElementById('ov').classList.remove('open');
document.getElementById('ov').onclick = e => { if (e.target === document.getElementById('ov')) document.getElementById('ov').classList.remove('open'); };

// ============================================================
//  SYNC UI
// ============================================================
function updateSyncButton() {
  const btn = document.getElementById('syncbtn-header');
  btn.classList.toggle('on', isSignedIn());
}

function openSyncModal() {
  renderSyncModal();
  document.getElementById('ov-sync').classList.add('open');
}

function renderSyncModal() {
  const body = document.getElementById('sync-body');
  if (isSignedIn()) {
    body.innerHTML = `
      <p>Logado como <strong>${syncSession.email || syncSession.uid}</strong>.</p>
      <button class="syncbtn" id="sync-now-btn">Sincronizar agora</button>
      <button class="syncbtn" id="sync-out-btn">Sair da conta</button>
      <div class="syncstatus" id="sync-status"></div>
    `;
    document.getElementById('sync-now-btn').onclick = async () => {
      const status = document.getElementById('sync-status');
      status.textContent = 'Sincronizando...';
      try {
        await syncNow();
        status.textContent = 'Sincronizado as ' + new Date().toLocaleTimeString();
      } catch (err) {
        status.textContent = 'Erro: ' + err.message;
      }
    };
    document.getElementById('sync-out-btn').onclick = () => {
      signOut();
      updateSyncButton();
      renderSyncModal();
    };
  } else {
    body.innerHTML = `
      <p>Entre com sua conta Google para sincronizar sua colecao entre dispositivos.</p>
      <button class="syncbtn" id="sync-in-btn">Entrar com Google</button>
      <div class="syncstatus" id="sync-status"></div>
    `;
    document.getElementById('sync-in-btn').onclick = async () => {
      const status = document.getElementById('sync-status');
      status.textContent = 'Abrindo login...';
      try {
        await signIn();
        updateSyncButton();
        status.textContent = 'Login ok, sincronizando...';
        await syncNow();
        renderSyncModal();
      } catch (err) {
        status.textContent = 'Erro: ' + err.message;
      }
    };
  }
}

document.getElementById('syncbtn-header').onclick = openSyncModal;
document.getElementById('sync-mx').onclick = () => document.getElementById('ov-sync').classList.remove('open');
document.getElementById('ov-sync').onclick = e => { if (e.target === document.getElementById('ov-sync')) document.getElementById('ov-sync').classList.remove('open'); };

// ============================================================
//  TOOLBAR
// ============================================================
document.getElementById('search').addEventListener('input', e => { searchQ = e.target.value; render(); });
document.getElementById('bclear').onclick = () => { if (confirm('Apagar tudo?')) { clearAll(); } };

// ============================================================
//  INIT
// ============================================================
load();
loadSession();
updateSyncButton();
render();

if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('service-worker.js').catch(() => {});
}
