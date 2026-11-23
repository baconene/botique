// Browser POS. Totals shown here are a preview; the server recalculates them (same rules as the
// Windows register) and the receipt prints the server's figures.
(function () {
    const root = document.getElementById('pos');
    if (!root) return;

    const $ = (id) => document.getElementById(id);
    const csrf = document.querySelector('meta[name="csrf-token"]').content;
    const cur = root.dataset.currency;
    const taxRate = parseFloat(root.dataset.tax) || 0;
    const canDiscount = root.dataset.discount === '1';

    let cart = [];           // {product_id, barcode, name, size, color, price (cents), quantity, stock}
    let method = 'Cash';
    let clientRef = newRef(); // one per sale: a retried request can never charge twice
    let busy = false;

    function newRef() {
        return (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : 'web-' + Date.now() + '-' + Math.random().toString(16).slice(2);
    }
    const cents = (v) => Math.round(parseFloat(String(v).replace(/[^0-9.\-]/g, '')) * 100) || 0;
    const money = (c) => cur + (c / 100).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const roundHalfAway = (x) => Math.sign(x) * Math.round(Math.abs(x));

    function say(text, kind) {
        const m = $('msg');
        m.textContent = text;
        m.className = 'pos-msg ' + (kind || '');
    }

    async function getJson(url) {
        const r = await fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' });
        if (r.status === 401 || r.status === 419) { location.href = '/login'; throw new Error('Signed out'); }
        return r.json();
    }

    // ---------------- search / scan ----------------
    let timer = null;
    async function search(q, addIfExact) {
        q = q.trim();
        if (!q) { hideResults(); return; }
        const res = await getJson(root.dataset.search + '?q=' + encodeURIComponent(q));
        const items = res.data || [];
        if (addIfExact && (res.exact || items.length === 1)) {
            add(items[0]);
            $('scan').value = '';
            hideResults();
            return;
        }
        if (addIfExact && items.length === 0) { say('No item matches "' + q + '".', 'bad'); hideResults(); return; }
        showResults(items);
    }

    function showResults(items) {
        const box = $('results');
        if (!items.length) { hideResults(); return; }
        box.innerHTML = items.map((it, i) =>
            '<button type="button" data-i="' + i + '"><span><strong>' + esc(it.name) + '</strong> ' +
            '<span class="muted">' + esc([it.size, it.color].filter(Boolean).join(' / ')) + ' · ' + esc(it.barcode) + '</span></span>' +
            '<span>' + money(cents(it.price)) + ' <span class="badge ' + (it.quantity > 0 ? 'ok' : 'bad') + '">' + it.quantity + '</span></span></button>'
        ).join('');
        box.classList.add('show');
        box.querySelectorAll('button').forEach((b) => b.addEventListener('click', () => {
            add(items[+b.dataset.i]);
            $('scan').value = '';
            hideResults();
            $('scan').focus();
        }));
    }
    function hideResults() { $('results').classList.remove('show'); $('results').innerHTML = ''; }

    $('scan').addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); clearTimeout(timer); search($('scan').value, true); }
        if (e.key === 'Escape') hideResults();
    });
    $('scan').addEventListener('input', () => {
        clearTimeout(timer);
        const q = $('scan').value;
        if (q.trim().length < 2) { hideResults(); return; }
        timer = setTimeout(() => search(q, false), 250);
    });
    $('scan-go').addEventListener('click', () => search($('scan').value, true));

    // ---------------- cart ----------------
    function add(it) {
        const price = cents(it.price);
        const line = cart.find((l) => l.product_id === it.id && l.price === price);
        if (line) line.quantity++;
        else cart.push({ product_id: it.id, barcode: it.barcode, name: it.name, size: it.size, color: it.color, price: price, list: price, quantity: 1, stock: it.quantity });
        const inCart = cart.filter((l) => l.product_id === it.id).reduce((n, l) => n + l.quantity, 0);
        if (inCart > it.quantity) say('Added ' + it.name + ', but only ' + it.quantity + ' in stock.', 'bad');
        else say('Added ' + it.name + ' – ' + money(price), 'ok');
        render();
    }

    function render() {
        const body = $('cart');
        if (!cart.length) {
            body.innerHTML = '<tr><td colspan="6" class="muted" style="text-align:center; padding: 28px">No items yet.</td></tr>';
        } else {
            body.innerHTML = cart.map((l, i) =>
                '<tr><td><strong>' + esc(l.name) + '</strong><br><span class="muted" style="font-size:.8rem">' + esc(l.barcode) + '</span></td>' +
                '<td>' + esc([l.size, l.color].filter(Boolean).join(' / ') || '—') + '</td>' +
                '<td class="num">' + (canDiscount
                    ? '<input data-price="' + i + '" value="' + (l.price / 100).toFixed(2) + '" inputmode="decimal">'
                    : money(l.price)) + '</td>' +
                '<td class="num"><input data-qty="' + i + '" type="number" min="1" max="999" value="' + l.quantity + '"></td>' +
                '<td class="num">' + money(l.price * l.quantity) + '</td>' +
                '<td><button class="btn small danger" type="button" data-rm="' + i + '">✕</button></td></tr>'
            ).join('');
            body.querySelectorAll('[data-qty]').forEach((el) => el.addEventListener('change', () => {
                const q = parseInt(el.value, 10);
                cart[+el.dataset.qty].quantity = q > 0 ? Math.min(q, 999) : 1;
                render();
            }));
            body.querySelectorAll('[data-price]').forEach((el) => el.addEventListener('change', () => {
                const c = cents(el.value);
                cart[+el.dataset.price].price = c >= 0 ? c : cart[+el.dataset.price].price;
                render();
            }));
            body.querySelectorAll('[data-rm]').forEach((el) => el.addEventListener('click', () => {
                cart.splice(+el.dataset.rm, 1);
                render();
            }));
        }
        totals();
    }

    function totals() {
        const sub = cart.reduce((s, l) => s + l.price * l.quantity, 0);
        const pct = canDiscount ? Math.min(Math.max(parseFloat($('disc').value) || 0, 0), 100) : 0;
        const disc = roundHalfAway(sub * pct / 100);
        const tax = roundHalfAway((sub - disc) * taxRate / 100);
        const total = sub - disc + tax;
        $('sub').textContent = money(sub);
        $('discAmt').textContent = disc ? '−' + money(disc) : money(0);
        $('tax').textContent = money(tax);
        $('total').textContent = money(total);
        const t = $('tendered').value.trim();
        if (method === 'Cash' && t) {
            const ch = cents(t) - total;
            $('change').textContent = ch >= 0 ? money(ch) : 'Short ' + money(-ch);
            $('change').style.color = ch >= 0 ? 'var(--ok)' : 'var(--bad)';
        } else { $('change').textContent = money(0); $('change').style.color = ''; }
        return total;
    }

    $('disc').addEventListener('input', totals);
    $('tendered').addEventListener('input', totals);
    document.querySelectorAll('.paymethods button').forEach((b) => b.addEventListener('click', () => {
        method = b.dataset.m;
        document.querySelectorAll('.paymethods button').forEach((x) => x.classList.toggle('on', x === b));
        $('cash-box').style.display = method === 'Cash' ? '' : 'none';
        totals();
    }));
    document.querySelectorAll('[data-q]').forEach((b) => b.addEventListener('click', () => {
        const q = parseInt(b.dataset.q, 10);
        $('tendered').value = q === 0 ? (totals() / 100).toFixed(2) : q.toFixed(2);
        totals();
    }));

    // ---------------- complete ----------------
    async function complete() {
        if (busy) return;
        if (!cart.length) { say('Scan at least one item first.', 'bad'); return; }
        const payload = {
            client_ref: clientRef,
            payment_method: method,
            discount_percent: canDiscount ? (parseFloat($('disc').value) || 0) : 0,
            lines: cart.map((l) => ({ product_id: l.product_id, quantity: l.quantity, unit_price: (l.price / 100).toFixed(2) })),
        };
        const t = $('tendered').value.trim();
        if (method === 'Cash' && t) payload.amount_tendered = (cents(t) / 100).toFixed(2);

        busy = true;
        $('complete').disabled = true;
        say('Saving sale…');
        try {
            const r = await fetch(root.dataset.sale, {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'X-CSRF-TOKEN': csrf },
                body: JSON.stringify(payload),
            });
            const body = await r.json().catch(() => ({}));
            if (r.status === 401 || r.status === 419) { say('Your session ended. Sign in again.', 'bad'); return; }
            if (!r.ok) {
                const first = body.errors ? Object.values(body.errors)[0][0] : null;
                say(first || body.message || ('Sale not saved (HTTP ' + r.status + ').'), 'bad');
                return;
            }
            const s = body.data;
            $('done-total').textContent = money(cents(s.total));
            $('done-detail').textContent = 'Receipt ' + s.code + ' · ' + s.payment_method +
                (s.payment_method === 'Cash' && cents(s.change_due) > 0 ? ' · Change due ' + money(cents(s.change_due)) : '');
            $('done-receipt').href = body.receipt_url;
            $('done').classList.add('show');
            say('Sale ' + s.code + ' saved.', 'ok');
        } catch (e) {
            say('Could not reach the server. Your cart is kept; press Complete sale again (it will not be charged twice).', 'bad');
        } finally {
            busy = false;
            $('complete').disabled = false;
        }
    }
    $('complete').addEventListener('click', complete);

    function reset() {
        cart = [];
        clientRef = newRef();
        $('disc').value = 0;
        $('tendered').value = '';
        $('done').classList.remove('show');
        render();
        say('Ready for the next customer.');
        $('scan').focus();
    }
    $('done-new').addEventListener('click', reset);
    $('new-sale').addEventListener('click', () => { if (!cart.length || confirm('Clear this sale?')) reset(); });
    document.addEventListener('keydown', (e) => { if (e.key === 'F12') { e.preventDefault(); complete(); } });

    render();
})();
