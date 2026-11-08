/* ============================================================
   page.js - MdReader v1.0.0
   Two roles, chosen by context:
   - container page (host.html): one iframe per tab, routes the messages of the application;
   - document page (doc.html, in an iframe): rendering, source zone, links, outline, search, zoom, theme.
   Editing lives in editor.js. No network access: every resource comes from the virtual host.
   ============================================================ */
(function () {
    'use strict';

    // ---------------------------------------------------------------- container page
    if (window === window.top) {
        const frames = new Map();
        const applyVariables = (variables) => {
            if (!variables) { return; }
            for (const [name, value] of Object.entries(variables)) {
                document.documentElement.style.setProperty(name, value);
            }
        };

        window.mdrHost = {
            send(tab, message) {
                if (message.type === 'rendered') {
                    frames.get(tab)?.classList.add('rendered');
                }

                message.tab = tab;
                window.chrome.webview.postMessage(message);
            },
            sendFiles(tab, files) {
                window.chrome.webview.postMessageWithAdditionalObjects({ type: 'drop', tab: tab }, files);
            },
        };

        window.chrome.webview.addEventListener('message', (event) => {
            const message = event.data;
            switch (message.type) {
                case 'open': {
                    const frame = document.createElement('iframe');
                    frame.src = 'doc.html?tab=' + encodeURIComponent(message.tab);
                    frame.title = 'Document';
                    frames.set(message.tab, frame);
                    document.body.appendChild(frame);
                    break;
                }
                case 'close':
                    frames.get(message.tab)?.remove();
                    frames.delete(message.tab);
                    break;
                case 'activate':
                    for (const [tab, frame] of frames) {
                        frame.classList.toggle('active', tab === message.tab);
                    }

                    frames.get(message.tab)?.contentWindow?.focus();
                    break;
                default: {
                    applyVariables(message.variables);
                    const target = frames.get(message.tab)?.contentWindow;
                    target?.mdr?.receive(message);
                }
            }
        });
        window.chrome.webview.postMessage({ type: 'hostReady' });
        return;
    }

    // ---------------------------------------------------------------- document page
    const tab = new URLSearchParams(location.search).get('tab');
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const state = { mode: 'Reading', dark: false, hits: [], current: -1, heading: null, mermaidReady: null, seq: 0 };
    let doc = null;

    const send = (message) => window.parent.mdrHost.send(tab, message);

    /**
     * Source mode: a textarea (exact text, native undo) over a layer laid out identically that carries
     * the search highlights. Resolved on use: this script runs before the body exists.
     */
    const source = () => document.getElementById('source');
    const sourceHits = () => document.getElementById('source-hits');
    const sourceText = () => source().value;

    /** Grows the textarea to its content, so the page scrolls as in the other modes. */
    function fitSource() {
        const area = source();
        area.style.height = 'auto';
        area.style.height = area.scrollHeight + 'px';
    }

    /** Zone searched in the current mode: the highlight layer in source mode, the rendering otherwise. */
    const activeZone = () => (state.mode === 'Source' ? sourceHits() : doc);
    const behavior = () => (reducedMotion.matches ? 'auto' : 'smooth');

    /** Runs DOM changes that must not mark the document modified (highlighting, rendering). */
    const untracked = (change) => (window.mdrEditor ? window.mdrEditor.untracked(change) : change());

    const isInFrame = (node) => !!(node.parentElement || node).closest?.('.mermaid-frame');

    function applyTheme(dark, variables) {
        state.dark = !!dark;
        document.documentElement.classList.toggle('dark', state.dark);
        for (const [name, value] of Object.entries(variables || {})) {
            document.documentElement.style.setProperty(name, value);
        }
    }

    function applyZoom(zoom) {
        document.documentElement.style.setProperty('--zoom', String(zoom || 1));
        if (state.mode === 'Source') { fitSource(); }
    }

    // ------------------------------------------------------------ Mermaid (loaded on demand)
    function loadMermaid() {
        if (!state.mermaidReady) {
            state.mermaidReady = new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = 'vendor/mermaid.min.js';
                script.onload = () => resolve(window.mermaid);
                script.onerror = reject;
                document.head.appendChild(script);
            });
        }

        return state.mermaidReady;
    }

    function diagramType(source) {
        const first = source.trim().split(/\s+/)[0] || '';
        return first.replace(/[^A-Za-z0-9-]/g, '');
    }

    function buildFrame(source, editorOpen, title) {
        const frame = document.createElement('div');
        frame.className = 'mermaid-frame';
        frame.contentEditable = 'false';
        frame.setAttribute('data-source', source);
        frame.innerHTML =
            '<div class="mermaid-bar"><span class="mermaid-chip">Mermaid</span><span class="mermaid-type"></span>' +
            '<span class="spacer"></span><button type="button" class="mini" data-act="toggle">Modifier la source</button></div>' +
            '<div class="mermaid-view"></div>' +
            '<div class="mermaid-editor" hidden><textarea spellcheck="false" aria-label="Source Mermaid"></textarea>' +
            '<div class="mermaid-actions"><button type="button" class="mini primary" data-act="validate">Valider</button>' +
            '<button type="button" class="mini" data-act="cancel">Annuler</button></div></div>';
        frame.querySelector('.mermaid-type').textContent = title || diagramType(source);
        frame.querySelector('textarea').value = source;
        if (editorOpen) {
            frame.querySelector('.mermaid-editor').hidden = false;
            const pending = document.createElement('div');
            pending.className = 'mermaid-pending';
            pending.textContent = 'L’aperçu s’affiche après validation de la source.';
            frame.querySelector('.mermaid-view').appendChild(pending);
        }

        return frame;
    }

    async function renderFrame(frame) {
        const source = frame.getAttribute('data-source') || '';
        const view = frame.querySelector('.mermaid-view');
        try {
            const mermaid = await loadMermaid();
            mermaid.initialize({
                startOnLoad: false,
                securityLevel: 'strict',
                theme: state.dark ? 'dark' : 'default',
                fontFamily: '"Segoe UI Variable Text", "Segoe UI", sans-serif',
            });
            const { svg } = await mermaid.render('mdr-mermaid-' + (++state.seq), source);
            untracked(() => {
                view.innerHTML = svg;
                frame.querySelector('.mermaid-type').textContent = diagramType(source);
            });
        } catch (error) {
            untracked(() => {
                view.textContent = '';
                const message = document.createElement('div');
                message.className = 'mermaid-error';
                message.textContent = 'Diagramme invalide : ' + (error && error.message ? error.message : String(error));
                const code = document.createElement('pre');
                code.textContent = source;
                view.append(message, code);
            });
        }
    }

    function renderAllFrames() {
        return Promise.all([...doc.querySelectorAll('.mermaid-frame')]
            .filter((frame) => !frame.querySelector('.mermaid-pending'))
            .map(renderFrame));
    }

    // ------------------------------------------------------------ loading
    function prepare() {
        doc.querySelectorAll('pre > code').forEach((code) => {
            if (code.classList.contains('language-mermaid')) {
                code.parentElement.replaceWith(buildFrame(code.textContent.replace(/\n$/, ''), false));
            } else if (window.hljs && /language-\S+/.test(code.className)) {
                const language = code.className.match(/language-(\S+)/)[1];
                if (window.hljs.getLanguage(language)) {
                    window.hljs.highlightElement(code);
                }
            }
        });
        doc.querySelectorAll('input[type="checkbox"]').forEach((box) => box.removeAttribute('disabled'));
        doc.querySelectorAll('img').forEach(watchImage);
        ensureHeadingIds();
    }

    function watchImage(image) {
        image.addEventListener('error', () => untracked(() => {
            const alt = document.createElement('span');
            alt.className = 'img-missing';
            alt.textContent = image.getAttribute('alt') || image.getAttribute('data-md-src') || '';
            image.replaceWith(alt);
        }), { once: true });
    }

    function ensureHeadingIds() {
        let index = 0;
        doc.querySelectorAll('h1, h2, h3').forEach((heading) => {
            if (!heading.id) {
                heading.id = 'mdr-heading-' + (++index);
            }
        });
    }

    function load(message) {
        doc = document.getElementById('doc');
        applyTheme(message.dark, message.variables);
        applyZoom(message.zoom);
        untracked(() => {
            doc.innerHTML = message.html || '';
            prepare();
        });
        window.mdrEditor?.reset();
        setMode(message.mode);
        window.scrollTo(0, 0);
        renderAllFrames();
        send({ type: 'stats', outline: outline(), words: wordCount() });
        updateCurrentHeading();
        send({ type: 'rendered' });
    }

    function setMode(mode) {
        clearSearch();
        state.mode = mode || 'Reading';
        document.body.classList.toggle('source-mode', state.mode === 'Source');
        window.mdrEditor?.setEditing(state.mode === 'Editing');
    }

    function showSource(text) {
        const area = source();
        area.value = text || '';
        // A new value leaves the caret at the end, and focusing would scroll the page down to it.
        area.setSelectionRange(0, 0);
        setMode('Source');
        fitSource();
        window.scrollTo(0, 0);
    }

    document.addEventListener('input', (event) => {
        if (event.target.id !== 'source') { return; }
        fitSource();
        // Typing moves the text away from its highlights: they are removed, the counter is emptied.
        if (state.hits.length > 0) {
            clearSearch();
            send({ type: 'searchResult' });
        }
    });
    window.addEventListener('resize', () => { if (state.mode === 'Source') { fitSource(); } });

    // ------------------------------------------------------------ outline and statistics
    function outline() {
        untracked(ensureHeadingIds);
        return [...doc.querySelectorAll('h1, h2, h3')]
            .filter((heading) => !isInFrame(heading))
            .map((heading) => ({ level: Number(heading.tagName[1]), text: heading.textContent.trim(), id: heading.id }));
    }

    function wordCount() {
        const walker = document.createTreeWalker(doc, NodeFilter.SHOW_TEXT, {
            acceptNode: (node) => (isInFrame(node) || node.parentElement?.closest('.mdb-hidden') ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT),
        });
        let count = 0;
        while (walker.nextNode()) {
            count += (walker.currentNode.nodeValue.match(/\S+/g) || []).length;
        }

        return count;
    }

    function updateCurrentHeading() {
        if (!doc || state.mode === 'Source') { return; }
        let current = null;
        for (const heading of doc.querySelectorAll('h1, h2, h3')) {
            if (isInFrame(heading)) { continue; }
            if (heading.getBoundingClientRect().top <= 32) {
                current = heading.id;
            } else {
                break;
            }
        }

        if (!current) {
            current = doc.querySelector('h1, h2, h3')?.id || null;
        }

        if (current !== state.heading) {
            state.heading = current;
            send({ type: 'currentHeading', heading: current });
        }
    }

    let scrollPending = false;
    window.addEventListener('scroll', () => {
        if (!scrollPending) {
            scrollPending = true;
            requestAnimationFrame(() => { scrollPending = false; updateCurrentHeading(); });
        }
    }, { passive: true });

    function scrollToAnchor(anchor) {
        const target = document.getElementById(anchor);
        if (target) {
            window.scrollTo({ top: target.getBoundingClientRect().top + window.scrollY - 16, behavior: behavior() });
        }
    }

    // ------------------------------------------------------------ search
    function clearSearch() {
        if (!doc || state.hits.length === 0) { state.hits = []; state.current = -1; return; }
        untracked(() => {
            for (const mark of activeZone().querySelectorAll('mark.mdr-hit')) {
                const parent = mark.parentNode;
                mark.replaceWith(...mark.childNodes);
                parent.normalize();
            }
        });
        state.hits = [];
        state.current = -1;
    }

    function search(query) {
        clearSearch();
        if (state.mode === 'Source') {
            sourceHits().textContent = sourceText();
        }

        const needle = query.toLocaleLowerCase('fr');
        const nodes = [];
        const walker = document.createTreeWalker(activeZone(), NodeFilter.SHOW_TEXT, {
            acceptNode: (node) => (isInFrame(node) ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT),
        });
        while (walker.nextNode()) { nodes.push(walker.currentNode); }
        untracked(() => {
            for (const node of nodes) {
                const text = node.nodeValue;
                const lower = text.toLocaleLowerCase('fr');
                let from = 0;
                let index = lower.indexOf(needle, from);
                if (index < 0) { continue; }
                const fragment = document.createDocumentFragment();
                while (index >= 0) {
                    fragment.append(text.slice(from, index));
                    const mark = document.createElement('mark');
                    mark.className = 'mdr-hit';
                    mark.textContent = text.slice(index, index + needle.length);
                    fragment.append(mark);
                    state.hits.push(mark);
                    from = index + needle.length;
                    index = lower.indexOf(needle, from);
                }
                fragment.append(text.slice(from));
                node.replaceWith(fragment);
            }
        });
        state.current = state.hits.length > 0 ? 0 : -1;
        showCurrentHit();
    }

    function step(direction) {
        if (state.hits.length === 0) { return; }
        state.current = (state.current + direction + state.hits.length) % state.hits.length;
        showCurrentHit();
    }

    function showCurrentHit() {
        untracked(() => state.hits.forEach((hit, i) => hit.classList.toggle('mdr-cur', i === state.current)));
        const hit = state.hits[state.current];
        hit?.scrollIntoView({ block: 'center', behavior: behavior() });
        send({ type: 'searchResult', current: state.current + 1, total: state.hits.length });
    }

    // ------------------------------------------------------------ links, tasks, Mermaid buttons
    document.addEventListener('click', (event) => {
        const button = event.target.closest?.('.mermaid-frame button[data-act]');
        if (button) {
            event.preventDefault();
            window.mdrEditor?.frameAction(button.closest('.mermaid-frame'), button.getAttribute('data-act'));
            return;
        }

        const box = event.target.closest?.('input[type="checkbox"]');
        if (box) {
            if (box.checked) { box.setAttribute('checked', ''); } else { box.removeAttribute('checked'); }
            return;
        }

        const link = event.target.closest?.('a[href]');
        if (!link) { return; }
        event.preventDefault();
        if (state.mode === 'Editing') { return; }
        const href = link.getAttribute('href');
        if (href.startsWith('#')) {
            scrollToAnchor(decodeURIComponent(href.slice(1)));
        } else {
            send({ type: 'link', href: href });
        }
    }, true);

    document.addEventListener('wheel', (event) => { if (event.ctrlKey) { event.preventDefault(); } }, { passive: false });
    document.addEventListener('dragover', (event) => event.preventDefault());
    document.addEventListener('drop', (event) => {
        event.preventDefault();
        const files = [...(event.dataTransfer?.files || [])];
        if (files.length > 0) { window.parent.mdrHost.sendFiles(tab, files); }
    });

    // ------------------------------------------------------------ messages of the application
    window.mdr = {
        receive(message) {
            switch (message.type) {
                case 'load': load(message); break;
                case 'setMode': setMode(message.mode); break;
                case 'showSource': showSource(message.source); break;
                case 'setZoom': applyZoom(message.zoom); break;
                case 'setTheme': applyTheme(message.dark, message.variables); if (doc) { renderAllFrames(); } break;
                case 'format': window.mdrEditor?.format(message.action, message.argument, message.extra); break;
                case 'search': search(message.query || ''); break;
                case 'searchStep': step(message.direction || 1); break;
                case 'searchClear': clearSearch(); break;
                case 'scrollTo': scrollToAnchor(message.anchor); break;
                case 'serialize': send({ type: 'serialized', requestId: message.requestId, blocks: window.mdrEditor.serialize() }); break;
                case 'getSource': send({ type: 'serialized', requestId: message.requestId, source: sourceText() }); break;
                case 'focus': window.focus(); (state.mode === 'Source' ? source() : doc)?.focus(); break;
                default: break;
            }
        },
        send: send,
        sourceText: sourceText,
        buildFrame: buildFrame,
        renderFrame: renderFrame,
        stats: () => ({ outline: outline(), words: wordCount() }),
        watchImage: watchImage,
    };

    window.addEventListener('DOMContentLoaded', () => send({ type: 'ready' }));
})();
