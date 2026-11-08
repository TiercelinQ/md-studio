/* ============================================================
   editor.js - MdReader v1.0.0
   WYSIWYG editing of the document page (specification 4.9):
   - formatting commands through execCommand, so the browser keeps the undo history;
   - tracking of the modified top-level blocks (MutationObserver);
   - serialization block by block (specification T-04): an untouched block is sent as its
     source index, a modified or new block as Markdown (Turndown + GFM).
   ============================================================ */
(function () {
    'use strict';

    if (window === window.top) { return; }

    const DEBOUNCE_MS = 350;
    const TRACKED_ATTRIBUTES = ['checked', 'data-source', 'href', 'src', 'start'];
    let doc = null;
    let editing = false;
    let dirty = new WeakSet();
    let timer = 0;
    let savedRange = null;

    const escapeHtml = (text) => text.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // ------------------------------------------------------------ Markdown conversion
    const turndown = new window.TurndownService({
        headingStyle: 'atx',
        hr: '---',
        bulletListMarker: '-',
        codeBlockStyle: 'fenced',
        fence: '```',
        emDelimiter: '*',
        strongDelimiter: '**',
        linkStyle: 'inlined',
    });
    turndown.use(window.turndownPluginGfm.gfm);
    turndown.addRule('mermaid', {
        filter: (node) => node.nodeType === 1 && node.classList.contains('mermaid-frame'),
        replacement: (content, node) => '\n\n```mermaid\n' + (node.getAttribute('data-source') || '').replace(/\n$/, '') + '\n```\n\n',
    });
    turndown.addRule('localImage', {
        filter: (node) => node.nodeName === 'IMG' && node.hasAttribute('data-md-src'),
        replacement: (content, node) => '![' + (node.getAttribute('alt') || '') + '](' + node.getAttribute('data-md-src') + ')',
    });
    turndown.addRule('searchHit', {
        filter: (node) => node.nodeName === 'MARK' && node.classList.contains('mdr-hit'),
        replacement: (content) => content,
    });

    // ------------------------------------------------------------ tracking of modified blocks
    const topLevel = (node) => {
        let current = node;
        while (current && current.parentNode !== doc) { current = current.parentNode; }
        return current;
    };

    function record(mutations) {
        let changed = false;
        for (const mutation of mutations) {
            if (mutation.target === doc) {
                mutation.addedNodes.forEach((node) => { if (node.nodeType === 1) { dirty.add(node); } });
                changed = true;
                continue;
            }

            const block = topLevel(mutation.target);
            if (block) {
                dirty.add(block);
                changed = true;
            }
        }

        if (changed) { schedule(); }
    }

    const observer = new MutationObserver(record);

    function observe() {
        observer.observe(doc, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: TRACKED_ATTRIBUTES });
    }

    function untracked(change) {
        if (!doc) { return change(); }
        record(observer.takeRecords());
        observer.disconnect();
        try {
            return change();
        } finally {
            observe();
        }
    }

    function schedule() {
        clearTimeout(timer);
        timer = setTimeout(() => {
            const stats = window.mdr.stats();
            window.mdr.send({ type: 'changed', blocks: serialize(), outline: stats.outline, words: stats.words });
        }, DEBOUNCE_MS);
    }

    function markdownOf(node) {
        const clone = node.cloneNode(true);
        clone.querySelectorAll?.('mark.mdr-hit').forEach((mark) => mark.replaceWith(...mark.childNodes));
        const holder = document.createElement('div');
        holder.appendChild(clone);
        return turndown.turndown(holder.innerHTML).trim();
    }

    function serialize() {
        record(observer.takeRecords());
        const nodes = [...doc.childNodes];
        const counts = new Map();
        for (const node of nodes) {
            const src = node.nodeType === 1 ? node.getAttribute('data-src') : null;
            if (src !== null) { counts.set(src, (counts.get(src) || 0) + 1); }
        }

        const blocks = [];
        for (const node of nodes) {
            const src = node.nodeType === 1 ? node.getAttribute('data-src') : null;
            if (src !== null && !dirty.has(node) && counts.get(src) === 1) {
                blocks.push({ src: Number(src) });
                continue;
            }

            const markdown = node.nodeType === 3 ? node.nodeValue.trim() : node.nodeType === 1 ? markdownOf(node) : '';
            if (markdown.length > 0) { blocks.push({ md: markdown }); }
        }

        return blocks;
    }

    // ------------------------------------------------------------ mode, selection, state of the bar
    function setEditing(value) {
        editing = !!value;
        document.body.classList.toggle('editing', editing);
        untracked(() => {
            doc.contentEditable = editing ? 'true' : 'false';
            doc.querySelectorAll('.mermaid-frame').forEach((frame) => { frame.contentEditable = 'false'; });
            // An empty document (new file) gets a paragraph, so the first input is a normal block.
            if (editing && doc.childNodes.length === 0) { doc.innerHTML = '<p><br></p>'; }
        });
        if (editing) { doc.focus(); }
    }

    function blockStyle() {
        const selection = document.getSelection();
        const node = selection && selection.anchorNode;
        const element = node && (node.nodeType === 1 ? node : node.parentElement);
        const block = element && element.closest('h1, h2, h3, blockquote, p, li, pre, td, th');
        if (!block || !doc.contains(block)) { return 'p'; }
        const quote = block.closest('blockquote');
        if (quote) { return 'blockquote'; }
        return ['H1', 'H2', 'H3'].includes(block.tagName) ? block.tagName.toLowerCase() : 'p';
    }

    let statePending = false;
    document.addEventListener('selectionchange', () => {
        if (!editing || !doc) { return; }
        const selection = document.getSelection();
        if (selection.rangeCount > 0 && doc.contains(selection.anchorNode)) {
            savedRange = selection.getRangeAt(0).cloneRange();
        }

        if (statePending) { return; }
        statePending = true;
        requestAnimationFrame(() => {
            statePending = false;
            window.mdr.send({
                type: 'formatState',
                format: {
                    bold: document.queryCommandState('bold'),
                    italic: document.queryCommandState('italic'),
                    strike: document.queryCommandState('strikeThrough'),
                    bulletList: document.queryCommandState('insertUnorderedList'),
                    numberedList: document.queryCommandState('insertOrderedList'),
                    block: blockStyle(),
                },
            });
        });
    });

    function restoreSelection() {
        // The frame of a tab activated after another one does not own the focus yet.
        window.focus();
        doc.focus();
        if (savedRange) {
            const selection = document.getSelection();
            selection.removeAllRanges();
            selection.addRange(savedRange);
        }
    }

    const selectedText = () => document.getSelection()?.toString() || '';

    // ------------------------------------------------------------ formatting commands (4.9.1)
    function format(action, argument, extra) {
        if (!editing || !doc) { return; }
        restoreSelection();
        const run = (command, value) => document.execCommand(command, false, value);
        const insert = (html) => run('insertHTML', html);
        switch (action) {
            case 'Undo': run('undo'); break;
            case 'Redo': run('redo'); break;
            case 'Paragraph': run('formatBlock', 'p'); break;
            case 'Heading1': run('formatBlock', 'h1'); break;
            case 'Heading2': run('formatBlock', 'h2'); break;
            case 'Heading3': run('formatBlock', 'h3'); break;
            case 'Quote': run('formatBlock', blockStyle() === 'blockquote' ? 'p' : 'blockquote'); break;
            case 'Bold': run('bold'); break;
            case 'Italic': run('italic'); break;
            case 'Strikethrough': run('strikeThrough'); break;
            case 'InlineCode': {
                const text = selectedText();
                insert(text ? '<code>' + escapeHtml(text) + '</code>' : '<code>code</code>&nbsp;');
                break;
            }
            case 'BulletList': run('insertUnorderedList'); break;
            case 'NumberedList': run('insertOrderedList'); break;
            case 'TaskList':
                insert('<ul class="contains-task-list"><li class="task-list-item"><input type="checkbox" class="task-list-item-checkbox"> élément à faire</li></ul>');
                break;
            case 'Link':
                if (!argument) { break; }
                if (selectedText()) { run('createLink', argument); } else { insert('<a href="' + escapeHtml(argument) + '">texte du lien</a>&nbsp;'); }
                break;
            case 'Image':
                if (argument) { insert('<img src="' + escapeHtml(argument) + '" data-md-src="' + escapeHtml(extra || argument) + '" alt="image">'); }
                break;
            case 'Table':
                insert('<table><thead><tr><th>Colonne 1</th><th>Colonne 2</th><th>Colonne 3</th></tr></thead>' +
                    '<tbody><tr><td><br></td><td><br></td><td><br></td></tr><tr><td><br></td><td><br></td><td><br></td></tr></tbody></table><p><br></p>');
                break;
            case 'CodeBlock': insert('<pre><code>// code</code></pre><p><br></p>'); break;
            case 'Mermaid': {
                const holder = document.createElement('div');
                holder.appendChild(window.mdr.buildFrame('flowchart TD\n  A[Début] --> B[Fin]', true, 'nouveau diagramme'));
                insert(holder.innerHTML + '<p><br></p>');
                break;
            }
            case 'HorizontalRule': run('insertHorizontalRule'); break;
            default: break;
        }
    }

    // ------------------------------------------------------------ Mermaid frames (F-MER-06 to 10)
    function frameAction(frame, action) {
        const panel = frame.querySelector('.mermaid-editor');
        const textarea = frame.querySelector('textarea');
        switch (action) {
            case 'toggle':
                untracked(() => { panel.hidden = !panel.hidden; });
                if (!panel.hidden) { textarea.focus(); }
                break;
            case 'validate':
                frame.setAttribute('data-source', textarea.value);
                untracked(() => { panel.hidden = true; });
                window.mdr.renderFrame(frame);
                window.mdr.send({ type: 'notify', key: 'mermaidValidated' });
                break;
            case 'cancel':
                untracked(() => {
                    textarea.value = frame.getAttribute('data-source') || '';
                    panel.hidden = true;
                });
                break;
            default: break;
        }
    }

    // ------------------------------------------------------------ source mode (raw Markdown)
    let sourceTimer = 0;
    document.addEventListener('input', (event) => {
        if (event.target.id !== 'source') { return; }
        clearTimeout(sourceTimer);
        sourceTimer = setTimeout(() => window.mdr.send({ type: 'sourceChanged', source: window.mdr.sourceText() }), DEBOUNCE_MS);
    });

    // ------------------------------------------------------------ keyboard and paste
    document.addEventListener('keydown', (event) => {
        if (editing && event.ctrlKey && !event.shiftKey && event.key.toLowerCase() === 'k') {
            event.preventDefault();
            window.mdr.send({ type: 'requestLink' });
        }
    });

    document.addEventListener('paste', (event) => {
        if (!editing || event.target.closest?.('textarea')) { return; }
        const html = event.clipboardData?.getData('text/html');
        if (!html) { return; }
        event.preventDefault();
        const template = document.createElement('template');
        template.innerHTML = html;
        template.content.querySelectorAll('script, style, meta, link, iframe, object, embed').forEach((node) => node.remove());
        template.content.querySelectorAll('*').forEach((node) => {
            [...node.attributes].forEach((attribute) => {
                if (!['href', 'src', 'alt', 'colspan', 'rowspan'].includes(attribute.name)) { node.removeAttribute(attribute.name); }
            });
        });
        template.content.querySelectorAll('font, span').forEach((node) => node.replaceWith(...node.childNodes));
        document.execCommand('insertHTML', false, template.innerHTML);
    });

    // ------------------------------------------------------------ API used by page.js
    window.mdrEditor = {
        reset() {
            doc = document.getElementById('doc');
            dirty = new WeakSet();
            savedRange = null;
            clearTimeout(timer);
            observer.disconnect();
            observe();
        },
        untracked: untracked,
        setEditing: setEditing,
        format: format,
        frameAction: frameAction,
        serialize: serialize,
    };
})();
