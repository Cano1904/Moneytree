// SHORT LEGS – rules, ported 1:1 from Unity/Assets/ShortLegs/Scripts/Core (ShrinkMatrix + DeceptionEngine).
(function (global) {
  "use strict";

  const ANY = -1;

  const ShrinkMatrix = {
    SHRINK_PER_LIE: 0.25,
    PITCH_PER_LIE: 0.15,
    legScale: (lies) => Math.max(0, 1 - 0.25 * Math.max(0, lies)),
    speed: (base, lies) => base * ShrinkMatrix.legScale(lies),
    canSprint: (lies) => lies < 1,
    canStepUp: (lies) => lies < 2,
    canJump: (lies) => lies < 3,
    mustCrawl: (lies) => lies >= 3,
    voicePitch: (lies) => 1 + 0.15 * Math.max(0, lies),
    isExposed: (lies, max) => lies >= max,
  };

  // Exclusive predicates hold one value per subject and time slot ("was in" one room at a time).
  const EXCLUSIVE = { wasIn: true, wasAsleep: true };

  function claim(subject, predicate, value, time = ANY, negated = false) {
    return { subject, predicate, value, time, negated };
  }

  // True when `c` cannot be true if evidence `e` is true. Unknowns are never lies.
  function contradicts(c, e) {
    if (c.predicate !== e.predicate) return false;
    if (c.time !== ANY && e.time !== ANY && c.time !== e.time) return false;
    if (!(c.subject === e.subject || c.subject === ANY)) return false;
    if (c.negated) return !e.negated && c.value === e.value;
    if (c.subject === ANY) return false;
    if (e.negated) return c.value === e.value;
    return !!EXCLUSIVE[c.predicate] && c.value !== e.value;
  }

  class DeceptionEngine {
    constructor(clues, autoResolve = true, threshold = 1) {
      this.clues = new Map();
      this.discovered = new Set();
      this.log = [];
      this.autoResolve = autoResolve;
      this.threshold = threshold;
      (clues || []).forEach((c) => this.register(c));
    }

    register(clue) {
      if (this.clues.has(clue.id)) throw new Error("duplicate clue " + clue.id);
      clue.facts = clue.facts || [];
      this.clues.set(clue.id, clue);
      for (const r of this.log) if (!r.exposed) r.latent = this._hidden(r.statement.claim);
    }

    isDiscovered(id) { return this.discovered.has(id); }

    genuineCount() {
      let n = 0;
      for (const id of this.discovered) { const c = this.clues.get(id); if (c.isTrue && !c.planted) n++; }
      return n;
    }

    // Returns { record, detection|null }
    submit(statement) {
      const record = { index: this.log.length, statement, deception: 0, latent: false, exposed: false, byClue: null, framed: false };
      this.log.push(record);
      const worst = this._evaluate(record);
      const detection = record.deception >= this.threshold ? this._expose(record, worst) : null;
      return { record, detection };
    }

    discover(id) {
      const out = [];
      if (!this.clues.has(id) || this.discovered.has(id)) return out;
      this.discovered.add(id);
      for (const r of this.log) {
        if (r.exposed) continue;
        const worst = this._evaluate(r);
        if (this.autoResolve && r.deception >= this.threshold) out.push(this._expose(r, worst));
      }
      return out;
    }

    // Story Mode: bind one discovered clue to one statement.
    present(index, clueId) {
      const r = this.log[index];
      const clue = this.clues.get(clueId);
      if (!r || r.exposed || !clue || !this.discovered.has(clueId)) return null;
      let w = 0;
      for (const f of clue.facts) if (contradicts(r.statement.claim, f.fact)) w += f.weight || 1;
      if (w < this.threshold) return null;
      r.deception = Math.max(r.deception, w);
      return this._expose(r, clueId);
    }

    _evaluate(r) {
      let index = 0, worst = 0, worstClue = null;
      for (const id of this.discovered) {
        const c = this.clues.get(id);
        let w = 0;
        for (const f of c.facts) if (contradicts(r.statement.claim, f.fact)) w += f.weight || 1;
        index += w;
        if (w > worst || (w > 0 && w === worst && !c.planted)) { worst = w; worstClue = id; }
      }
      r.deception = index;
      r.latent = index < this.threshold && this._hidden(r.statement.claim);
      return worstClue;
    }

    _hidden(c) {
      for (const [id, clue] of this.clues) {
        if (this.discovered.has(id)) continue;
        for (const f of clue.facts) if (contradicts(c, f.fact)) return true;
      }
      return false;
    }

    _expose(r, clueId) {
      r.exposed = true;
      r.latent = false;
      r.byClue = clueId;
      let genuine = 0;
      for (const id of this.discovered) {
        const c = this.clues.get(id);
        if (c.planted) continue;
        for (const f of c.facts) if (contradicts(r.statement.claim, f.fact)) genuine += f.weight || 1;
      }
      r.framed = genuine < this.threshold;
      return { record: r, clueId, speaker: r.statement.speaker };
    }
  }

  global.SL = Object.assign(global.SL || {}, { ANY, ShrinkMatrix, DeceptionEngine, claim, contradicts });
})(window);
