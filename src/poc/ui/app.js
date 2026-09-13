'use strict';

(function () {
  const form = document.getElementById('ask-form');
  const askButton = document.getElementById('ask-button');
  const statusEl = document.getElementById('status');
  const badge = document.getElementById('mode-badge');
  const banner = document.getElementById('banner');
  const pipelineEl = document.getElementById('pipeline');
  const schemaEl = document.getElementById('schema');
  const messagesEl = document.getElementById('messages');
  const resultsEl = document.getElementById('results');
  const resultCount = document.getElementById('result-count');

  let schemaViews = { compact: null, flattened: null };
  let activeSchema = 'compact';

  function setBadge(mode, text) {
    badge.className = 'badge ' + (mode === 'live' ? 'badge-live' : mode === 'demo' ? 'badge-demo' : 'badge-muted');
    badge.textContent = text;
  }

  function showBanner(message, kind) {
    if (!message) {
      banner.classList.add('hidden');
      banner.textContent = '';
      return;
    }
    banner.className = 'banner ' + (kind || 'warn');
    banner.textContent = message;
  }

  function loadHealth() {
    fetch('/api/health')
      .then((r) => r.json())
      .then((data) => {
        if (data.liveReady) {
          setBadge('live', 'live');
          showBanner('', null);
        } else {
          setBadge('demo', 'demo');
          showBanner('Running in demo mode. Set GEMINI_API_KEY and a reachable MongoDB to enable live queries.', 'warn');
        }
      })
      .catch(() => setBadge('muted', 'offline'));
  }

  function renderSchema() {
    const value = schemaViews[activeSchema];
    schemaEl.textContent = value ? JSON.stringify(value, null, 2) : 'No schema available.';
    schemaEl.classList.toggle('empty', !value);
  }

  function renderMessages(warnings, errors) {
    messagesEl.innerHTML = '';
    const list = [];
    (errors || []).forEach((e) => list.push({ text: e, kind: 'error' }));
    (warnings || []).forEach((w) => list.push({ text: w, kind: 'warn' }));
    if (!list.length) {
      const li = document.createElement('li');
      li.className = 'muted';
      li.textContent = 'No warnings or errors.';
      messagesEl.appendChild(li);
      return;
    }
    list.forEach((item) => {
      const li = document.createElement('li');
      li.className = item.kind;
      li.textContent = item.text;
      messagesEl.appendChild(li);
    });
  }

  function renderResults(rows) {
    resultsEl.innerHTML = '';
    if (!Array.isArray(rows) || rows.length === 0) {
      resultsEl.textContent = 'No results.';
      resultsEl.classList.add('empty');
      resultCount.textContent = '';
      return;
    }
    resultsEl.classList.remove('empty');

    const columns = [];
    rows.forEach((row) => {
      Object.keys(row).forEach((key) => {
        if (!columns.includes(key)) columns.push(key);
      });
    });

    const table = document.createElement('table');
    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    columns.forEach((col) => {
      const th = document.createElement('th');
      th.textContent = col;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    rows.forEach((row) => {
      const tr = document.createElement('tr');
      columns.forEach((col) => {
        const td = document.createElement('td');
        const value = row[col];
        td.textContent = value === undefined || value === null
          ? ''
          : typeof value === 'object'
            ? JSON.stringify(value)
            : String(value);
        tr.appendChild(td);
      });
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    resultsEl.appendChild(table);
    resultCount.textContent = '(' + rows.length + ')';
  }

  function renderResult(data) {
    const schema = data.schema || {};
    schemaViews = { compact: schema.compact || null, flattened: schema.flattened || null };
    activeSchema = 'compact';
    document.querySelectorAll('.tab').forEach((tab) => {
      tab.classList.toggle('active', tab.dataset.schema === 'compact');
    });
    renderSchema();

    pipelineEl.textContent = JSON.stringify(data.pipeline || [], null, 2);
    pipelineEl.classList.toggle('empty', !data.pipeline || data.pipeline.length === 0);

    renderMessages(data.warnings, data.errors);
    renderResults(data.results);

    if (data.mode === 'demo') {
      setBadge('demo', 'demo');
      showBanner(data.note || 'Showing demo data.', 'warn');
    } else {
      setBadge('live', 'live');
      if ((data.errors || []).length) {
        showBanner('Pipeline failed validation. See warnings and errors below.', 'error');
      } else {
        showBanner('', null);
      }
    }
  }

  function submit(event) {
    event.preventDefault();
    const question = document.getElementById('question').value.trim();
    const db = document.getElementById('db').value.trim() || 'shop';
    const collection = document.getElementById('collection').value.trim() || 'users';

    if (!question) {
      showBanner('Enter a question first.', 'error');
      return;
    }

    askButton.disabled = true;
    statusEl.textContent = 'Working...';
    showBanner('', null);

    fetch('/api/ask', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ db, collection, question }),
    })
      .then(async (r) => {
        const data = await r.json();
        if (!r.ok) throw new Error(data.error || 'Request failed');
        return data;
      })
      .then((data) => {
        statusEl.textContent = 'Done.';
        renderResult(data);
      })
      .catch((err) => {
        statusEl.textContent = '';
        showBanner(err.message, 'error');
      })
      .finally(() => {
        askButton.disabled = false;
      });
  }

  form.addEventListener('submit', submit);
  document.querySelectorAll('.tab').forEach((tab) => {
    tab.addEventListener('click', () => {
      activeSchema = tab.dataset.schema;
      document.querySelectorAll('.tab').forEach((t) => t.classList.toggle('active', t === tab));
      renderSchema();
    });
  });

  loadHealth();
})();
