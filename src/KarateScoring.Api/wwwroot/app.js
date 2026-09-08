(function () {
"use strict";

const GRADES = ["Ceinture blanche","Ceinture jaune","Ceinture orange","Ceinture verte","Ceinture bleue","Ceinture marron","Ceinture noire 1er Dan","Ceinture noire 2e Dan","Ceinture noire 3e Dan et +"];
const NIVEAUX = ["Amicale","Club","Ligue","National"];
const FORMAT_LABEL = { FinaleDirecte: "Finale directe", PouleUnique: "Poule unique", PoulePuisElimination: "Poule(s) puis élimination", EliminationRepechage: "Élimination directe + repêchage" };
const PENALITES = ["Chukoku", "Keikoku", "HansokuChui", "Hansoku", "Kiken", "Shikkaku"];
const PENALITE_LABEL = { Chukoku: "Chukoku", Keikoku: "Keikoku", HansokuChui: "Hansoku-chui", Hansoku: "Hansoku", Kiken: "Kiken", Shikkaku: "Shikkaku" };
const DISQUALIFIANTES = ["Hansoku", "Shikkaku", "Kiken"];

/* ================= API ================= */
async function apiFetch(method, path, body) {
  const headers = body !== undefined ? { "Content-Type": "application/json" } : {};
  if (operateurNom) headers["X-Operateur"] = operateurNom;
  const res = await fetch("/api" + path, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (res.status === 204) return null;
  const text = await res.text();
  let data = null;
  if (text) { try { data = JSON.parse(text); } catch (e) { data = text; } }
  if (!res.ok) {
    const msg = (data && typeof data === "object" && (data.detail || data.title)) ||
      (typeof data === "string" ? data : null) || (res.status + " " + res.statusText);
    throw new Error(msg);
  }
  return data;
}
const api = {
  get: (p) => apiFetch("GET", p),
  post: (p, b) => apiFetch("POST", p, b === undefined ? {} : b),
  put: (p, b) => apiFetch("PUT", p, b === undefined ? {} : b),
  del: (p) => apiFetch("DELETE", p),
};
/* Upload multipart (FormData) : distinct d'apiFetch, qui envoie toujours du JSON. */
async function apiUpload(path, formData) {
  const headers = {};
  if (operateurNom) headers["X-Operateur"] = operateurNom;
  const res = await fetch("/api" + path, { method: "POST", headers, body: formData });
  const text = await res.text();
  let data = null;
  if (text) { try { data = JSON.parse(text); } catch (e) { data = text; } }
  if (!res.ok) {
    const msg = (data && typeof data === "object" && (data.detail || data.title)) || (typeof data === "string" ? data : null) || (res.status + " " + res.statusText);
    throw new Error(msg);
  }
  return data;
}
async function safe(fn) {
  try { await fn(); return true; }
  catch (e) { toast(e.message || String(e), true); return false; }
}

/* ================= State ================= */
let activeCompetitionId = Number(localStorage.getItem("karate_scoring_active_competition")) || null;
let operateurNom = localStorage.getItem("karate_scoring_operateur") || "";
let currentRoute = "accueil";
let routeState = {};

/* Lien direct par tatami (#tatami/<aireId>) : un poste tatami met ce lien en favori une fois pour
   toutes et retombe toujours sur sa propre file d'attente, même après une coupure Wi-Fi/veille qui
   aurait sinon perdu la sélection d'aire (jusque-là gardée seulement en mémoire JS, jamais dans l'URL). */
function parseHashRoute() {
  const mTatami = /^#tatami\/(\d+)$/.exec(location.hash);
  if (mTatami) { currentRoute = "tatamis"; routeState.tatamiAireId = Number(mTatami[1]); return; }
  const mPublic = /^#public\/(\d+)$/.exec(location.hash);
  if (mPublic) { currentRoute = "public"; routeState.publicAireId = Number(mPublic[1]); }
}
function syncTatamiHash(aireId) {
  const wanted = "#tatami/" + aireId;
  if (location.hash !== wanted) history.replaceState(null, "", wanted);
}
window.addEventListener("hashchange", () => { parseHashRoute(); renderApp(); });
// Chrono par combat : { remainingMs, totalSec, running, runningSince }. remainingMs est le temps
// restant "figé" au dernier point de contrôle (démarrage/pause/reset/réglage durée) ; pendant que
// le chrono tourne, le temps réel affiché se calcule à partir de runningSince (horloge murale, voir
// chronoRemainingMs) plutôt que d'être décrémenté à chaque tick — ça évite toute dérive cumulative
// en cas d'onglet en arrière-plan ou de machine chargée, et ça permet de reconstituer le temps exact
// après un rafraîchissement de page puisque runningSince est un horodatage absolu persisté.
let timers = loadTimers();
let competitionsCache = [];
let networkInfoCache = null;
let securiteCache = null;
let sidebarOpen = false;

function loadTimers() {
  try { return JSON.parse(localStorage.getItem("karate_scoring_timers")) || {}; }
  catch (e) { return {}; }
}
function saveTimers() {
  try { localStorage.setItem("karate_scoring_timers", JSON.stringify(timers)); }
  catch (e) { /* stockage indisponible : le chrono reste fonctionnel pour cet onglet, juste pas persistant */ }
}
function chronoRemainingMs(t) {
  if (!t) return 0;
  return t.running ? Math.max(0, t.remainingMs - (Date.now() - t.runningSince)) : t.remainingMs;
}
/* Miroise l'état du chrono côté serveur (démarré/en pause + temps restant) pour qu'un écran public
   sur un autre appareil puisse le reconstruire par polling — best-effort, ne bloque jamais l'arbitre. */
function syncChronoServeur(confId, t) {
  api.post(`/combats/${confId}/chrono-sync`, { running: !!(t && t.running), remainingMs: chronoRemainingMs(t) }).catch(() => {});
}

function setActiveCompetition(id) {
  activeCompetitionId = id;
  localStorage.setItem("karate_scoring_active_competition", id ? String(id) : "");
}
function activeComp() { return competitionsCache.find((c) => c.id === activeCompetitionId) || null; }

/* Retourne le code saisi si un code admin est configuré, null si aucun n'est requis, ou undefined si l'utilisateur annule la saisie. */
function demanderCodeAdminSiConfigure() {
  if (!(securiteCache && securiteCache.codeConfigure)) return null;
  const code = prompt("Code administrateur requis :");
  return code == null ? undefined : code;
}

function toast(msg, isErr) {
  const wrap = document.getElementById("toastWrap");
  const d = document.createElement("div");
  d.className = "toast" + (isErr ? " err" : "");
  d.textContent = msg;
  wrap.appendChild(d);
  setTimeout(() => d.remove(), 3800);
}

/* ================= Helpers ================= */
function esc(s) { return (s == null ? "" : String(s)).replace(/[&<>"']/g, (m) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[m])); }
function fmtDate(d) { if (!d) return "—"; const dt = new Date(d); if (isNaN(dt)) return d; return dt.toLocaleDateString("fr-FR", { day: "2-digit", month: "short", year: "numeric" }); }
function ageOf(dn, refDate) {
  if (!dn) return null;
  const d = new Date(dn), r = new Date(refDate || new Date().toISOString().slice(0, 10));
  if (isNaN(d)) return null;
  let age = r.getFullYear() - d.getFullYear();
  if (r.getMonth() < d.getMonth() || (r.getMonth() === d.getMonth() && r.getDate() < d.getDate())) age--;
  return age;
}
function disciplineLabel(d) { return d === "KumiteIndividuel" ? "Kumite individuel" : d === "KataIndividuel" ? "Kata individuel" : "Kata équipe"; }
function eligible(p, cat, comp) {
  const age = ageOf(p.dateNaissance, comp.date);
  if (age != null && cat.ageMin != null && cat.ageMax != null && (age < cat.ageMin || age > cat.ageMax)) return false;
  if (cat.gradeMin && GRADES.indexOf(p.grade) < GRADES.indexOf(cat.gradeMin)) return false;
  return true;
}
function determinerFormatClient(n, comp) {
  if (n < 2) return null;
  if (n === 2) return "FinaleDirecte";
  if (n <= comp.seuilPouleUnique) return "PouleUnique";
  if (n <= comp.seuilPoulePuisElimination) return "PoulePuisElimination";
  return "EliminationRepechage";
}
function decisionLabel(m) { return { EcartPoints: "Écart de points", FinTemps: "Fin du temps réglementaire", Hantei: "Hantei (drapeaux)", Disqualification: "Disqualification" }[m] || m || "—"; }
function dureeLabel(sec) { if (!sec) return "—"; const m = Math.floor(sec / 60), s = sec % 60; return m + " min " + (s < 10 ? "0" : "") + s + " s"; }
function medailleLabel(m) { return { Or: "Or", Argent: "Argent", Bronze1: "Bronze", Bronze2: "Bronze" }[m] || m; }
function medailleClass(m) { return m === "Or" ? "or" : m === "Argent" ? "argent" : "bronze"; }

function field(label, name, type, placeholder, required, style) {
  return `<div class="field" ${style ? `style="${style}"` : ""}><label>${esc(label)}</label><input type="${type}" name="${name}" placeholder="${esc(placeholder || "")}" ${required ? "required" : ""}></div>`;
}
function numField(label, name, val) {
  return `<div class="field"><label>${esc(label)}</label><input type="number" name="${name}" value="${val === undefined || val === "" || val === null ? "" : val}"></div>`;
}
function selectField(label, name, options, def, labelFn) {
  const opts = options.map((o) => `<option value="${esc(o)}" ${o === def ? "selected" : ""}>${esc(labelFn ? labelFn(o) : (o || "—"))}</option>`).join("");
  return `<div class="field"><label>${esc(label)}</label><select name="${name}">${opts}</select></div>`;
}
function medalCard(cls, label, nom) {
  return `<div class="medal-card ${cls}"><div class="medal">${esc(label)}</div><div class="cnm">${esc(nom)}</div></div>`;
}
/* Icône karateka : kimono (gi) avec ceinture colorée selon Aka (rouge) / Ao (bleu). */
function giIcon(couleur, size) {
  size = size || 22;
  const belt = couleur === "aka" ? "var(--aka)" : "var(--ao)";
  return `<svg class="gi-icon" viewBox="0 0 32 34" width="${size}" height="${size}" aria-hidden="true">
    <circle cx="16" cy="6.2" r="4.2" fill="var(--ink-soft)"/>
    <path d="M8 32 L8 15.2 Q8 12 11.2 11 L16 14.6 L20.8 11 Q24 12 24 15.2 L24 32 Z" fill="var(--paper-raised)" stroke="var(--ink-soft)" stroke-width="1.1" stroke-linejoin="round"/>
    <path d="M16 14.6 L12.4 32 M16 14.6 L19.6 32" stroke="var(--ink-soft)" stroke-width="0.9" fill="none"/>
    <rect x="8" y="20.6" width="16" height="3.8" fill="${belt}"/>
  </svg>`;
}

function badgeStatut(statut) {
  if (statut === "EnAttente") return '<span class="tag tag-attente">En attente</span>';
  if (statut === "EnCours") return '<span class="tag tag-encours">En cours</span>';
  return '<span class="tag tag-termine">Terminé</span>';
}

/* Réplique en lecture seule du classement de poule (victoires puis différentiel) pour prévisualiser
   les rencontres avant que la phase finale ne soit générée côté serveur — jamais utilisé pour décider,
   seulement pour l'affichage ; le calcul qui compte reste TableauService/KataTableauService côté API. */
function classerPouleClient(confs) {
  const stats = {};
  function add(id, diff, win) { const c = stats[id] || { v: 0, diff: 0 }; c.v += win ? 1 : 0; c.diff += diff; stats[id] = c; }
  confs.filter((c) => c.statut === "Termine").forEach((c) => {
    let diffA, diffB;
    if (c.type === "kumite") { diffA = (c.scoreAka || 0) - (c.scoreAo || 0); diffB = -diffA; }
    else { const vAka = (c.votes || []).filter((v) => v.couleur === "Aka").length; const vAo = (c.votes || []).length - vAka; diffA = vAka - vAo; diffB = -diffA; }
    if (c.aId != null) add(c.aId, diffA, c.vainqueurCouleur === "Aka");
    if (c.bId != null) add(c.bId, diffB, c.vainqueurCouleur === "Ao");
  });
  return Object.keys(stats).map((id) => ({ id: Number(id), victoires: stats[id].v, diff: stats[id].diff })).sort((a, b) => b.victoires - a.victoires || b.diff - a.diff);
}
function nomDeId(confs, id) {
  for (const c of confs) { if (c.aId === id) return { nom: c.aNom, club: c.aClub }; if (c.bId === id) return { nom: c.bNom, club: c.bClub }; }
  return { nom: "—", club: "—" };
}

/* ================= Routing / Render ================= */
const NAV = [
  { id: "accueil", label: "Accueil", ico: "⌂", kanji: "家" },
  { sec: "Organisation" },
  { id: "competitions", label: "Compétitions", ico: "◆", kanji: "大会" },
  { id: "categories", label: "Catégories", ico: "▤", kanji: "級" },
  { id: "participants", label: "Participants", ico: "◉", kanji: "選手" },
  { id: "equipes", label: "Équipes Kata", ico: "◈", kanji: "組" },
  { sec: "Compétition" },
  { id: "tableaux", label: "Tableaux", ico: "⑂", kanji: "表" },
  { id: "tatamis", label: "Tatamis", ico: "▣", kanji: "畳" },
  { id: "kumite", label: "Arbitrage Kumite", ico: "⚑", kanji: "組手" },
  { id: "kata", label: "Jury Kata", ico: "⚐", kanji: "型" },
  { sec: "Bilan" },
  { id: "resultats", label: "Résultats & exports", ico: "▦", kanji: "賞" },
  { id: "audit", label: "Journal d'audit", ico: "≣", kanji: "記録" },
  { id: "securite", label: "Sécurité", ico: "⛨", kanji: "安全" },
];

async function renderApp() {
  if (currentRoute === "public") { await renderPublicScreen(); return; }
  competitionsCache = await api.get("/competitions").catch(() => []);
  if (activeCompetitionId && !competitionsCache.some((c) => c.id === activeCompetitionId)) setActiveCompetition(null);
  if (!networkInfoCache) networkInfoCache = await api.get("/network-info").catch(() => null);
  securiteCache = await api.get("/securite").catch(() => securiteCache);

  const app = document.getElementById("app");
  const comp = activeComp();
  const mobileTopbar = `<div class="mobile-topbar">
    <button class="hamburger" type="button" data-action="toggle-sidebar" aria-label="Menu">☰</button>
    <span class="mt-comp">${comp ? esc(comp.nom) : "Karate Scoring"}</span>
  </div>`;
  app.innerHTML = mobileTopbar + renderSidebar() + `<div class="sidebar-backdrop${sidebarOpen ? " open" : ""}" data-action="close-sidebar"></div>` +
    '<main><div id="screenRoot"><div class="spinner-line">Chargement…</div></div></main>';

  let html;
  try { html = await renderScreen(); }
  catch (e) { html = `<div class="card"><p class="error">${esc(e.message || String(e))}</p></div>`; }
  document.getElementById("screenRoot").innerHTML = html;
}

function renderSidebar() {
  const comp = activeComp();
  const navHtml = NAV.map((item) => item.sec
    ? `<li class="section-label">${esc(item.sec)}</li>`
    : `<li><a href="#" data-nav="${item.id}" class="${currentRoute === item.id ? "active" : ""}"><span class="ico">${item.ico}</span>${item.label}<span class="kanji">${item.kanji || ""}</span></a></li>`
  ).join("");
  return `
  <nav class="sidebar${sidebarOpen ? " open" : ""}">
    <div class="sidebar-brand"><div class="logo-badge"><img class="brand-logo" src="assets/karate-scoring-logo.jpg" alt="Karate Scoring"></div><div class="sub">Plateforme locale</div></div>
    <div class="sidebar-comp">Compétition active${comp ? `<b>${esc(comp.nom)}</b>` : '<b style="color:var(--sidebar-ink-dim);font-weight:500;">Aucune sélectionnée</b>'}</div>
    <ul class="nav">${navHtml}</ul>
    <div class="sidebar-partner"><img src="assets/fkc-logo.jpg" alt="Fouda Karate Club"><div class="ptxt">Partenaire fondateur<b>Fouda Karate Club</b></div></div>
    <div class="sidebar-foot">Application autonome, exécutée localement (Docker) — toutes les données restent sur ce poste.
      ${networkInfoCache && networkInfoCache.addresses.length ? `<div class="hint" style="margin:6px 0;">Postes tatami — ouvrir : ${networkInfoCache.addresses.map((a) => `<code>http://${a}:${networkInfoCache.port}</code>`).join(", ")}</div>` : ""}
      <button data-action="seed-demo" type="button">Charger la démo</button>
      <button data-action="reset-all" type="button">Réinitialiser tout</button>
    </div>
  </nav>`;
}

function screenGuardNoComp() {
  return `<div class="topbar"><div><div class="crumb">Karate Scoring</div><h1>Sélectionnez une compétition</h1></div></div>
  <div class="card"><p>Créez ou activez une compétition dans l'écran <b>Compétitions</b> pour continuer, ou chargez le jeu de données de démonstration depuis le menu de gauche.</p>
  <button class="btn btn-primary" data-nav="competitions">Aller à Compétitions</button></div>`;
}

async function renderScreen() {
  switch (currentRoute) {
    case "accueil": return await screenAccueil();
    case "competitions": return await screenCompetitions();
    case "categories": return activeComp() ? await screenCategories() : screenGuardNoComp();
    case "participants": return activeComp() ? await screenParticipants() : screenGuardNoComp();
    case "equipes": return activeComp() ? await screenEquipes() : screenGuardNoComp();
    case "tableaux": return activeComp() ? await screenTableaux() : screenGuardNoComp();
    case "tatamis": return activeComp() ? await screenTatamis() : screenGuardNoComp();
    case "kumite": return activeComp() ? await screenKumite() : screenGuardNoComp();
    case "kata": return activeComp() ? await screenKata() : screenGuardNoComp();
    case "resultats": return activeComp() ? await screenResultats() : screenGuardNoComp();
    case "audit": return await screenAudit();
    case "securite": return await screenSecurite();
    default: return await screenAccueil();
  }
}

/* ---- Accueil ---- */
async function screenAccueil() {
  const comp = activeComp();
  if (!comp) {
    return `<div class="welcome-screen">
      <img class="welcome-logo" src="assets/karate-scoring-logo.jpg" alt="Karate Scoring">
      <h1>Bienvenue dans Karate Scoring</h1>
      <p class="welcome-msg">Aux arbitres et opérateurs : veuillez sélectionner une compétition pour commencer.</p>
      <div class="welcome-actions">
        <button class="btn btn-primary" data-nav="competitions">Aller à Compétitions</button>
      </div>
    </div>`;
  }

  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const avecTableau = cats.filter((c) => c.hasTableau);
  const tableaux = await Promise.all(avecTableau.map(async (c) => ({ cat: c, tableau: await api.get(`/categories/${c.id}/tableau`) })));

  const brackets = tableaux.map(({ cat, tableau }) => {
    let html;
    if (tableau.format === "PouleUnique") html = renderPouleTable(tableau.confrontations, null);
    else {
      const finale = tableau.confrontations.filter((c) => !c.estRepechage && (tableau.format !== "PoulePuisElimination" || c.tour >= 2));
      html = finale.length ? renderBracket(finale) : renderPouleTable(tableau.confrontations.filter((c) => c.tour === 1), null);
    }
    return `<div class="card"><h3>${esc(cat.nom)} <span class="muted">${FORMAT_LABEL[tableau.format]}</span></h3>${html}</div>`;
  }).join("");

  // Combat suivant, toutes catégories confondues (hypothèse un seul tatami actif — cahier §14/15) :
  // priorité au combat déjà en cours, sinon le premier en attente.
  const kumite = await kumiteEligibleConfs(comp);
  const enCours = kumite.find((x) => x.c.statut === "EnCours");
  const suivant = enCours || kumite.find((x) => x.c.statut === "EnAttente");
  const suivantHtml = suivant ? `
    <div class="card next-combat-card">
      <h3>${suivant.c.statut === "EnCours" ? "Combat en cours" : "Combat suivant"} <span class="muted">${esc(suivant.cat.nom)}</span></h3>
      <div class="next-combat-row">
        <div class="next-combat-side">${publicPhotoFrame(suivant.c.aId, "aka", 80)}<div class="next-combat-name">${esc(suivant.c.aNom)}</div><div class="next-combat-club">${esc(suivant.c.aClub || "")}</div></div>
        <div class="next-combat-vs">VS</div>
        <div class="next-combat-side">${publicPhotoFrame(suivant.c.bId, "ao", 80)}<div class="next-combat-name">${esc(suivant.c.bNom)}</div><div class="next-combat-club">${esc(suivant.c.bClub || "")}</div></div>
      </div>
    </div>` : "";

  return `<div class="topbar"><div><div class="crumb">Karate Scoring</div><h1>Bienvenue à ${esc(comp.nom)}${comp.lieu ? " — " + esc(comp.lieu) : ""}</h1></div></div>
  ${brackets || '<p class="empty">Aucun tableau généré pour l\'instant — rendez-vous dans l\'écran Tableaux.</p>'}
  ${suivantHtml}`;
}

/* ---- Compétitions ---- */
async function screenCompetitions() {
  const rows = competitionsCache.map((c) => {
    const active = c.id === activeCompetitionId;
    return `<tr><td><b>${esc(c.nom)}</b><div class="hint">${esc(c.lieu || "")}</div></td><td>${fmtDate(c.date)}</td><td><span class="pill">${esc(c.niveau)}</span></td>
      <td>${active ? '<span class="tag tag-termine">Active</span>' : `<button class="btn btn-sm" data-action="activer-comp" data-id="${c.id}">Activer</button>`}
      <button class="btn btn-sm btn-ghost" data-action="edit-comp" data-id="${c.id}">Réglages</button></td></tr>`;
  }).join("");

  let editing = "";
  if (routeState.editCompId) {
    const c = competitionsCache.find((x) => x.id === routeState.editCompId);
    if (c) editing = await screenCompetitionReglages(c);
  }

  return `
  <div class="topbar"><div><div class="crumb">Organisation</div><h1>Compétitions</h1></div></div>
  <div class="card"><h3>Nouvelle compétition</h3>
    <form id="form-competition" class="grid grid-3">
      ${field("Nom", "nom", "text", "Ex. Coupe FKC 2026", true)}
      ${field("Date", "date", "date", "", true)}
      ${field("Lieu", "lieu", "text", "Ex. Gymnase Fouda", true)}
      ${selectField("Niveau", "niveau", NIVEAUX, "Club")}
      <div></div><div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary" type="submit">Créer la compétition</button></div>
    </form>
  </div>
  <div class="card"><h3>Compétitions enregistrées <span class="muted">${competitionsCache.length} au total</span></h3>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Compétition</th><th>Date</th><th>Niveau</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucune compétition. Créez-en une ci-dessus, ou chargez la démo depuis le menu.</p>'}
  </div>
  ${editing}`;
}

async function screenCompetitionReglages(c) {
  const katas = await api.get("/katas");
  const katasHtml = katas.map((k) => `<span class="tag tag-attente" style="margin:3px 4px 0 0;display:inline-flex;gap:6px;">${esc(k.nom)} <a href="#" data-action="del-kata" data-id="${k.id}" style="color:var(--danger);text-decoration:none;">✕</a></span>`).join("");
  return `<div class="card">
    <h3>Réglages réglementaires — ${esc(c.nom)} <button class="btn btn-sm btn-ghost" data-action="close-comp-reglages" type="button">Fermer</button></h3>
    <form id="form-reglages" data-id="${c.id}" class="grid grid-4">
      ${numField("Durée combat (s)", "dureeCombatDefautSec", c.dureeCombatDefautSec)}
      ${numField("Écart victoire (pts)", "ecartVictoire", c.ecartVictoire)}
      ${numField("Nombre de juges Kata", "nbJugesKataDefaut", c.nbJugesKataDefaut)}
      <div></div>
      ${numField("Seuil poule unique (≤)", "seuilPouleUnique", c.seuilPouleUnique)}
      ${numField("Seuil poule+élimination (≤)", "seuilPoulePuisElimination", c.seuilPoulePuisElimination)}
      <div></div><div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary btn-sm" type="submit">Enregistrer les réglages</button></div>
    </form>
    <div style="margin-top:14px;"><label>Liste des katas (personnalisable, partagée entre compétitions)</label>
      <div style="margin-bottom:10px;">${katasHtml}</div>
      <form id="form-add-kata" class="row-inline"><div class="field" style="flex:1;margin-bottom:0;"><input type="text" name="kata" placeholder="Ajouter un kata…" required></div><button class="btn btn-sm" type="submit">Ajouter</button></form>
    </div>
  </div>`;
}

/* ---- Catégories ---- */
async function screenCategories() {
  const comp = activeComp();
  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const rows = cats.map((c) => `<tr><td><b>${esc(c.nom)}</b></td><td>${disciplineLabel(c.discipline)}</td><td>${esc(c.sexe || "")}</td><td>${c.ageMin ?? "–"}–${c.ageMax ?? "–"} ans</td><td>${c.inscritsCount}</td>
    <td><button class="btn btn-sm btn-ghost" data-action="del-cat" data-id="${c.id}">Supprimer</button></td></tr>`).join("");

  return `
  <div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Catégories</h1></div></div>
  <div class="card"><h3>Nouvelle catégorie</h3>
    <form id="form-categorie" data-comp="${comp.id}" class="grid grid-4">
      ${field("Nom", "nom", "text", "Ex. Kumite Seniors -75kg", true, "grid-column:1/3")}
      ${selectField("Discipline", "discipline", ["KumiteIndividuel", "KataIndividuel", "KataEquipe"], "KumiteIndividuel", disciplineLabel)}
      ${selectField("Sexe", "sexe", ["Mixte", "Messieurs", "Dames"], "Mixte")}
      ${numField("Âge min", "ageMin", 14)}
      ${numField("Âge max", "ageMax", 99)}
      ${selectField("Grade minimum", "gradeMin", [""].concat(GRADES), "")}
      <div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary" type="submit">Créer la catégorie</button></div>
    </form>
  </div>
  <div class="card"><h3>Catégories ${esc(comp.nom)} <span class="muted">${cats.length}</span></h3>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Nom</th><th>Discipline</th><th>Sexe</th><th>Âge</th><th>Inscrits</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucune catégorie pour l\'instant.</p>'}
  </div>`;
}

/* ---- Participants ---- */
function avatarHtml(url, kind, hasImage, size) {
  size = size || 34;
  const icon = kind === "club" ? "🏫" : "🥋";
  const fallback = `<span class="avatar-fallback" style="${hasImage ? "display:none;" : "display:flex;"}font-size:${Math.round(size * 0.55)}px;">${icon}</span>`;
  const img = hasImage ? `<img src="${url}?t=${Date.now()}" alt="" onerror="this.style.display='none';this.nextElementSibling.style.display='flex';">` : "";
  return `<span class="avatar" style="width:${size}px;height:${size}px;">${img}${fallback}</span>`;
}
const FORMAT_IMAGE_HINT = "Formats acceptés : PNG ou JPEG uniquement, 5 Mo maximum. Tout autre format sera refusé.";

async function screenParticipants() {
  const comp = activeComp();
  const [participants, cats, clubs] = await Promise.all([api.get("/participants"), api.get(`/competitions/${comp.id}/categories`), api.get("/clubs")]);
  const catsIndividuelles = cats.filter((c) => c.discipline !== "KataEquipe");

  const rows = participants.map((p) => {
    const age = ageOf(p.dateNaissance, comp.date);
    return `<tr><td style="display:flex;align-items:center;gap:8px;">${avatarHtml(`/api/participants/${p.id}/photo`, "participant", p.aPhoto)}<div><b>${esc(p.prenom + " " + p.nom)}</b><div class="hint">${esc(p.licence || "")}</div></div></td><td>${esc(p.club)}</td><td>${esc(p.grade || "")}</td><td>${age != null ? age + " ans" : "—"}</td><td>${p.poids ? p.poids + " kg" : "—"}</td>
      <td style="white-space:nowrap;">
        <label class="btn btn-sm btn-ghost" title="${FORMAT_IMAGE_HINT}">📷 Photo<input type="file" accept="image/png,image/jpeg" data-action="upload-photo" data-id="${p.id}" style="display:none"></label>
        <button class="btn btn-sm btn-ghost" data-action="inscrire" data-id="${p.id}">Inscrire…</button> <button class="btn btn-sm btn-ghost" data-action="del-part" data-id="${p.id}">Suppr.</button></td></tr>`;
  }).join("");

  const clubRows = clubs.map((c) => `<tr><td style="display:flex;align-items:center;gap:8px;">${avatarHtml(`/api/clubs/${c.id}/logo`, "club", c.aLogo)}<b>${esc(c.nom)}</b></td>
    <td style="white-space:nowrap;"><label class="btn btn-sm btn-ghost" title="${FORMAT_IMAGE_HINT}">🏷️ Logo<input type="file" accept="image/png,image/jpeg" data-action="upload-logo" data-id="${c.id}" style="display:none"></label></td></tr>`).join("");

  let inscrForm = "";
  if (routeState.inscrireId) {
    const p = participants.find((x) => x.id === routeState.inscrireId);
    if (p) inscrForm = await renderInscriptionForm(p, catsIndividuelles, comp);
  }

  return `
  <div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Participants</h1></div></div>
  <div class="card"><h3>Ajouter un participant</h3>
    <form id="form-participant" class="grid grid-4">
      ${field("Prénom", "prenom", "text", "", true)}
      ${field("Nom", "nom", "text", "", true)}
      ${field("Club", "club", "text", "", true)}
      ${field("N° licence", "licence", "text", "", false)}
      ${selectField("Grade", "grade", GRADES, GRADES[0])}
      ${field("Date de naissance", "dateNaissance", "date", "", false)}
      ${numField("Poids (kg)", "poids", "")}
      <div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary" type="submit">Ajouter</button></div>
    </form>
    <div class="hint" style="margin-top:6px;">L'import en masse Excel/CSV (cahier 5.2) est une évolution future — saisie unitaire ou données de démonstration pour l'instant.</div>
  </div>
  ${inscrForm}
  <div class="card"><h3>Participants <span class="muted">${participants.length}</span></h3>
    <p class="hint" style="margin-top:-4px;">${FORMAT_IMAGE_HINT} La photo apparaît sur les écrans d'arbitrage et l'écran public.</p>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Nom</th><th>Club</th><th>Grade</th><th>Âge</th><th>Poids</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucun participant. Ajoutez-en un ci-dessus.</p>'}
  </div>
  <div class="card"><h3>Clubs <span class="muted">${clubs.length}</span></h3>
    <p class="hint" style="margin-top:-4px;">${FORMAT_IMAGE_HINT} Le logo apparaît sur l'écran public.</p>
    ${clubRows ? `<div class="table-wrap"><table><thead><tr><th>Club</th><th></th></tr></thead><tbody>${clubRows}</tbody></table></div>` : '<p class="empty">Aucun club — créé automatiquement en ajoutant un participant.</p>'}
  </div>`;
}

async function renderInscriptionForm(p, cats, comp) {
  const current = await api.get(`/participants/${p.id}/inscriptions`);
  const opts = cats.map((c) => {
    const elig = eligible(p, c, comp);
    return `<label style="display:flex;align-items:center;gap:8px;font-weight:400;text-transform:none;letter-spacing:0;font-size:13px;padding:5px 0;">
      <input type="checkbox" name="cat" value="${c.id}" ${current.includes(c.id) ? "checked" : ""}> ${esc(c.nom)}
      ${elig ? "" : '<span class="tag" style="background:#fff3d6;color:#8a5a00;">Hors critères d\'âge/grade</span>'}
    </label>`;
  }).join("");
  return `<div class="card"><h3>Inscrire ${esc(p.prenom + " " + p.nom)} <button class="btn btn-sm btn-ghost" type="button" data-action="close-inscrire">Fermer</button></h3>
    <form id="form-inscription" data-part="${p.id}">${opts || "<p class=\"empty\">Créez d'abord des catégories.</p>"}
    ${cats.length ? '<button class="btn btn-primary btn-sm" style="margin-top:10px;" type="submit">Enregistrer les inscriptions</button>' : ""}
    </form></div>`;
}

/* ---- Équipes ---- */
async function screenEquipes() {
  const comp = activeComp();
  const [equipes, cats, participants] = await Promise.all([api.get(`/competitions/${comp.id}/equipes`), api.get(`/competitions/${comp.id}/categories`), api.get("/participants")]);
  const eqCats = cats.filter((c) => c.discipline === "KataEquipe");

  const rows = equipes.map((e) => {
    const membres = e.membres.map((m) => esc(m.nom)).join(", ");
    const cat = e.categorieId != null ? cats.find((c) => c.id === e.categorieId) : null;
    return `<tr><td><b>${esc(e.nom)}</b></td><td>${esc(e.club)}</td><td style="max-width:280px;">${membres}</td><td>${cat ? esc(cat.nom) : '<span class="hint">non inscrite</span>'}</td>
      <td><button class="btn btn-sm btn-ghost" data-action="del-equipe" data-id="${e.id}">Suppr.</button></td></tr>`;
  }).join("");
  const memberOpts = participants.map((p) => `<label style="display:flex;align-items:center;gap:8px;font-weight:400;text-transform:none;letter-spacing:0;font-size:13px;padding:4px 0;"><input type="checkbox" name="membre" value="${p.id}"> ${esc(p.prenom + " " + p.nom)} <span class="hint">(${esc(p.club)})</span></label>`).join("");
  const catOpts = eqCats.map((c) => `<option value="${c.id}">${esc(c.nom)}</option>`).join("");

  return `
  <div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Équipes Kata</h1></div></div>
  <div class="card"><h3>Nouvelle équipe</h3>
    ${eqCats.length ? "" : '<p class="hint">Créez d\'abord une catégorie de discipline « Kata équipe » dans l\'écran Catégories.</p>'}
    <form id="form-equipe" data-comp="${comp.id}" class="grid grid-2">
      ${field("Nom de l'équipe", "nom", "text", "Ex. Équipe FKC A", true)}
      ${field("Club", "club", "text", "", true)}
      ${catOpts ? `<div class="field" style="grid-column:1/-1"><label>Catégorie</label><select name="categorieId">${catOpts}</select></div>` : ""}
      <div style="grid-column:1/-1"><label>Membres (3 recommandés)</label><div style="max-height:180px;overflow:auto;border:1px solid var(--line);border-radius:8px;padding:8px 10px;">${memberOpts}</div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary" type="submit" ${catOpts ? "" : "disabled"}>Créer l'équipe</button></div>
    </form>
  </div>
  <div class="card"><h3>Équipes <span class="muted">${equipes.length}</span></h3>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Équipe</th><th>Club</th><th>Membres</th><th>Catégorie</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucune équipe.</p>'}
  </div>`;
}

/* ---- Tableaux ---- */
async function screenTableaux() {
  const comp = activeComp();
  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const selId = routeState.tableauCatId || (cats[0] && cats[0].id);
  const cat = cats.find((c) => c.id === selId);
  const opts = cats.map((c) => `<option value="${c.id}" ${c.id === selId ? "selected" : ""}>${esc(c.nom)} (${c.inscritsCount} inscrits)</option>`).join("");

  let body;
  if (!cat) {
    body = '<p class="empty">Créez une catégorie puis inscrivez des participants.</p>';
  } else {
    const tableau = await api.get(`/categories/${cat.id}/tableau`);
    if (!tableau) {
      const formatAuto = cat.inscritsCount >= 2 ? FORMAT_LABEL[determinerFormatClient(cat.inscritsCount, comp)] : null;
      body = `<div class="card"><h3>Générer le tableau — ${esc(cat.nom)}</h3>
        <p>Effectif inscrit : <b>${cat.inscritsCount}</b>. Format déterminé automatiquement : <b>${formatAuto || "—"}</b> (cahier des charges §5.3).</p>
        <form id="form-gen-tableau" data-cat="${cat.id}" class="row-inline">
          <div class="field" style="margin-bottom:0;"><label>Forcer un format (optionnel)</label><select name="force"><option value="">Automatique — ${formatAuto || "—"}</option>${Object.keys(FORMAT_LABEL).map((k) => `<option value="${k}">${FORMAT_LABEL[k]}</option>`).join("")}</select></div>
          <button class="btn btn-primary" type="submit" ${cat.inscritsCount < 2 ? "disabled" : ""}>Tirer au sort &amp; générer</button>
        </form>
        ${cat.inscritsCount < 2 ? '<p class="hint">Il faut au moins 2 inscrits.</p>' : ""}
      </div>`;
    } else {
      const aires = await api.get(`/competitions/${comp.id}/aires`);
      body = renderTableauBody(tableau, cat, aires);
    }
  }

  return `<div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Tableaux de compétition</h1></div><select id="select-tableau-cat">${opts}</select></div>${body}`;
}

function renderTableauBody(tableau, cat, aires) {
  const aireOpts = `<option value="">Aucune aire</option>` + (aires || []).map((a) => `<option value="${a.id}" ${tableau.aireId === a.id ? "selected" : ""}>${esc(a.nom)}</option>`).join("");
  let html = `<div class="card"><h3>${esc(cat.nom)} <span class="muted">${FORMAT_LABEL[tableau.format]}</span>
    <select data-action="assign-aire" data-tab="${tableau.id}" style="margin-left:10px;font-size:12px;">${aireOpts}</select>
    <button class="btn btn-sm btn-ghost" data-action="print-tableau" type="button">🖨️ Imprimer</button>
    <button class="btn btn-sm btn-ghost" data-action="regen-tableau" data-tab="${tableau.id}" type="button">Régénérer…</button></h3>`;

  if (tableau.format === "PouleUnique") {
    html += renderPouleTable(tableau.confrontations, null);
  } else if (tableau.format === "PoulePuisElimination") {
    html += `<div class="grid grid-2">${renderPouleTable(tableau.confrontations.filter((c) => c.tour === 1 && c.moitie === 1), "Poule 1")}${renderPouleTable(tableau.confrontations.filter((c) => c.tour === 1 && c.moitie === 2), "Poule 2")}</div>`;
    const poulesDone = tableau.confrontations.filter((c) => c.tour === 1).every((c) => c.statut === "Termine");
    const elimGenerated = tableau.confrontations.some((c) => c.tour >= 2);
    if (poulesDone && !elimGenerated) {
      html += `<button class="btn btn-primary btn-sm" data-action="gen-elim-apres-poules" data-tab="${tableau.id}" style="margin-top:6px;">Générer la phase à élimination directe</button>`;
    } else if (elimGenerated) {
      html += `<h4 style="margin:16px 0 8px;font-size:14px;">Phase à élimination directe</h4>${renderBracket(tableau.confrontations.filter((c) => c.tour >= 2 && !c.estRepechage))}`;
      const rep = tableau.confrontations.filter((c) => c.estRepechage);
      if (rep.length) html += `<h4 style="margin:16px 0 8px;font-size:14px;">Repêchage — deux médailles de bronze</h4>${renderBracket(rep, true)}`;
    } else {
      html += '<p class="hint" style="margin-top:8px;">Terminez toutes les rencontres de poule pour générer la phase finale.</p>';
    }
  } else {
    html += renderBracket(tableau.confrontations.filter((c) => !c.estRepechage));
    const rep = tableau.confrontations.filter((c) => c.estRepechage);
    if (rep.length) html += `<h4 style="margin:16px 0 8px;font-size:14px;">Repêchage — deux médailles de bronze</h4>${renderBracket(rep, true)}`;
  }
  html += "</div>";
  return html;
}

function renderPouleTable(confs, label) {
  const classement = classerPouleClient(confs);
  const matches = confs.map((c) => {
    const statut = c.statut === "Termine"
      ? (c.type === "kumite"
        ? `${esc(c.aNom)} <span class="tag tag-aka">${c.scoreAka}</span> — <span class="tag tag-ao">${c.scoreAo}</span> ${esc(c.bNom)}`
        : `${esc(c.aNom)} <span class="tag tag-aka">${(c.votes || []).filter((v) => v.couleur === "Aka").length}</span> — <span class="tag tag-ao">${(c.votes || []).filter((v) => v.couleur === "Ao").length}</span> ${esc(c.bNom)}`)
      : '<span class="tag tag-attente">à arbitrer</span>';
    return `<tr><td>${esc(c.aNom)} <span class="vs">vs</span> ${esc(c.bNom)}</td><td>${statut}</td></tr>`;
  }).join("");
  const classRows = classement.map((r, i) => { const { nom } = nomDeId(confs, r.id); return `<tr><td>${i + 1}</td><td>${esc(nom)}</td><td>${r.victoires}</td><td>${r.diff}</td></tr>`; }).join("");
  return `<div>${label ? `<h4 style="font-size:13px;margin-bottom:6px;">${label}</h4>` : ""}
    <div class="table-wrap"><table><thead><tr><th>Rencontre</th><th>Résultat</th></tr></thead><tbody>${matches}</tbody></table></div>
    <div class="table-wrap" style="margin-top:8px;"><table><thead><tr><th>#</th><th>Compétiteur</th><th>V</th><th>Diff.</th></tr></thead><tbody>${classRows || '<tr><td colspan="4" class="empty">—</td></tr>'}</tbody></table></div>
  </div>`;
}

function renderBracket(confs, isRepechage) {
  if (!confs.length) return '<p class="empty">—</p>';
  const byTour = {};
  confs.forEach((c) => (byTour[c.tour] = byTour[c.tour] || []).push(c));
  const tours = Object.keys(byTour).map(Number).sort((a, b) => a - b);
  const maxTour = tours[tours.length - 1];

  const cols = tours.map((t, idx) => {
    const isLastCol = idx === tours.length - 1;
    const label = isRepechage ? "Repêchage T" + t : (t === maxTour && byTour[t].length === 1 ? "Finale" : (t === maxTour - 1 ? "Demi-finales" : "Tour " + t));

    let inner;
    if (isLastCol) {
      inner = byTour[t].map((c) => `<div class="bracket-single">${matchCard(c)}</div>`).join("");
    } else {
      // Regroupe les combats de ce tour par combat suivant commun (prochainCombatId), pour dessiner
      // les traits de crochet reliant chaque paire au combat qui en découle — plutôt que de supposer
      // un ordre pair/impair fragile dès qu'il y a des exempts ou du repêchage.
      const groups = {};
      const order = [];
      byTour[t].forEach((c) => {
        const key = c.prochainCombatId != null ? String(c.prochainCombatId) : `solo-${c.id}`;
        if (!groups[key]) { groups[key] = []; order.push(key); }
        groups[key].push(c);
      });
      order.sort((a, b) => {
        const na = /^\d+$/.test(a) ? +a : Infinity, nb = /^\d+$/.test(b) ? +b : Infinity;
        return na - nb;
      });
      inner = order.map((key) => {
        const g = groups[key];
        return g.length === 2
          ? `<div class="bracket-pair">${g.map((c) => matchCard(c)).join("")}</div>`
          : `<div class="bracket-single">${matchCard(g[0])}</div>`;
      }).join("");
    }
    return `<div class="bracket-round${isLastCol ? "" : " has-next"}"><div class="round-label">${label}</div><div class="bracket-matches">${inner}</div></div>`;
  }).join("");

  const finale = byTour[maxTour].length === 1 ? byTour[maxTour][0] : null;
  const championNom = finale && finale.vainqueurCouleur ? (finale.vainqueurCouleur === "Aka" ? finale.aNom : finale.bNom) : null;
  const championHtml = !isRepechage && championNom
    ? `<div class="bracket-champion"><div class="trophy">🏆</div><div class="champion-label">Vainqueur</div><div class="champion-name">${esc(championNom)}</div></div>`
    : "";

  return `<div class="bracket">${cols}${championHtml}</div>`;
}
function matchCard(c) {
  function slot(nom, id, couleur, score, isWinner) {
    if (id == null) return `<div class="slot bye ${couleur}">${giIcon(couleur, 18)}<span>— exempt —</span></div>`;
    return `<div class="slot ${couleur}${isWinner ? " win" : ""}"><span class="who">${giIcon(couleur, 20)}<span class="nm">${esc(nom)}</span></span>${c.type === "kumite" && c.statut === "Termine" ? `<span>${score}</span>` : ""}</div>`;
  }
  const winA = c.vainqueurCouleur === "Aka", winB = c.vainqueurCouleur === "Ao";
  const body = slot(c.aNom, c.aId, "aka", c.scoreAka, winA) + slot(c.bNom, c.bId, "ao", c.scoreAo, winB);
  const clickable = (c.aId != null && c.bId != null && !c.estBye) ? ` data-action="goto-confrontation" data-id="${c.id}" data-type="${c.type}"` : "";
  return `<div class="match-card"${clickable}>${body}</div>`;
}

/* ---- Tatamis ---- */
async function screenTatamis() {
  const comp = activeComp();
  const aires = await api.get(`/competitions/${comp.id}/aires`);
  const selId = routeState.tatamiAireId || (aires[0] && aires[0].id);

  const rows = aires.map((a) => `<tr>
    <td><input type="text" class="aire-nom-input" data-id="${a.id}" value="${esc(a.nom)}" style="width:100%;"></td>
    <td style="white-space:nowrap;">
      <button class="btn btn-sm" data-action="save-aire-nom" data-id="${a.id}">Enregistrer</button>
      <button class="btn btn-sm ${a.id === selId ? "btn-primary" : "btn-ghost"}" data-action="select-tatami" data-id="${a.id}">File d'attente</button>
      <button class="btn btn-sm btn-ghost" data-action="del-aire" data-id="${a.id}">Supprimer</button>
    </td></tr>`).join("");

  let html = `<div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Tatamis</h1></div></div>
  <div class="card"><h3>Nouvelle aire</h3>
    <form id="form-aire" data-comp="${comp.id}" class="row-inline">
      <div class="field" style="flex:1;margin-bottom:0;"><input type="text" name="nom" placeholder="Ex. Tatami 1" required></div>
      <button class="btn btn-primary btn-sm" type="submit">Ajouter</button>
    </form>
  </div>
  <div class="card"><h3>Aires <span class="muted">${aires.length}</span></h3>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Nom</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Créez une aire pour répartir les tableaux entre plusieurs tatamis, puis assignez-les depuis l\'écran Tableaux.</p>'}
  </div>`;

  const selAire = aires.find((a) => a.id === selId);
  if (selAire) { syncTatamiHash(selAire.id); html += await renderFileAttente(selAire); }
  return html;
}

async function renderFileAttente(aire) {
  const fa = await api.get(`/aires/${aire.id}/file-attente`);
  function row(entry, label, primary) {
    if (!entry) return `<tr><td>${label}</td><td class="empty">—</td><td></td></tr>`;
    const c = entry.confrontation;
    return `<tr><td>${label}</td><td>${esc(entry.categorieNom)} <span class="hint">(${c.type === "kumite" ? "Kumite" : "Kata"})</span><br>${esc(c.aNom)} <span class="vs">vs</span> ${esc(c.bNom)}</td>
      <td>${badgeStatut(c.statut)} <button class="btn btn-sm ${primary ? "btn-primary" : ""}" data-action="goto-confrontation" data-id="${c.id}" data-type="${c.type}">${c.type === "kumite" ? "Arbitrer" : "Juger"}</button></td></tr>`;
  }
  const aVenirRows = fa.aVenir.map((e) => row(e, "À venir")).join("");
  return `<div class="card"><h3>File d'attente — ${esc(aire.nom)}
      <button class="btn btn-sm btn-ghost" data-action="copy-tatami-link" data-id="${aire.id}" type="button" style="margin-left:8px;">🔗 Copier le lien de ce poste</button>
      <button class="btn btn-sm btn-ghost" data-action="copy-public-link" data-id="${aire.id}" type="button">📺 Copier le lien de l'écran public</button>
    </h3>
    <p class="hint" style="margin-top:-4px;">Le premier lien ramène toujours à la file d'attente de <b>${esc(aire.nom)}</b> (poste d'arbitrage) ; le second ouvre l'affichage plein écran pour TV/vidéoprojecteur — à mettre en favori sur l'appareil de ce tatami.</p>
    <div class="table-wrap"><table><thead><tr><th>Statut</th><th>Rencontre</th><th></th></tr></thead><tbody>
      ${row(fa.enCours, "En cours", true)}
      ${row(fa.suivant, "Suivant")}
      ${aVenirRows}
    </tbody></table></div>
  </div>`;
}

/* ---- Écran public (TV / vidéoprojecteur) ----
   Poste indépendant, sans sidebar ni compétition active locale : tout part du seul aireId dans le
   hash (#public/<id>), interrogé par polling — aucune dépendance à l'état du navigateur de l'arbitre. */
let publicPollTimer = null;
async function renderPublicScreen() {
  if (publicPollTimer) { clearInterval(publicPollTimer); publicPollTimer = null; }
  const aireId = routeState.publicAireId;
  const app = document.getElementById("app");
  app.innerHTML = '<div id="publicRoot" class="public-screen"><div class="public-wait">Chargement…</div></div>';

  const tick = async () => {
    if (currentRoute !== "public") { if (publicPollTimer) { clearInterval(publicPollTimer); publicPollTimer = null; } return; }
    const root = document.getElementById("publicRoot");
    if (!root) return;
    try { root.innerHTML = renderPublicBody(await api.get(`/aires/${aireId}/file-attente`)); }
    catch (e) { root.innerHTML = '<div class="public-wait">Connexion au poste central perdue — nouvelle tentative…</div>'; }
  };
  await tick();
  publicPollTimer = setInterval(tick, 1000);
}

function publicChronoTxt(c) {
  if (c.type !== "kumite" || c.chronoRestantMs == null) return null;
  // SQLite ne conserve pas l'indicateur UTC sur les DateTime : le JSON revient sans suffixe "Z" une
  // fois relu depuis la base — sans ça, `new Date(...)` l'interpréterait à tort en heure locale.
  const remainingMs = c.chronoDemarreLeUtc
    ? Math.max(0, c.chronoRestantMs - (Date.now() - new Date(c.chronoDemarreLeUtc + (c.chronoDemarreLeUtc.endsWith("Z") ? "" : "Z")).getTime()))
    : c.chronoRestantMs;
  const totalSec = Math.ceil(remainingMs / 1000);
  const mm = Math.floor(totalSec / 60), ss = totalSec % 60;
  return (mm < 10 ? "0" : "") + mm + ":" + (ss < 10 ? "0" : "") + ss;
}

function publicPhotoFrame(participantId, couleur, sizePx) {
  const img = participantId != null
    ? `<img src="/api/participants/${participantId}/photo?t=${Date.now()}" alt="" onerror="this.style.display='none';this.nextElementSibling.style.display='flex';">`
    : "";
  const sizeStyle = sizePx ? `style="width:${sizePx}px;height:${sizePx}px;"` : "";
  return `<div class="public-photo-frame ${couleur}" ${sizeStyle}>${img}<div class="public-photo-fallback" style="${participantId != null ? "display:none;" : "display:flex;"}">${giIcon(couleur, sizePx ? Math.round(sizePx * 0.42) : 100)}</div></div>`;
}
function publicEvenements(evenements, couleur) {
  const filtres = (evenements || []).filter((e) => e.couleur === (couleur === "aka" ? "Aka" : "Ao"));
  if (!filtres.length) return '<div class="public-ev-empty">—</div>';
  return filtres.slice(0, 6).map((e) => `<div class="public-ev ${e.kind}"><span class="public-ev-t">${esc(e.t)}</span><span class="public-ev-label">${esc(e.label)}</span></div>`).join("");
}

function renderPublicBody(fa) {
  const entry = fa.enCours || fa.suivant;
  if (!entry) {
    return `<div class="public-aire">${esc(fa.aireNom)}</div><div class="public-wait">En attente du prochain combat…</div>`;
  }
  const c = entry.confrontation;
  const chrono = publicChronoTxt(c);
  const isKumite = c.type === "kumite";
  const scoreHtml = isKumite
    ? `<div class="public-scores"><div class="public-score aka">${c.scoreAka ?? 0}</div><div class="public-chrono">${chrono || "—:—"}</div><div class="public-score ao">${c.scoreAo ?? 0}</div></div>`
    : `<div class="public-scores"><div class="public-score aka">${(c.votes || []).filter((v) => v.couleur === "Aka").length}</div><div class="public-chrono">VOTES</div><div class="public-score ao">${(c.votes || []).filter((v) => v.couleur === "Ao").length}</div></div>`;
  const senshuAka = c.senshuCouleur === "Aka", senshuAo = c.senshuCouleur === "Ao";
  return `
    <div class="public-topline">${esc(fa.aireNom)} · ${esc(entry.categorieNom)}${fa.enCours ? "" : '<span class="public-tag-next">PROCHAIN COMBAT</span>'}</div>
    <div class="public-competitors">
      <div class="public-competitor aka">
        ${publicPhotoFrame(c.aId, "aka")}
        <div class="public-color">AKA${senshuAka ? '<span class="public-senshu">★ SENSHU</span>' : ""}</div>
        <div class="public-name">${esc(c.aNom || "—")}</div><div class="public-club">${esc(c.aClub || "")}</div>
        ${isKumite ? `<div class="public-ev-list">${publicEvenements(c.evenements, "aka")}</div>` : ""}
      </div>
      <div class="public-competitor ao">
        ${publicPhotoFrame(c.bId, "ao")}
        <div class="public-color">AO${senshuAo ? '<span class="public-senshu">★ SENSHU</span>' : ""}</div>
        <div class="public-name">${esc(c.bNom || "—")}</div><div class="public-club">${esc(c.bClub || "")}</div>
        ${isKumite ? `<div class="public-ev-list">${publicEvenements(c.evenements, "ao")}</div>` : ""}
      </div>
    </div>
    ${scoreHtml}`;
}

/* ---- Arbitrage Kumite ---- */
async function kumiteEligibleConfs(comp) {
  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const kumiteCats = cats.filter((c) => c.discipline === "KumiteIndividuel");
  const results = await Promise.all(kumiteCats.map(async (cat) => {
    const tableau = await api.get(`/categories/${cat.id}/tableau`);
    if (!tableau) return [];
    return tableau.confrontations.filter((c) => !c.estBye && c.aId != null && c.bId != null).map((c) => ({ c, cat, tableauFormat: tableau.format }));
  }));
  return results.flat();
}

async function screenKumite() {
  const comp = activeComp();
  const all = await kumiteEligibleConfs(comp);
  const current = routeState.kumiteConfId ? all.find((x) => x.c.id === routeState.kumiteConfId) : null;

  const listHtml = all.map((x) => `<tr><td>${esc(x.cat.nom)}</td><td style="display:flex;align-items:center;gap:6px;">${giIcon("aka", 18)}${esc(x.c.aNom)} <span class="vs">vs</span> ${giIcon("ao", 18)}${esc(x.c.bNom)}</td><td>${badgeStatut(x.c.statut)}</td>
    <td><button class="btn btn-sm" data-action="select-kumite" data-id="${x.c.id}">${x.c.statut === "Termine" ? "Revoir" : "Arbitrer"}</button></td></tr>`).join("");

  let html = `<div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Arbitrage Kumite</h1></div></div>`;
  html += `<div class="card"><h3>Combats disponibles <span class="muted">${all.length}</span></h3>
    ${listHtml ? `<div class="table-wrap"><table><thead><tr><th>Catégorie</th><th>Combat</th><th>Statut</th><th></th></tr></thead><tbody>${listHtml}</tbody></table></div>` : '<p class="empty">Générez un tableau Kumite dans l\'écran Tableaux pour faire apparaître des combats ici.</p>'}
  </div>`;
  if (current) html += renderKumiteScoreboard(current.c, current.cat, current.tableauFormat, comp);
  return html;
}

function renderKumiteScoreboard(c, cat, tableauFormat, comp) {
  const enPoule = tableauFormat === "PouleUnique" || (tableauFormat === "PoulePuisElimination" && c.tour === 1);
  if (c.statut === "Termine" && timers[c.id]) { delete timers[c.id]; saveTimers(); } // plus de chrono actif à conserver une fois le combat clos
  let t = timers[c.id];
  if (!t && c.statut !== "Termine") t = timers[c.id] = { remainingMs: comp.dureeCombatDefautSec * 1000, totalSec: comp.dureeCombatDefautSec, running: false, runningSince: null };
  const remainingSec = t ? Math.ceil(chronoRemainingMs(t) / 1000) : 0;
  const mm = Math.floor(remainingSec / 60), ss = remainingSec % 60;
  const chronoTxt = (mm < 10 ? "0" : "") + mm + ":" + (ss < 10 ? "0" : "") + ss;

  const evHtml = (c.evenements || []).map((e) => {
    const lbl = e.kind === "point" ? `${e.label} (+${e.valeur})` : `Pénalité — ${e.label}`;
    return `<div class="ev"><span class="t num">${e.t}</span><span class="tag ${e.couleur === "Aka" ? "tag-aka" : "tag-ao"}">${e.couleur.toUpperCase()}</span><span>${lbl}</span></div>`;
  }).join("") || '<p class="empty" style="padding:8px 0;">Aucun événement.</p>';

  const canScore = c.statut === "EnCours";
  const showHantei = c.modeDecision === "Hantei" && c.statut !== "Termine";

  function penBtns(couleur) {
    return PENALITES.map((p) => `<button class="pen-btn ${DISQUALIFIANTES.includes(p) ? "grave" : ""}" data-action="penalite" data-conf="${c.id}" data-couleur="${couleur}" data-pen="${p}" ${canScore ? "" : "disabled"}>${PENALITE_LABEL[p]}</button>`).join("");
  }
  function panel(couleur, nm, club, score) {
    return `<div class="competitor-panel ${couleur}">
      <div class="who"><span class="color-label">${couleur === "aka" ? "Aka — Rouge" : "Ao — Bleu"}</span>${giIcon(couleur, 30)}</div>
      ${c.senshuCouleur === (couleur === "aka" ? "Aka" : "Ao") ? '<span class="senshu-badge">★ Senshu</span>' : ""}
      <div class="cname">${esc(nm)}</div><div class="cclub">${esc(club)}</div>
      <div class="score-big num">${score}</div>
      <div class="pt-btns">
        <button class="pt-btn" data-action="point" data-conf="${c.id}" data-couleur="${couleur}" data-type="ippon" ${canScore ? "" : "disabled"}>Ippon<small>${comp.pointsIppon} pts</small></button>
        <button class="pt-btn" data-action="point" data-conf="${c.id}" data-couleur="${couleur}" data-type="wazaari" ${canScore ? "" : "disabled"}>Waza-ari<small>${comp.pointsWazaAri} pts</small></button>
        <button class="pt-btn" data-action="point" data-conf="${c.id}" data-couleur="${couleur}" data-type="yuko" ${canScore ? "" : "disabled"}>Yuko<small>${comp.pointsYuko} pt</small></button>
      </div>
      <div class="pen-btns">${penBtns(couleur)}</div>
    </div>`;
  }

  let centerCtrl;
  if (c.statut === "Termine") {
    centerCtrl = `<div class="combat-meta"><b>Décision : ${decisionLabel(c.modeDecision)}</b><br>Vainqueur ${c.vainqueurCouleur === "Aka" ? '<span class="tag tag-aka">AKA</span>' : '<span class="tag tag-ao">AO</span>'}<br>Durée réelle : ${esc(dureeLabel(c.dureeReelleSec))}</div>`;
  } else if (showHantei) {
    centerCtrl = `<div class="hantei-box"><b>Hantei</b><div class="hint">Égalité en fin de temps — décision arbitrale (drapeaux)</div>
      <div style="display:flex;gap:8px;justify-content:center;margin-top:10px;">
      <button class="btn" style="background:var(--aka);color:#fff;border:none;" data-action="hantei" data-conf="${c.id}" data-couleur="aka">Drapeau Aka</button>
      <button class="btn" style="background:var(--ao);color:#fff;border:none;" data-action="hantei" data-conf="${c.id}" data-couleur="ao">Drapeau Ao</button>
      </div></div>`;
  } else {
    centerCtrl = `<div class="chrono-ctrl">
      ${t.running ? `<button class="btn" data-action="chrono-pause" data-conf="${c.id}">⏸ Pause</button>` : `<button class="btn btn-primary" data-action="chrono-start" data-conf="${c.id}">▶ ${c.statut === "EnAttente" ? "Démarrer" : "Reprendre"}</button>`}
      <button class="btn btn-ghost" data-action="chrono-reset" data-conf="${c.id}">↺ Réinit.</button>
    </div>
    <div class="duree-ctrl">
      <button class="btn btn-sm btn-ghost" data-action="chrono-duree" data-conf="${c.id}" data-delta="-30" ${t.running ? "disabled" : ""}>−30s</button>
      <span class="hint">Durée du match : ${Math.floor(t.totalSec / 60)} min ${t.totalSec % 60 ? (t.totalSec % 60) + " s" : ""}</span>
      <button class="btn btn-sm btn-ghost" data-action="chrono-duree" data-conf="${c.id}" data-delta="30" ${t.running ? "disabled" : ""}>+30s</button>
    </div>
    <div class="combat-meta">${enPoule ? "Phase de poule — Senshu actif" : "Élimination directe"}<br>Écart de victoire : ${comp.ecartVictoire} pts</div>`;
  }

  return `<div class="card">
    <h3>${esc(cat.nom)} <span class="muted">Combat #${c.id} — Tour ${c.tour}${c.moitie ? ` · Moitié ${c.moitie}` : ""}${c.estRepechage ? " · Repêchage" : ""}</span></h3>
    <div id="chronoDisplay" class="chrono ${remainingSec <= 15 && c.statut !== "Termine" ? "low" : ""}" style="margin-bottom:6px;">${chronoTxt}</div>
    <div class="combat-grid">
      ${panel("aka", c.aNom, c.aClub, c.scoreAka)}
      <div class="center-col">${centerCtrl}</div>
      ${panel("ao", c.bNom, c.bClub, c.scoreAo)}
    </div>
    <h4 style="margin:16px 0 6px;font-size:13px;">Historique horodaté</h4>
    <div class="event-log">${evHtml}</div>
  </div>`;
}

/* Boucle de chrono — 100 ms, purement côté client ; le serveur n'apprend le temps écoulé qu'au
   moment d'une action (point/pénalité/fin de temps) via tempsEcouleSec. Le temps restant se calcule
   à chaque tick à partir de l'horloge murale (chronoRemainingMs), pas d'un décrément cumulé : un
   tick en retard (onglet en arrière-plan, machine chargée) ne fait donc dériver aucun combat. */
setInterval(async () => {
  let any = false;
  for (const id of Object.keys(timers)) {
    const t = timers[id];
    if (t.running) {
      any = true;
      if (chronoRemainingMs(t) <= 0) {
        t.remainingMs = 0; t.running = false; t.runningSince = null;
        saveTimers();
        await safe(() => api.post(`/combats/${id}/fin-de-temps`, { tempsEcouleSec: t.totalSec }));
        if (currentRoute === "kumite") await renderApp();
      }
    }
  }
  if (any && currentRoute === "kumite") liveUpdateChrono();
}, 100);

function liveUpdateChrono() {
  const confId = routeState.kumiteConfId;
  if (!confId) return;
  const t = timers[confId];
  if (!t) return;
  const el = document.getElementById("chronoDisplay");
  if (!el) return;
  const remainingSec = Math.ceil(chronoRemainingMs(t) / 1000);
  const mm = Math.floor(remainingSec / 60), ss = remainingSec % 60;
  el.textContent = (mm < 10 ? "0" : "") + mm + ":" + (ss < 10 ? "0" : "") + ss;
  el.classList.toggle("low", remainingSec <= 15);
}

/* ---- Jury Kata ---- */
async function kataEligibleConfs(comp) {
  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const kataCats = cats.filter((c) => c.discipline !== "KumiteIndividuel");
  const results = await Promise.all(kataCats.map(async (cat) => {
    const tableau = await api.get(`/categories/${cat.id}/tableau`);
    if (!tableau) return [];
    return tableau.confrontations.filter((c) => !c.estBye && c.aId != null && c.bId != null).map((c) => ({ c, cat }));
  }));
  return results.flat();
}

async function screenKata() {
  const comp = activeComp();
  const all = await kataEligibleConfs(comp);
  const current = routeState.kataConfId ? all.find((x) => x.c.id === routeState.kataConfId) : null;

  const listHtml = all.map((x) => `<tr><td>${esc(x.cat.nom)}</td><td style="display:flex;align-items:center;gap:6px;">${giIcon("aka", 18)}${esc(x.c.aNom)} <span class="vs">vs</span> ${giIcon("ao", 18)}${esc(x.c.bNom)}</td><td>${badgeStatut(x.c.statut)}</td>
    <td><button class="btn btn-sm" data-action="select-kata" data-id="${x.c.id}">${x.c.statut === "Termine" ? "Revoir" : "Juger"}</button></td></tr>`).join("");

  let html = `<div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Jury Kata</h1></div></div>`;
  html += `<div class="card"><h3>Confrontations disponibles <span class="muted">${all.length}</span></h3>
    ${listHtml ? `<div class="table-wrap"><table><thead><tr><th>Catégorie</th><th>Confrontation</th><th>Statut</th><th></th></tr></thead><tbody>${listHtml}</tbody></table></div>` : '<p class="empty">Générez un tableau Kata dans l\'écran Tableaux pour faire apparaître des confrontations ici.</p>'}
  </div>`;
  if (current) html += await renderKataPanel(current.c, current.cat);
  return html;
}

async function renderKataPanel(c, cat) {
  const katas = await api.get("/katas");
  const kataOpts = (colorNom) => katas.map((k) => `<option value="${k.id}" ${k.nom === colorNom ? "selected" : ""}>${esc(k.nom)}</option>`).join("");
  const nbJuges = c.nbJuges;
  let judgeCards = "";
  for (let j = 1; j <= nbJuges; j++) {
    const v = (c.votes || []).find((x) => x.jugeNumero === j);
    judgeCards += `<div class="juge-card"><div class="jn">Juge ${j}</div><div class="flag-btns">
      <button class="flag-btn aka ${v && v.couleur === "Aka" ? "sel" : ""}" data-action="vote-kata" data-conf="${c.id}" data-juge="${j}" data-couleur="aka" ${c.statut === "Termine" ? "disabled" : ""}>AKA</button>
      <button class="flag-btn ao ${v && v.couleur === "Ao" ? "sel" : ""}" data-action="vote-kata" data-conf="${c.id}" data-juge="${j}" data-couleur="ao" ${c.statut === "Termine" ? "disabled" : ""}>AO</button>
      </div></div>`;
  }
  const vAka = (c.votes || []).filter((v) => v.couleur === "Aka").length, vAo = (c.votes || []).length - vAka;

  const resultBlock = c.statut === "Termine"
    ? `<div class="combat-meta" style="margin-top:10px;"><b>Vainqueur : ${c.vainqueurCouleur === "Aka" ? '<span class="tag tag-aka">AKA</span>' : '<span class="tag tag-ao">AO</span>'}</b> — ${vAka} drapeaux Aka / ${vAo} drapeaux Ao</div>`
    : `<div style="margin-top:12px;text-align:center;"><button class="btn btn-primary" data-action="valider-kata" data-conf="${c.id}">Valider les votes (${(c.votes || []).length}/${nbJuges})</button></div>`;

  return `<div class="card">
    <h3>${esc(cat.nom)} <span class="muted">Confrontation #${c.id} — Tour ${c.tour}${c.moitie ? ` · Moitié ${c.moitie}` : ""}${c.estRepechage ? " · Repêchage" : ""}</span></h3>
    <div class="grid grid-2" style="margin-bottom:14px;">
      <div class="field" style="margin-bottom:0;"><label style="display:flex;align-items:center;gap:6px;">${giIcon("aka", 22)}<span class="tag tag-aka">AKA</span> ${esc(c.aNom)} — kata exécuté</label>
        <select data-action="set-kata" data-conf="${c.id}" data-couleur="aka" ${c.statut === "Termine" ? "disabled" : ""}><option value="">— sélectionner —</option>${kataOpts(c.kata1Nom)}</select></div>
      <div class="field" style="margin-bottom:0;"><label style="display:flex;align-items:center;gap:6px;">${giIcon("ao", 22)}<span class="tag tag-ao">AO</span> ${esc(c.bNom)} — kata exécuté</label>
        <select data-action="set-kata" data-conf="${c.id}" data-couleur="ao" ${c.statut === "Termine" ? "disabled" : ""}><option value="">— sélectionner —</option>${kataOpts(c.kata2Nom)}</select></div>
    </div>
    <div class="hint" style="margin-bottom:8px;">Saisi par le juge central au moment de la confrontation.</div>
    <h4 style="font-size:13px;margin-bottom:8px;">Vote des juges — système de drapeaux</h4>
    <div class="juge-grid">${judgeCards}</div>
    ${resultBlock}
  </div>`;
}

/* ---- Résultats ---- */
async function screenResultats() {
  const comp = activeComp();
  const cats = await api.get(`/competitions/${comp.id}/categories`);
  const blocks = await Promise.all(cats.map(async (cat) => {
    const tableau = await api.get(`/categories/${cat.id}/tableau`);
    if (!tableau) return `<div class="card"><h3>${esc(cat.nom)}</h3><p class="empty">Aucun tableau généré.</p></div>`;
    const classement = await api.get(`/categories/${cat.id}/classement`);
    const cards = classement.filter((c) => c.medaille).map((c) => medalCard(medailleClass(c.medaille), medailleLabel(c.medaille), c.nom)).join("");
    return `<div class="card"><h3>${esc(cat.nom)} <span class="muted">${FORMAT_LABEL[tableau.format]}</span></h3>
      ${cards ? `<div class="podium-row">${cards}</div>` : '<p class="empty">Compétition en cours — podium non encore déterminé.</p>'}
    </div>`;
  }));

  return `
  <div class="topbar"><div><div class="crumb">${esc(comp.nom)}</div><h1>Résultats &amp; exports</h1></div></div>
  <div class="card"><h3>Exports</h3>
    <p class="hint" style="margin-bottom:10px;">Rapports PDF détaillés (cahier 5.6) et export Excel des podiums, générés côté serveur.</p>
    <div style="display:flex;gap:10px;flex-wrap:wrap;">
      <a class="btn btn-primary" href="/api/competitions/${comp.id}/export/kumite" target="_blank" rel="noopener">Rapport Kumite (PDF)</a>
      <a class="btn btn-primary" href="/api/competitions/${comp.id}/export/kata" target="_blank" rel="noopener">Rapport Kata (PDF)</a>
      <a class="btn" href="/api/competitions/${comp.id}/export/excel" target="_blank" rel="noopener">Résultats (Excel)</a>
    </div>
  </div>
  ${blocks.join("")}`;
}

/* ---- Audit ---- */
async function screenAudit() {
  const logs = await api.get("/audit");
  const rows = logs.map((a) => `<tr><td class="num" style="white-space:nowrap;color:var(--ink-soft);">${new Date(a.horodatage).toLocaleString("fr-FR")}</td><td>${esc(a.entiteType)} #${a.entiteId} — ${esc(a.action)}${a.nouvelleValeur ? " : " + esc(a.nouvelleValeur) : ""}</td><td>${esc(a.utilisateur || "—")}</td></tr>`).join("");
  return `<div class="topbar"><div><div class="crumb">Traçabilité</div><h1>Journal d'audit</h1></div></div>
  <div class="card"><h3>Modifications de score et décisions <span class="muted">${logs.length} entrées</span></h3>
    ${rows ? `<div class="table-wrap"><table><thead><tr><th>Horodatage</th><th>Événement</th><th>Opérateur</th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucun événement enregistré.</p>'}
  </div>`;
}

/* ---- Sécurité ---- */
async function screenSecurite() {
  const configure = !!(securiteCache && securiteCache.codeConfigure);
  return `<div class="topbar"><div><div class="crumb">Plateforme</div><h1>Sécurité</h1></div></div>

  <div class="card"><h3>Nom d'opérateur (ce poste)</h3>
    <p class="hint" style="margin-bottom:10px;">Enregistré dans le journal d'audit pour chaque action effectuée depuis cet appareil.</p>
    <form id="form-operateur" class="row-inline">
      <div class="field" style="flex:1;margin-bottom:0;"><input type="text" name="nom" placeholder="Ex. Jean" value="${esc(operateurNom)}"></div>
      <button class="btn btn-primary btn-sm" type="submit">Enregistrer</button>
    </form>
  </div>

  <div class="card"><h3>Code administrateur</h3>
    ${configure
      ? '<p class="hint" style="margin-bottom:10px;">Un code est configuré : il sera demandé pour réinitialiser toutes les données de la plateforme.</p>'
      : '<p class="error" style="margin-bottom:10px;">Aucun code configuré — n\'importe quel poste sur le réseau local peut actuellement réinitialiser toutes les données. Configurez-en un ci-dessous.</p>'}
    <form id="form-code-admin" class="grid grid-3">
      ${configure ? field("Code actuel", "ancienCode", "password", "", true) : ""}
      ${field(configure ? "Nouveau code" : "Code (min. 4 caractères)", "nouveauCode", "password", "", true)}
      <div></div>
      <div style="grid-column:1/-1"><button class="btn btn-primary btn-sm" type="submit">${configure ? "Changer le code" : "Configurer le code"}</button></div>
    </form>
  </div>

  ${await renderSauvegardes()}`;
}

/* ---- Sauvegardes ---- */
function fmtTaille(octets) {
  if (octets < 1024) return octets + " o";
  if (octets < 1024 * 1024) return (octets / 1024).toFixed(0) + " Ko";
  return (octets / (1024 * 1024)).toFixed(1) + " Mo";
}

async function renderSauvegardes() {
  const sauvegardes = await api.get("/sauvegardes").catch(() => []);
  const rows = sauvegardes.map((s) => `<tr><td>${esc(s.nom)}</td><td>${new Date(s.creeLe).toLocaleString("fr-FR")}</td><td>${fmtTaille(s.tailleOctets)}</td>
    <td><button class="btn btn-sm btn-danger" data-action="restaurer-sauvegarde" data-nom="${esc(s.nom)}">Restaurer</button></td></tr>`).join("");

  return `<div class="card"><h3>Sauvegardes</h3>
    <p class="hint" style="margin-bottom:10px;">Une sauvegarde automatique est prise régulièrement pendant que l'application tourne. Téléchargez-en une sur une clé USB ou un disque externe pour la conserver hors de ce poste.</p>
    <a class="btn btn-primary btn-sm" href="/api/sauvegardes/telecharger" download>Télécharger une sauvegarde maintenant</a>
    ${rows ? `<div class="table-wrap" style="margin-top:14px;"><table><thead><tr><th>Fichier</th><th>Créée le</th><th>Taille</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>` : '<p class="empty">Aucune sauvegarde pour l\'instant.</p>'}
    <div style="margin-top:16px;border-top:1px solid var(--line);padding-top:14px;">
      <h4 style="font-size:13px;margin-bottom:8px;">Importer une sauvegarde externe</h4>
      <p class="error" style="margin-bottom:10px;">Remplace immédiatement toutes les données actuelles (une sauvegarde de l'état présent est prise automatiquement avant).</p>
      <form id="form-importer-sauvegarde" class="row-inline">
        <input type="file" name="fichier" accept=".db" required>
        <button class="btn btn-sm btn-danger" type="submit">Importer et restaurer</button>
      </form>
    </div>
  </div>`;
}

/* ---- Démo ---- */
async function seedDemo() {
  if (competitionsCache.length && !confirm("Ajouter un jeu de données de démonstration à côté des données existantes ?")) return;
  const comp = await api.post("/competitions", { nom: "Coupe Karate Scoring", date: new Date(Date.now() + 21 * 86400000).toISOString().slice(0, 10), lieu: "Gymnase municipal", niveau: "Club" });
  setActiveCompetition(comp.id);

  const clubs = ["Étoile du Sud", "Dragon Club", "Sakura Dojo", "Phénix Karaté"];
  const prenoms = ["Jean", "Marie", "Paul", "Sarah", "Eric", "Fatima", "David", "Grace", "Junior", "Aicha", "Boris", "Nadia"];
  const noms = ["Mbarga", "Fotso", "Talla", "Essomba", "Kamdem", "Njoya", "Owona", "Tchoua", "Simo", "Kengne"];
  const rand = (arr) => arr[Math.floor(Math.random() * arr.length)];

  const participants = [];
  for (let i = 0; i < 26; i++) {
    const p = await api.post("/participants", {
      nom: rand(noms), prenom: rand(prenoms), club: rand(clubs), grade: GRADES[Math.floor(Math.random() * GRADES.length)],
      dateNaissance: `${1996 + Math.floor(Math.random() * 18)}-0${1 + Math.floor(Math.random() * 9)}-1${Math.floor(Math.random() * 8)}`,
      licence: "LIC-" + (1000 + i), poids: 50 + Math.floor(Math.random() * 45),
    });
    participants.push(p);
  }

  async function makeCat(nom, discipline, n) {
    const cat = await api.post(`/competitions/${comp.id}/categories`, { nom, discipline, sexe: "Mixte", ageMin: 14, ageMax: 99, gradeMin: null });
    const pool = participants.slice().sort(() => Math.random() - 0.5).slice(0, n);
    for (const p of pool) {
      const current = await api.get(`/participants/${p.id}/inscriptions`);
      await api.put(`/participants/${p.id}/inscriptions`, { categorieIds: current.concat([cat.id]) });
    }
    return cat;
  }
  await makeCat("Kumite Seniors -75kg", "KumiteIndividuel", 10);
  await makeCat("Kumite Cadets Mixte", "KumiteIndividuel", 6);
  await makeCat("Kumite Vétérans", "KumiteIndividuel", 2);
  await makeCat("Kata Individuel Seniors", "KataIndividuel", 4);
  await makeCat("Kata Individuel Juniors", "KataIndividuel", 9);

  const eqCat = await api.post(`/competitions/${comp.id}/categories`, { nom: "Kata Équipes Seniors", discipline: "KataEquipe", sexe: "Mixte", ageMin: 16, ageMax: 99, gradeMin: null });
  const eqPool = participants.slice(18, 27);
  for (let e = 0; e < 3; e++) {
    const membreIds = eqPool.slice(e * 3, e * 3 + 3).map((p) => p.id);
    await api.post(`/competitions/${comp.id}/equipes`, { nom: `Équipe ${rand(clubs)} ${e + 1}`, club: rand(clubs), membreIds, categorieId: eqCat.id });
  }

  toast("Données de démonstration chargées.");
  currentRoute = "competitions";
  await renderApp();
}

/* ================= Event wiring (délégation, attachée une seule fois) ================= */
const appEl = document.getElementById("app");

appEl.addEventListener("click", async (e) => {
  const navEl = e.target.closest("[data-nav]");
  if (navEl) { e.preventDefault(); currentRoute = navEl.dataset.nav; sidebarOpen = false; await renderApp(); return; }

  const btn = e.target.closest("[data-action]");
  if (!btn) return;
  e.preventDefault();
  const a = btn.dataset.action;

  if (a === "toggle-sidebar") { sidebarOpen = !sidebarOpen; await renderApp(); return; }
  if (a === "close-sidebar") { sidebarOpen = false; await renderApp(); return; }
  if (a === "seed-demo") { await safe(seedDemo); return; }
  if (a === "reset-all") {
    if (!confirm("Réinitialiser toutes les données de la plateforme ? Cette action supprime définitivement compétitions, participants et résultats.")) return;
    const code = demanderCodeAdminSiConfigure();
    if (code === undefined) return;
    if (await safe(() => api.post("/admin/reset", { code }))) { setActiveCompetition(null); routeState = {}; timers = {}; currentRoute = "accueil"; }
    await renderApp(); return;
  }
  if (a === "restaurer-sauvegarde") {
    if (!confirm(`Restaurer « ${btn.dataset.nom} » ? Toutes les données actuelles seront remplacées (une sauvegarde de l'état présent est prise avant).`)) return;
    const code = demanderCodeAdminSiConfigure();
    if (code === undefined) return;
    if (await safe(() => api.post(`/sauvegardes/${encodeURIComponent(btn.dataset.nom)}/restaurer`, { code }))) { setActiveCompetition(null); routeState = {}; timers = {}; }
    await renderApp(); return;
  }
  if (a === "activer-comp") { setActiveCompetition(Number(btn.dataset.id)); await renderApp(); return; }
  if (a === "edit-comp") { routeState.editCompId = Number(btn.dataset.id); await renderApp(); return; }
  if (a === "close-comp-reglages") { routeState.editCompId = null; await renderApp(); return; }
  if (a === "del-kata") { await safe(() => api.del(`/katas/${btn.dataset.id}`)); await renderApp(); return; }
  if (a === "del-cat") {
    if (!confirm("Supprimer cette catégorie et ses inscriptions/tableaux ?")) return;
    await safe(() => api.del(`/categories/${btn.dataset.id}`)); await renderApp(); return;
  }
  if (a === "inscrire") { routeState.inscrireId = Number(btn.dataset.id); await renderApp(); return; }
  if (a === "close-inscrire") { routeState.inscrireId = null; await renderApp(); return; }
  if (a === "del-part") { await safe(() => api.del(`/participants/${btn.dataset.id}`)); await renderApp(); return; }
  if (a === "del-equipe") { await safe(() => api.del(`/equipes/${btn.dataset.id}`)); await renderApp(); return; }
  if (a === "print-tableau") { window.print(); return; }
  if (a === "regen-tableau") {
    if (!confirm("Régénérer ce tableau ? Les résultats déjà saisis seront perdus.")) return;
    await safe(() => api.del(`/tableaux/${btn.dataset.tab}`)); await renderApp(); return;
  }
  if (a === "gen-elim-apres-poules") { await safe(() => api.post(`/tableaux/${btn.dataset.tab}/phase-elimination`)); await renderApp(); return; }
  if (a === "select-tatami") { routeState.tatamiAireId = Number(btn.dataset.id); await renderApp(); return; }
  if (a === "copy-tatami-link" || a === "copy-public-link") {
    const kind = a === "copy-tatami-link" ? "tatami" : "public";
    const url = location.origin + "#" + kind + "/" + btn.dataset.id;
    try { await navigator.clipboard.writeText(url); toast("Lien copié : " + url); }
    catch (e) { prompt("Copiez ce lien :", url); }
    return;
  }
  if (a === "save-aire-nom") {
    const input = document.querySelector(`.aire-nom-input[data-id="${btn.dataset.id}"]`);
    await safe(() => api.put(`/aires/${btn.dataset.id}`, { nom: input.value }));
    await renderApp(); return;
  }
  if (a === "del-aire") {
    if (!confirm("Supprimer cette aire ? Les tableaux qui y sont assignés seront désassignés.")) return;
    if (await safe(() => api.del(`/aires/${btn.dataset.id}`)) && routeState.tatamiAireId === Number(btn.dataset.id)) routeState.tatamiAireId = null;
    await renderApp(); return;
  }
  if (a === "goto-confrontation") {
    if (btn.dataset.type === "kumite") { routeState.kumiteConfId = Number(btn.dataset.id); currentRoute = "kumite"; }
    else { routeState.kataConfId = Number(btn.dataset.id); currentRoute = "kata"; }
    await renderApp(); return;
  }
  if (a === "select-kumite") { routeState.kumiteConfId = Number(btn.dataset.id); await renderApp(); return; }
  if (a === "select-kata") { routeState.kataConfId = Number(btn.dataset.id); await renderApp(); return; }

  if (a === "chrono-start") {
    const comp = activeComp();
    let t = timers[btn.dataset.conf];
    if (!t) t = timers[btn.dataset.conf] = { remainingMs: comp.dureeCombatDefautSec * 1000, totalSec: comp.dureeCombatDefautSec, running: false, runningSince: null };
    if (!(await safe(() => api.post(`/combats/${btn.dataset.conf}/demarrer`)))) return;
    t.running = true; t.runningSince = Date.now(); saveTimers();
    syncChronoServeur(btn.dataset.conf, t);
    await renderApp(); return;
  }
  if (a === "chrono-pause") {
    const t = timers[btn.dataset.conf];
    t.remainingMs = chronoRemainingMs(t); t.running = false; t.runningSince = null;
    saveTimers();
    syncChronoServeur(btn.dataset.conf, t);
    await renderApp(); return;
  }
  if (a === "chrono-reset") {
    const comp = activeComp();
    const totalSec = timers[btn.dataset.conf] ? timers[btn.dataset.conf].totalSec : comp.dureeCombatDefautSec;
    timers[btn.dataset.conf] = { remainingMs: totalSec * 1000, totalSec, running: false, runningSince: null };
    saveTimers();
    syncChronoServeur(btn.dataset.conf, timers[btn.dataset.conf]);
    await renderApp(); return;
  }
  if (a === "chrono-duree") {
    const t = timers[btn.dataset.conf];
    if (!t || t.running) return;
    const delta = Number(btn.dataset.delta);
    const nextTotal = Math.max(30, t.totalSec + delta);
    t.remainingMs = Math.max(0, t.remainingMs + (nextTotal - t.totalSec) * 1000);
    t.totalSec = nextTotal;
    saveTimers();
    syncChronoServeur(btn.dataset.conf, t);
    await renderApp(); return;
  }
  if (a === "point") {
    const t = timers[btn.dataset.conf];
    const tempsEcouleSec = t ? Math.max(0, t.totalSec - Math.ceil(chronoRemainingMs(t) / 1000)) : 0;
    await safe(() => api.post(`/combats/${btn.dataset.conf}/point`, { couleur: btn.dataset.couleur, type: btn.dataset.type, tempsEcouleSec }));
    await renderApp(); return;
  }
  if (a === "penalite") {
    const t = timers[btn.dataset.conf];
    const tempsEcouleSec = t ? Math.max(0, t.totalSec - Math.ceil(chronoRemainingMs(t) / 1000)) : 0;
    await safe(() => api.post(`/combats/${btn.dataset.conf}/penalite`, { couleur: btn.dataset.couleur, penalite: btn.dataset.pen, tempsEcouleSec }));
    await renderApp(); return;
  }
  if (a === "hantei") { await safe(() => api.post(`/combats/${btn.dataset.conf}/hantei`, { couleur: btn.dataset.couleur })); await renderApp(); return; }

  if (a === "vote-kata") {
    await safe(() => api.post(`/kata-confrontations/${btn.dataset.conf}/vote`, { jugeNumero: Number(btn.dataset.juge), couleur: btn.dataset.couleur }));
    await renderApp(); return;
  }
  if (a === "valider-kata") { await safe(() => api.post(`/kata-confrontations/${btn.dataset.conf}/valider`)); await renderApp(); return; }
});

appEl.addEventListener("change", async (e) => {
  if (e.target.id === "select-tableau-cat") { routeState.tableauCatId = Number(e.target.value); await renderApp(); return; }
  if (e.target.dataset.action === "assign-aire") {
    await safe(() => api.post(`/tableaux/${e.target.dataset.tab}/aire`, { aireId: e.target.value ? Number(e.target.value) : null }));
    await renderApp(); return;
  }
  if (e.target.dataset.action === "set-kata") {
    const kataId = Number(e.target.value);
    if (!kataId) return;
    await safe(() => api.post(`/kata-confrontations/${e.target.dataset.conf}/kata`, { couleur: e.target.dataset.couleur, kataId }));
    await renderApp();
  }
  if (e.target.dataset.action === "upload-photo" || e.target.dataset.action === "upload-logo") {
    const fichier = e.target.files && e.target.files[0];
    e.target.value = "";
    if (!fichier) return;
    const kind = e.target.dataset.action === "upload-photo" ? "participants" : "clubs";
    const fd = new FormData();
    fd.append("fichier", fichier);
    if (await safe(() => apiUpload(`/${kind}/${e.target.dataset.id}/${kind === "participants" ? "photo" : "logo"}`, fd))) toast("Image importée.");
    await renderApp();
  }
});

appEl.addEventListener("submit", async (e) => {
  const form = e.target;
  if (!form.id) return;
  e.preventDefault();
  const f = new FormData(form);

  if (form.id === "form-competition") {
    await safe(() => api.post("/competitions", { nom: f.get("nom"), date: f.get("date"), lieu: f.get("lieu"), niveau: f.get("niveau") }));
  } else if (form.id === "form-reglages") {
    await safe(() => api.put(`/competitions/${form.dataset.id}/reglages`, {
      dureeCombatDefautSec: Number(f.get("dureeCombatDefautSec")), ecartVictoire: Number(f.get("ecartVictoire")),
      nbJugesKataDefaut: Number(f.get("nbJugesKataDefaut")), seuilPouleUnique: Number(f.get("seuilPouleUnique")), seuilPoulePuisElimination: Number(f.get("seuilPoulePuisElimination")),
    }));
  } else if (form.id === "form-add-kata") {
    const v = (f.get("kata") || "").trim();
    if (v) await safe(() => api.post("/katas", { nom: v }));
  } else if (form.id === "form-categorie") {
    await safe(() => api.post(`/competitions/${form.dataset.comp}/categories`, {
      nom: f.get("nom"), discipline: f.get("discipline"), sexe: f.get("sexe"),
      ageMin: Number(f.get("ageMin") || 0), ageMax: Number(f.get("ageMax") || 99), gradeMin: f.get("gradeMin") || null,
    }));
  } else if (form.id === "form-participant") {
    await safe(() => api.post("/participants", {
      nom: f.get("nom"), prenom: f.get("prenom"), club: f.get("club"), grade: f.get("grade") || null,
      dateNaissance: f.get("dateNaissance") || null, licence: f.get("licence") || null, poids: f.get("poids") ? Number(f.get("poids")) : null,
    }));
  } else if (form.id === "form-inscription") {
    const checked = [...form.querySelectorAll('input[name="cat"]:checked')].map((c) => Number(c.value));
    if (await safe(() => api.put(`/participants/${form.dataset.part}/inscriptions`, { categorieIds: checked }))) { routeState.inscrireId = null; toast("Inscriptions mises à jour."); }
  } else if (form.id === "form-equipe") {
    const membreIds = [...form.querySelectorAll('input[name="membre"]:checked')].map((c) => Number(c.value));
    await safe(() => api.post(`/competitions/${form.dataset.comp}/equipes`, { nom: f.get("nom"), club: f.get("club"), membreIds, categorieId: f.get("categorieId") ? Number(f.get("categorieId")) : null }));
  } else if (form.id === "form-gen-tableau") {
    await safe(() => api.post(`/categories/${form.dataset.cat}/tableau/generer`, { formatForce: f.get("force") || null }));
  } else if (form.id === "form-aire") {
    const v = (f.get("nom") || "").trim();
    if (v) await safe(() => api.post(`/competitions/${form.dataset.comp}/aires`, { nom: v }));
  } else if (form.id === "form-operateur") {
    operateurNom = (f.get("nom") || "").trim();
    localStorage.setItem("karate_scoring_operateur", operateurNom);
    toast("Nom d'opérateur enregistré.");
  } else if (form.id === "form-code-admin") {
    const ok = await safe(() => api.post("/securite/code", { nouveauCode: f.get("nouveauCode"), ancienCode: f.get("ancienCode") || null }));
    if (ok) { securiteCache = await api.get("/securite").catch(() => securiteCache); toast("Code administrateur enregistré."); }
  } else if (form.id === "form-importer-sauvegarde") {
    const fichier = f.get("fichier");
    if (!fichier || !fichier.size) { toast("Sélectionnez un fichier de sauvegarde.", true); await renderApp(); return; }
    if (!confirm(`Importer « ${fichier.name} » ? Toutes les données actuelles seront remplacées (une sauvegarde de l'état présent est prise avant).`)) { await renderApp(); return; }
    const code = demanderCodeAdminSiConfigure();
    if (code === undefined) { await renderApp(); return; }
    if (code) f.set("code", code);
    if (await safe(() => apiUpload("/sauvegardes/importer", f))) { setActiveCompetition(null); routeState = {}; timers = {}; toast("Sauvegarde importée."); }
  }
  await renderApp();
});

parseHashRoute();
renderApp();
})();
