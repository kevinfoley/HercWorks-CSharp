namespace HercWorks.Help.Html;

/// <summary>
/// The page's one script, fixed text that no help file contributes to. Everything it needs to know
/// about a particular manual it reads from attributes <see cref="HelpHtmlWriter"/> wrote: the topic
/// templates, <c>data-go</c> on links, and the window and pop-up containers. The page's
/// Content-Security-Policy admits this script by its hash and nothing else.
///
/// <para>A link's <c>data-go</c> is <c>t</c><i>topic</i>[<c>.</c><i>block</i>] for a jump, <c>k</c><i>n</i>
/// for a keyword's topic list, <c>index</c>, <c>p</c><i>topic</i> for a pop-up, <c>back</c>, <c>prev</c>,
/// <c>next</c>, <c>close</c> (the window it sits in) or <c>note</c> (show the link's <c>data-note</c> in
/// the pop-up). <c>data-w</c> sends a jump to a secondary window instead of the one the link is in.
/// Jumps in the main window go through the URL fragment, so the browser's Back works as WinHelp's
/// does.</para>
/// </summary>
internal static class ManualScript {
	public const string Text = """
'use strict';
(() => {
	const $ = id => document.getElementById(id);
	const main = $('main'), pop = $('popup'), body = document.body;
	const contents = 't' + body.dataset.contents;
	const pattern = /^(t\d+|k\d+|index)(?:\.(\d+))?$/;

	function show(box, id, block) {
		const t = $(id);
		if (!t || t.tagName !== 'TEMPLATE') return false;
		box.replaceChildren(t.content.cloneNode(true));
		box.dataset.shown = id;
		const target = block ? box.querySelector('[data-b="' + block + '"]') : null;
		if (target) target.scrollIntoView(); else box.scrollTop = 0;
		for (const w of (t.dataset.close || '').split(' ').filter(Boolean)) closeWindow(w);
		return true;
	}

	function route() {
		hidePopup();
		const m = pattern.exec(location.hash.slice(1));
		if (!m || !show(main, m[1], m[2])) show(main, contents);
		const t = $(main.dataset.shown);
		for (const dir of ['prev', 'next']) {
			const b = document.querySelector('#bar [data-go="' + dir + '"]');
			if (b) b.classList.toggle('off', !(t && t.dataset[dir]));
		}
	}

	function go(target) {
		if (location.hash === '#' + target) route(); else location.hash = target;
	}

	function openWindow(w, target) {
		const win = $('w' + w);
		if (!win) return;
		const m = pattern.exec(target);
		if (!m) return;
		win.hidden = false;
		show(win.querySelector('.wbody'), m[1], m[2]);
	}

	function closeWindow(w) {
		const win = $('w' + w);
		if (win) win.hidden = true;
	}

	function hidePopup() {
		pop.hidden = true;
		pop.replaceChildren();
	}

	function showPopup(x, y, fill) {
		pop.replaceChildren();
		fill(pop);
		pop.hidden = false;
		const r = pop.getBoundingClientRect();
		pop.style.left = Math.max(0, Math.min(x, innerWidth - r.width - 8)) + 'px';
		pop.style.top = Math.max(0, Math.min(y + 12, innerHeight - r.height - 8)) + 'px';
	}

	document.addEventListener('click', e => {
		const a = e.target.closest('[data-go]');
		if (!pop.hidden && !(a && pop.contains(a))) hidePopup();
		if (!a) return;
		e.preventDefault();
		const target = a.dataset.go;
		const win = a.closest('.win');
		if (target === 'back') { history.back(); return; }
		if (target === 'prev' || target === 'next') {
			const t = $(main.dataset.shown);
			if (t && t.dataset[target]) go('t' + t.dataset[target]);
			return;
		}
		if (target === 'close') { if (win) closeWindow(win.id.slice(1)); return; }
		if (target === 'note') {
			showPopup(e.clientX, e.clientY, p => { p.textContent = a.dataset.note; });
			return;
		}
		if (target[0] === 'p') {
			const t = $('t' + target.slice(1));
			if (t) showPopup(e.clientX, e.clientY, p => p.append(t.content.cloneNode(true)));
			return;
		}
		if (a.dataset.w !== undefined && a.dataset.w !== '0') { openWindow(a.dataset.w, target); return; }
		if (win && a.dataset.w === undefined) {
			const m = pattern.exec(target);
			if (m) show(win.querySelector('.wbody'), m[1], m[2]);
			return;
		}
		go(target);
	});

	document.addEventListener('keydown', e => { if (e.key === 'Escape') hidePopup(); });
	window.addEventListener('hashchange', route);
	route();
})();
""";
}
