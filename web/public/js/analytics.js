// Analytics dashboard (React 18 + Recharts + Lucide, loaded from a CDN; markup via htm, no build step).
// Data comes from /admin/analytics/data. Blocks marked demo are sample data for metrics the POS doesn't record.
(() => {
    'use strict';

    const root = document.getElementById('analytics-root');
    if (!root) return;
    const missing = ['React', 'ReactDOM', 'Recharts', 'htm', 'lucide'].filter((g) => !window[g]);
    if (missing.length) {
        root.innerHTML = '<div class="panel an-error">The dashboard libraries could not be loaded (' + missing.join(', ') +
            '). Check the internet connection and reload. Sales and inventory reports are available under Reports.</div>';
        return;
    }

    const { useState, useEffect, useMemo, useCallback, Fragment } = React;
    const html = htm.bind(React.createElement);
    const R = Recharts;
    const CFG = {
        endpoint: root.dataset.endpoint,
        usdRate: parseFloat(root.dataset.usdRate) || 58,
        channels: JSON.parse(root.dataset.channels || '{}'),
        today: root.dataset.today,
    };
    const PLUM = '#69406e', PLUM_SOFT = '#cdb9d3', MINT = '#58aeb2', AMBER = '#d99a2b', RED = '#c2453d', INK = '#272339', MUTED = '#777285';
    const SERIES = ['#69406e', '#58aeb2', '#d99a2b', '#8a7bd0', '#c2453d', '#3f8f5f', '#b07aa1', '#5b6bbf'];

    // ---------------- helpers ----------------

    const iso = (d) => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    const parseDay = (s) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
    const addDays = (d, n) => { const x = new Date(d); x.setDate(x.getDate() + n); return x; };

    function presetRange(key) {
        const t = parseDay(CFG.today);
        switch (key) {
            case 'today': return [t, t];
            case 'yesterday': return [addDays(t, -1), addDays(t, -1)];
            case '7d': return [addDays(t, -6), t];
            case '30d': return [addDays(t, -29), t];
            case '90d': return [addDays(t, -89), t];
            case 'month': return [new Date(t.getFullYear(), t.getMonth(), 1), t];
            case 'lastMonth': return [new Date(t.getFullYear(), t.getMonth() - 1, 1), new Date(t.getFullYear(), t.getMonth(), 0)];
            case 'year': return [new Date(t.getFullYear(), 0, 1), t];
            case '12m': return [addDays(new Date(t.getFullYear() - 1, t.getMonth(), t.getDate()), 1), t];
            default: return [t, t];
        }
    }
    const PRESETS = [['today', 'Today'], ['yesterday', 'Yesterday'], ['7d', 'Last 7 days'], ['30d', 'Last 30 days'], ['90d', 'Last 90 days'],
        ['month', 'This month'], ['lastMonth', 'Last month'], ['year', 'This year'], ['12m', 'Last 12 months'], ['custom', 'Custom']];
    const VIEWS = [
        { key: 'daily', label: 'Daily operations', icon: 'Activity', preset: 'today' },
        { key: 'monthly', label: 'Monthly profitability', icon: 'PiggyBank', preset: 'month' },
        { key: 'annual', label: 'Annual strategy', icon: 'TrendingUp', preset: 'year' },
        { key: 'timing', label: 'Product & sales timing', icon: 'Clock', preset: '90d' },
    ];

    function makeMoney(currency) {
        const usd = currency === 'USD';
        const full = new Intl.NumberFormat('en-PH', { style: 'currency', currency: usd ? 'USD' : 'PHP', maximumFractionDigits: 2, minimumFractionDigits: 2 });
        const compact = new Intl.NumberFormat('en-PH', { style: 'currency', currency: usd ? 'USD' : 'PHP', notation: 'compact', maximumFractionDigits: 1 });
        const conv = (v) => (usd ? (Number(v) || 0) / CFG.usdRate : Number(v) || 0);
        return { fmt: (v) => full.format(conv(v)), short: (v) => compact.format(conv(v)), conv, code: usd ? 'USD' : 'PHP' };
    }
    const num = (v) => new Intl.NumberFormat('en-PH').format(Number(v) || 0);
    const pct = (v, digits = 1) => (Number(v) || 0).toFixed(digits) + '%';
    const delta = (cur, prev) => (prev ? ((cur - prev) / Math.abs(prev)) * 100 : cur ? null : 0);

    function downloadCsv(name, columns, rows) {
        const esc = (v) => { const s = v == null ? '' : String(v); return /[",\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
        const lines = [columns.map((c) => esc(c.label)).join(',')]
            .concat(rows.map((r) => columns.map((c) => esc(c.csv ? c.csv(r) : r[c.key])).join(',')));
        const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob); a.download = name + '.csv';
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    }

    // ---------------- small components ----------------

    function Icon({ name, size = 16, className = '' }) {
        const node = lucide.icons && lucide.icons[name];
        if (!node) return null;
        const children = node[0] === 'svg' ? node[2] : node;
        return html`<svg className=${'an-icon ' + className} width=${size} height=${size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
            strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            ${children.map(([tag, attrs], i) => React.createElement(tag, { key: i, ...attrs }))}</svg>`;
    }

    const Badge = ({ kind, children }) => html`<span className=${'an-badge ' + kind}>${children}</span>`;
    const DemoTag = ({ text }) => html`<span className="an-demo" title="The POS doesn't record this yet, so this block shows sample data."><${Icon} name="FlaskConical" size=${12} />${text || 'Sample data'}</span>`;
    const EstTag = ({ text }) => html`<span className="an-est" title="Estimated with the rates in config/pos.php"><${Icon} name="Calculator" size=${12} />${text || 'Estimate'}</span>`;

    function Panel({ title, sub, tag, actions, children, className = '' }) {
        return html`<section className=${'panel an-panel ' + className}>
            <div className="an-panel-head">
                <div><h2>${title}</h2>${sub && html`<p className="muted">${sub}</p>`}</div>
                <div className="an-panel-tools">${tag}${actions}</div>
            </div>
            ${children}
        </section>`;
    }

    function Kpi({ label, value, icon, change, sub, featured, demo, invert }) {
        const c = change == null ? null : Math.round(change * 10) / 10;
        const good = c == null ? null : invert ? c <= 0 : c >= 0;
        return html`<div className=${'stat an-kpi' + (featured ? ' featured' : '')}>
            <div className="an-kpi-top"><span>${label}</span><span className="an-kpi-icons">${demo && html`<${DemoTag} text="Sample" />`}<${Icon} name=${icon} size=${18} /></span></div>
            <strong>${value}</strong>
            <div className="an-kpi-foot">
                ${c != null && html`<em className=${good ? 'up' : 'down'}><${Icon} name=${c >= 0 ? 'ArrowUpRight' : 'ArrowDownRight'} size=${13} />${Math.abs(c)}%</em>`}
                ${c == null && change !== undefined && html`<em className="flat">new</em>`}
                <small>${sub}</small>
            </div>
        </div>`;
    }

    const ChartTip = ({ active, payload, label, money, unit }) => {
        if (!active || !payload || !payload.length) return null;
        return html`<div className="an-tip"><strong>${label}</strong>
            ${payload.map((p, i) => html`<div key=${i}><i style=${{ background: p.color || p.fill }}></i>${p.name}: <b>${
                p.dataKey && /cvr|rate|margin|roas|share|sellThrough/i.test(p.dataKey) ? (/roas/i.test(p.dataKey) ? p.value + '×' : pct(p.value))
                    : /revenue|spend|amount|ltv|net|value|gross/i.test(p.dataKey) ? money.fmt(p.value) : num(p.value) + (unit || '')}</b></div>`)}
        </div>`;
    };

    function SortTable({ columns, rows, csvName, initialSort, searchable, expand, empty, pageSize = 0 }) {
        const [sort, setSort] = useState(initialSort || { key: columns[0].key, dir: 'desc' });
        const [q, setQ] = useState('');
        const [open, setOpen] = useState(null);
        const [limit, setLimit] = useState(pageSize || 1e9);
        const shown = useMemo(() => {
            let list = rows;
            if (q.trim()) {
                const t = q.trim().toLowerCase();
                list = list.filter((r) => columns.some((c) => String(c.csv ? c.csv(r) : r[c.key] ?? '').toLowerCase().includes(t)));
            }
            const col = columns.find((c) => c.key === sort.key);
            const val = (r) => (col && col.sortValue ? col.sortValue(r) : r[sort.key]);
            return [...list].sort((a, b) => {
                const x = val(a), y = val(b);
                const cmp = typeof x === 'number' && typeof y === 'number' ? x - y : String(x ?? '').localeCompare(String(y ?? ''));
                return sort.dir === 'asc' ? cmp : -cmp;
            });
        }, [rows, q, sort, columns]);
        const toggle = (key) => setSort((s) => ({ key, dir: s.key === key && s.dir === 'desc' ? 'asc' : 'desc' }));
        return html`<div className="an-table">
            <div className="an-table-tools">
                ${searchable && html`<label className="an-search"><${Icon} name="Search" size=${14} /><input value=${q} onInput=${(e) => setQ(e.target.value)} placeholder="Search…" aria-label="Search table" /></label>`}
                <span className="muted">${num(shown.length)} ${shown.length === 1 ? 'row' : 'rows'}</span>
                ${csvName && html`<button type="button" className="btn small" onClick=${() => downloadCsv(csvName, columns, shown)}><${Icon} name="Download" size=${14} /> Export CSV</button>`}
            </div>
            <div className="table-scroll" tabIndex="0" role="region" aria-label=${csvName || 'Table'}>
                <table className="data">
                    <thead><tr>
                        ${expand && html`<th aria-label="Details"></th>`}
                        ${columns.map((c) => html`<th key=${c.key} className=${c.num ? 'num' : ''} aria-sort=${sort.key === c.key ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
                            <button type="button" className="an-sort" onClick=${() => toggle(c.key)}>${c.label}<${Icon} name=${sort.key === c.key ? (sort.dir === 'asc' ? 'ChevronUp' : 'ChevronDown') : 'ChevronsUpDown'} size=${12} /></button></th>`)}
                    </tr></thead>
                    <tbody>
                        ${shown.length === 0 && html`<tr><td colSpan=${columns.length + (expand ? 1 : 0)} className="empty-note">${empty || 'Nothing to show for this period.'}</td></tr>`}
                        ${shown.slice(0, limit).map((r, i) => html`<${Fragment} key=${i}>
                            <tr className=${open === i ? 'open' : ''}>
                                ${expand && html`<td><button type="button" className="an-expand" aria-expanded=${open === i} aria-label="Show size breakdown" onClick=${() => setOpen(open === i ? null : i)}><${Icon} name=${open === i ? 'ChevronDown' : 'ChevronRight'} size=${14} /></button></td>`}
                                ${columns.map((c) => html`<td key=${c.key} className=${c.num ? 'num' : ''}>${c.render ? c.render(r) : r[c.key]}</td>`)}
                            </tr>
                            ${expand && open === i && html`<tr className="an-expanded"><td colSpan=${columns.length + 1}>${expand(r)}</td></tr>`}
                        <//>`)}
                    </tbody>
                </table>
            </div>
            ${shown.length > limit && html`<button type="button" className="btn small an-more" onClick=${() => setLimit(limit + pageSize)}>Show more (${num(shown.length - limit)} left)</button>`}
        </div>`;
    }

    function Heatmap({ heatmap, money }) {
        const [metric, setMetric] = useState('counts');
        const [hover, setHover] = useState(null);
        const grid = heatmap[metric];
        const max = Math.max(1, ...grid.flat());
        const total = grid.flat().reduce((a, b) => a + b, 0);
        let peak = null;
        grid.forEach((row, d) => row.forEach((v, h) => { if (v > 0 && (!peak || v > peak.v)) peak = { d, h, v }; }));
        const hourLabel = (h) => (h % 12 === 0 ? 12 : h % 12) + (h < 12 ? 'a' : 'p');
        const fmt = (v) => (metric === 'counts' ? num(v) + (v === 1 ? ' order' : ' orders') : money.fmt(v));
        return html`<${Panel} title="Sales heatmap" sub="Day of week × hour of day. Use the peaks to time ads and flash drops."
            actions=${html`<div className="an-seg" role="group" aria-label="Heatmap metric">
                <button type="button" aria-pressed=${metric === 'counts'} onClick=${() => setMetric('counts')}>Orders</button>
                <button type="button" aria-pressed=${metric === 'revenue'} onClick=${() => setMetric('revenue')}>Net sales</button></div>`}>
            <div className="an-heat-wrap">
                <div className="an-heat" role="grid" aria-label="Sales heatmap">
                    <div className="an-heat-row an-heat-hours" role="row"><span></span>${Array.from({ length: 24 }, (_, h) => html`<span key=${h} role="columnheader">${h % 3 === 0 ? hourLabel(h) : ''}</span>`)}</div>
                    ${grid.map((row, d) => html`<div key=${d} className="an-heat-row" role="row"><span role="rowheader">${heatmap.days[d]}</span>
                        ${row.map((v, h) => html`<button type="button" key=${h} role="gridcell" className="an-heat-cell"
                            style=${{ background: v ? `rgba(105,64,110,${0.12 + 0.88 * (v / max)})` : '#f3f0f7' }}
                            aria-label=${heatmap.days[d] + ' ' + hourLabel(h) + ': ' + fmt(v)}
                            onMouseEnter=${() => setHover({ d, h, v })} onFocus=${() => setHover({ d, h, v })} onMouseLeave=${() => setHover(null)}></button>`)}
                    </div>`)}
                </div>
                <div className="an-heat-side">
                    <div className="an-heat-readout">${hover ? html`<strong>${heatmap.days[hover.d]} · ${hourLabel(hover.h)}–${hourLabel((hover.h + 1) % 24)}</strong><span>${fmt(hover.v)}</span>`
                        : peak ? html`<strong>Peak: ${heatmap.days[peak.d]} ${hourLabel(peak.h)}</strong><span>${fmt(peak.v)}</span>` : html`<span className="muted">No completed sales in this period.</span>`}</div>
                    <div className="an-heat-scale"><span>Less</span><i></i><span>More</span></div>
                    <p className="muted">${metric === 'counts' ? num(total) + ' completed orders' : money.fmt(total) + ' net sales'} in the period.</p>
                </div>
            </div>
        <//>`;
    }

    function Gauge({ value, max, label, sub }) {
        const ratio = Math.max(0, Math.min(1, value / max));
        const angle = Math.PI * (1 - ratio);
        const x = 100 + 80 * Math.cos(angle), y = 100 - 80 * Math.sin(angle);
        const color = value >= 3 ? '#3f8f5f' : value >= 1.5 ? AMBER : RED;
        return html`<div className="an-gauge"><svg viewBox="0 0 200 116" role="img" aria-label=${label + ' ' + value}>
            <path d="M20 100 A80 80 0 0 1 180 100" fill="none" stroke="#efeaf3" strokeWidth="16" strokeLinecap="round" />
            <path d=${`M20 100 A80 80 0 0 1 ${x.toFixed(1)} ${y.toFixed(1)}`} fill="none" stroke=${color} strokeWidth="16" strokeLinecap="round" />
            <text x="100" y="90" textAnchor="middle" className="an-gauge-value">${value.toFixed(2)}×</text>
            <text x="100" y="110" textAnchor="middle" className="an-gauge-label">${label}</text></svg>
            <p className="muted">${sub}</p></div>`;
    }

    // ---------------- views ----------------

    function DailyView({ d, money }) {
        const k = d.kpis, p = k.prev, ads = d.ads;
        const lowCols = [
            { key: 'name', label: 'Item', render: (r) => html`<strong>${r.name}</strong><small className="cell-sub">${r.barcode}</small>` },
            { key: 'size', label: 'Size', render: (r) => html`<span className="an-size">${r.size || '—'}</span> ${r.color}` },
            { key: 'onHand', label: 'On hand', num: true },
            { key: 'sold30', label: 'Sold · 30d', num: true },
            { key: 'cover', label: 'Days left', num: true, sortValue: (r) => (r.cover == null ? 9999 : r.cover), render: (r) => (r.cover == null ? '—' : r.cover) },
            { key: 'status', label: 'Status', render: (r) => html`<${Badge} kind=${r.status === 'Out of stock' ? 'bad' : r.status === 'Restock now' ? 'warn' : 'soft'}>${r.status === 'Low' ? 'Low' : 'Restock'}${r.status === 'Out of stock' ? ' · out' : ''}<//>` },
        ];
        const ageCols = [
            { key: 'order', label: 'Order' }, { key: 'customer', label: 'Customer' },
            { key: 'ageHours', label: 'Waiting', num: true, render: (r) => r.ageHours + 'h' },
            { key: 'bucket', label: 'Age', sortValue: (r) => r.ageHours, render: (r) => html`<${Badge} kind=${r.ageHours >= 48 ? 'bad' : r.ageHours >= 24 ? 'warn' : 'ok'}>${r.bucket}<//>` },
            { key: 'courier', label: 'Courier' }, { key: 'region', label: 'Region' }, { key: 'payment', label: 'Payment' },
            { key: 'amount', label: 'Amount', num: true, render: (r) => money.fmt(r.amount), csv: (r) => money.conv(r.amount).toFixed(2) },
        ];
        const over24 = d.unfulfilled.rows.filter((r) => r.ageHours >= 24).length, over48 = d.unfulfilled.rows.filter((r) => r.ageHours >= 48).length;
        return html`<${Fragment}>
            <div className="metric-grid">
                <${Kpi} featured label="Gross sales" icon="Receipt" value=${money.fmt(k.gross)} change=${delta(k.gross, p.gross)}
                    sub=${'Net ' + money.fmt(k.net) + ' after ' + money.fmt(k.cancellations) + ' refunds & ' + money.fmt(k.discounts) + ' discounts'} />
                <${Kpi} label="Net sales" icon="Wallet" value=${money.fmt(k.net)} change=${delta(k.net, p.net)} sub=${'vs ' + money.fmt(p.net) + ' previous period'} />
                <${Kpi} label="Ad spend" icon="Megaphone" demo value=${money.fmt(ads.spend)} change=${delta(ads.spend, ads.prevSpend)} invert
                    sub=${ads.blendedRoas + '× ROAS (' + (ads.prevRoas || 0) + '× before)'} />
                <${Kpi} label="Total orders" icon="ShoppingBag" value=${num(k.orders)} change=${delta(k.orders, p.orders)} sub=${'AOV ' + money.fmt(k.aov) + ' · ' + num(k.units) + ' pieces'} />
            </div>
            <div className="an-grid two">
                <${Panel} title="Ad spend & ROAS by platform" sub="Spend vs attributed revenue; blended ROAS uses your real net sales." tag=${html`<${DemoTag} />`}>
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${250}>
                        <${R.ComposedChart} data=${ads.platforms} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="name" tickLine=${false} axisLine=${false} />
                            <${R.YAxis} yAxisId="m" tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                            <${R.YAxis} yAxisId="r" orientation="right" tickFormatter=${(v) => v + '×'} tickLine=${false} axisLine=${false} width=${40} />
                            <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                            <${R.Legend} />
                            <${R.Bar} yAxisId="m" dataKey="spend" name="Spend" fill=${PLUM_SOFT} radius=${[6, 6, 0, 0]} />
                            <${R.Bar} yAxisId="m" dataKey="revenue" name="Attributed revenue" fill=${PLUM} radius=${[6, 6, 0, 0]} />
                            <${R.Line} yAxisId="r" dataKey="roas" name="ROAS" stroke=${MINT} strokeWidth=${3} dot=${{ r: 5 }} />
                        <//>
                    <//></div>
                <//>
                <${Panel} title="Traffic & conversion by device" sub="Sessions (bars) and conversion rate (line). Drops of 20%+ vs the previous period are flagged." tag=${html`<${DemoTag} />`}>
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${210}>
                        <${R.ComposedChart} data=${d.devices.rows} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="device" tickLine=${false} axisLine=${false} />
                            <${R.YAxis} yAxisId="s" tickFormatter=${num} tickLine=${false} axisLine=${false} width=${55} />
                            <${R.YAxis} yAxisId="c" orientation="right" tickFormatter=${(v) => v + '%'} tickLine=${false} axisLine=${false} width=${40} />
                            <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                            <${R.Bar} yAxisId="s" dataKey="sessions" name="Sessions" radius=${[6, 6, 0, 0]}>
                                ${d.devices.rows.map((r, i) => html`<${R.Cell} key=${i} fill=${r.drop ? RED : PLUM_SOFT} />`)}
                            <//>
                            <${R.Line} yAxisId="c" dataKey="cvr" name="Conversion rate" stroke=${PLUM} strokeWidth=${3} dot=${{ r: 5 }} />
                            <${R.Line} yAxisId="c" dataKey="prevCvr" name="Previous CVR" stroke=${MUTED} strokeDasharray="4 4" dot=${false} />
                        <//>
                    <//></div>
                    <div className="an-chips">${d.devices.rows.map((r) => html`<span key=${r.device} className=${'an-chip' + (r.drop ? ' bad' : '')}>
                        <${Icon} name=${r.device === 'Mobile' ? 'Smartphone' : r.device === 'Tablet' ? 'Tablet' : 'Monitor'} size=${14} />
                        ${r.device} ${pct(r.share, 0)} of traffic · CVR ${pct(r.cvr, 2)}${r.drop ? ' · sudden drop' : ''}</span>`)}</div>
                <//>
            </div>
            <${Heatmap} heatmap=${d.heatmap} money=${money} />
            <div className="an-grid two-xl">
                <${Panel} title="Low-stock & out-of-stock alerts" sub="At or below the reorder level, fastest movers first. Days left uses the last 30 days of sales.">
                    <${SortTable} columns=${lowCols} rows=${d.lowStock} csvName="low-stock" initialSort=${{ key: 'onHand', dir: 'asc' }} empty="Every item is above its reorder level." pageSize=${8} />
                <//>
                <${Panel} title="Unfulfilled orders aging" sub=${over24 + ' orders past 24h, ' + over48 + ' past 48h. Ship these first to prevent cancellations.'} tag=${html`<${DemoTag} />`}>
                    <${SortTable} columns=${ageCols} rows=${d.unfulfilled.rows} csvName="unfulfilled-orders" initialSort=${{ key: 'ageHours', dir: 'desc' }} />
                <//>
            </div>
        <//>`;
    }

    function MonthlyView({ d, money }) {
        const w = d.waterfall;
        let running = 0;
        const steps = w.steps.map((s) => {
            if (s.type === 'total') { running = s.amount; return { ...s, base: Math.min(0, s.amount), value: Math.abs(s.amount) }; }
            const top = running; running += s.amount;
            return { ...s, base: Math.min(top, running), value: Math.abs(s.amount) };
        });
        const kindColor = { real: PLUM, estimate: AMBER, demo: '#b5a6c4', derived: w.netProfit >= 0 ? '#3f8f5f' : RED };
        const sizeOrder = (sizes) => Object.entries(sizes || {});
        const prodCols = [
            { key: 'name', label: 'Product', render: (r) => html`<strong>${r.name}</strong><small className="cell-sub">${r.brand}</small>` },
            { key: 'category', label: 'Category' },
            { key: 'sizes', label: 'Sizes', sortValue: (r) => Object.keys(r.sizes).length, csv: (r) => sizeOrder(r.sizes).map(([s, n]) => s + ':' + n).join(' '),
                render: (r) => html`<span className="an-size-strip">${sizeOrder(r.sizes).map(([s, n]) => html`<span key=${s} className="an-size">${s}<b>${n}</b></span>`)}</span>` },
            { key: 'units', label: 'Units', num: true },
            { key: 'gross', label: 'Gross revenue', num: true, render: (r) => money.fmt(r.gross), csv: (r) => money.conv(r.gross).toFixed(2) },
            { key: 'net', label: 'Net revenue', num: true, render: (r) => money.fmt(r.net), csv: (r) => money.conv(r.net).toFixed(2) },
            { key: 'returnRate', label: 'Return rate', num: true, render: (r) => html`<span className=${r.returnRate >= 15 ? 'an-neg' : ''}>${pct(r.returnRate)}</span>` },
            { key: 'margin', label: 'Profit margin', num: true, render: (r) => html`<span className=${r.margin < 30 ? 'an-neg' : 'an-pos'}>${pct(r.margin)}</span>` },
        ];
        const sizeDetail = (r) => {
            const rows = sizeOrder(r.sizes).map(([size, units]) => ({ size, units, share: r.units ? (units / r.units) * 100 : 0 }));
            const max = Math.max(1, ...rows.map((x) => x.units));
            return html`<div className="an-velocity"><strong>Size velocity · ${r.name}</strong>
                ${rows.map((x) => html`<div key=${x.size} className="an-vrow"><span className="an-size">${x.size}</span>
                    <div className="an-bar"><i style=${{ width: (x.units / max) * 100 + '%' }}></i></div><b>${x.units} units</b><span className="muted">${pct(x.share, 0)}</span></div>`)}
                <p className="muted">${r.returned} returned · ${money.fmt(r.cost)} cost of goods · ${money.fmt(r.net - r.cost)} gross profit</p></div>`;
        };
        const reasons = Object.keys(d.returns.reasonTotals);
        const returnRows = d.returns.rows.map((r) => ({ label: r.name + ' ' + (r.size || ''), ...r.reasons, rate: r.rate }));
        const reasonPie = reasons.map((k) => ({ name: k, value: d.returns.reasonTotals[k] }));
        const rtoCols = (title) => [
            { key: 'name', label: title }, { key: 'orders', label: 'COD orders', num: true }, { key: 'rto', label: 'Returned', num: true },
            { key: 'rate', label: 'RTO rate', num: true, render: (r) => html`<${Badge} kind=${r.high ? 'bad' : r.rate >= 10 ? 'warn' : 'ok'}>${pct(r.rate)}${r.high ? ' · high' : ''}<//>` },
        ];
        return html`<${Fragment}>
            <div className="metric-grid">
                <${Kpi} featured label="Net sales" icon="Wallet" value=${money.fmt(d.kpis.net)} change=${delta(d.kpis.net, d.kpis.prev.net)} sub=${num(d.kpis.orders) + ' orders'} />
                <${Kpi} label="Gross margin" icon="Percent" value=${pct(w.grossMargin)} sub="After cost of goods (real costs)" />
                <${Kpi} label="Net profit" icon="PiggyBank" value=${money.fmt(w.netProfit)} sub="After fees, packaging, shipping & ads" />
                <${Kpi} label="Profit per order" icon="Coins" value=${money.fmt(w.perOrder)} sub=${'AOV ' + money.fmt(d.kpis.aov)} />
            </div>
            <div className="an-grid wide-left">
                <${Panel} title="Contribution margin" sub="From net sales to true net profit. Cost of goods is real; fees, packaging and shipping are estimates; marketing is sample ad spend."
                    tag=${html`<${EstTag} text="Mixed sources" />`}>
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${290}>
                        <${R.BarChart} data=${steps} margin=${{ top: 16, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="label" tickLine=${false} axisLine=${false} interval=${0} tick=${{ fontSize: 11 }} />
                            <${R.YAxis} tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                            <${R.Tooltip} cursor=${{ fill: '#f5f1f8' }} formatter=${(v, n, item) => (n === 'base' ? [null, null] : [money.fmt(item.payload.amount), item.payload.kind])} />
                            <${R.ReferenceLine} y=${0} stroke="#cfc6d8" />
                            <${R.Bar} dataKey="base" stackId="w" fill="transparent" isAnimationActive=${false} />
                            <${R.Bar} dataKey="value" stackId="w" radius=${[6, 6, 6, 6]}>
                                ${steps.map((s, i) => html`<${R.Cell} key=${i} fill=${kindColor[s.kind]} />`)}
                            <//>
                        <//>
                    <//></div>
                    <div className="an-legend"><span><i style=${{ background: PLUM }}></i>Real</span><span><i style=${{ background: AMBER }}></i>Estimate</span>
                        <span><i style=${{ background: '#b5a6c4' }}></i>Sample</span><span><i style=${{ background: kindColor.derived }}></i>Net profit</span></div>
                <//>
                <${Panel} title="Unit economics" sub="Average order value against cost to acquire a customer." tag=${html`<${DemoTag} text="CAC is sample" />`}>
                    <${Gauge} value=${d.unit.ratio} max=${5} label="AOV ÷ CAC" sub=${'AOV ' + money.fmt(d.unit.aov) + ' (real) vs CAC ' + money.fmt(d.unit.cac) + '. Aim for 3× or more.'} />
                    <div className="an-mini-stats"><div><span>AOV</span><b>${money.fmt(d.unit.aov)}</b></div><div><span>CAC</span><b>${money.fmt(d.unit.cac)}</b></div><div><span>New customers</span><b>${num(d.unit.newCustomers)}</b></div></div>
                <//>
            </div>
            <${Panel} title="Product sales" sub="Per style, with size breakdown. Expand a row for size-level velocity.">
                <${SortTable} columns=${prodCols} rows=${d.products} csvName="product-sales" initialSort=${{ key: 'net', dir: 'desc' }} searchable expand=${sizeDetail} pageSize=${12} />
            <//>
            <div className="an-grid two">
                <${Panel} title="Returns & exchanges by SKU" sub=${num(d.returns.returnedUnits) + ' returned units. Return counts are real; the POS doesn\'t record reasons yet.'} tag=${html`<${DemoTag} text="Reasons are sample" />`}>
                    ${returnRows.length === 0 ? html`<p className="empty-note">No returns in this period.</p>` : html`<${Fragment}>
                        <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${Math.max(180, returnRows.length * 34)}>
                            <${R.BarChart} data=${returnRows} layout="vertical" margin=${{ top: 0, right: 16, left: 10, bottom: 0 }}>
                                <${R.XAxis} type="number" allowDecimals=${false} tickLine=${false} axisLine=${false} />
                                <${R.YAxis} type="category" dataKey="label" width=${150} tickLine=${false} axisLine=${false} tick=${{ fontSize: 11 }} />
                                <${R.Tooltip} content=${html`<${ChartTip} money=${money} unit=" units" />`} />
                                <${R.Legend} />
                                ${reasons.map((k, i) => html`<${R.Bar} key=${k} dataKey=${k} name=${k} stackId="r" fill=${SERIES[i % SERIES.length]} />`)}
                            <//>
                        <//></div>
                        <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${180}>
                            <${R.PieChart}><${R.Pie} data=${reasonPie} dataKey="value" nameKey="name" innerRadius=${45} outerRadius=${75} paddingAngle=${2}>
                                ${reasonPie.map((x, i) => html`<${R.Cell} key=${i} fill=${SERIES[i % SERIES.length]} />`)}<//>
                                <${R.Tooltip} /><${R.Legend} layout="vertical" align="right" verticalAlign="middle" /><//>
                        <//></div>
                    <//>`}
                <//>
                <${Panel} title="Inventory velocity" sub="Sell-through in the period by category, and how long unsold stock has been sitting.">
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${210}>
                        <${R.BarChart} data=${d.velocity.categories} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="category" tickLine=${false} axisLine=${false} tick=${{ fontSize: 11 }} interval=${0} />
                            <${R.YAxis} tickFormatter=${(v) => v + '%'} tickLine=${false} axisLine=${false} width=${44} />
                            <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                            <${R.Bar} dataKey="sellThrough" name="Sell-through" fill=${PLUM} radius=${[6, 6, 0, 0]} />
                        <//>
                    <//></div>
                    <div className="an-aging">${d.velocity.aging.map((a, i) => html`<div key=${a.bucket} className=${'an-age-card' + (i === 3 ? ' bad' : i === 2 ? ' warn' : '')}>
                        <span>${a.bucket}</span><b>${num(a.units)} units</b><small>${num(a.items)} items · ${money.fmt(a.value)} at cost</small></div>`)}</div>
                <//>
            </div>
            <${Panel} title="COD return-to-origin (RTO)" sub="Cash-on-delivery parcels that came back, by courier and region. 15%+ is flagged as a high-RTO zone." tag=${html`<${DemoTag} />`}>
                <div className="an-grid two flush">
                    <${SortTable} columns=${rtoCols('Courier')} rows=${d.rto.couriers} csvName="rto-couriers" initialSort=${{ key: 'rate', dir: 'desc' }} />
                    <${SortTable} columns=${rtoCols('Region')} rows=${d.rto.regions} csvName="rto-regions" initialSort=${{ key: 'rate', dir: 'desc' }} />
                </div>
            <//>
        <//>`;
    }

    function AnnualView({ d, money }) {
        const a = d.annual;
        const quarterRows = a.quarters.map((q) => {
            const row = { label: q.label, margin: q.margin };
            a.categories.forEach((c) => { row[c] = q.categories[c] ? q.categories[c].revenue : 0; });
            return row;
        });
        const split = [{ name: 'First-time', value: d.ltv.firstTimeShare }, { name: 'Returning', value: Math.round((100 - d.ltv.firstTimeShare) * 10) / 10 }];
        const s = d.shrinkage;
        const shrinkCols = [
            { key: 'reason', label: 'Reason' }, { key: 'units', label: 'Units', num: true },
            { key: 'cost', label: 'Cost impact', num: true, render: (r) => money.fmt(r.cost), csv: (r) => money.conv(r.cost).toFixed(2) },
        ];
        const heat = (v) => (v >= 100 ? PLUM : `rgba(105,64,110,${0.08 + Math.min(1, v / 40) * 0.8})`);
        return html`<${Fragment}>
            <div className="metric-grid">
                <${Kpi} featured label="Net sales" icon="Wallet" value=${money.fmt(d.kpis.net)} change=${delta(d.kpis.net, d.kpis.prev.net)} sub=${num(d.kpis.orders) + ' orders in the period'} />
                <${Kpi} label="Gross margin" icon="Percent" value=${pct(d.waterfall.grossMargin)} sub="Across all categories" />
                <${Kpi} label="Repeat purchase rate" icon="Repeat" demo value=${pct(d.ltv.repeatRate)} sub="Customers who bought again" />
                <${Kpi} label="12-month LTV" icon="Gem" demo value=${money.fmt(d.ltv.curve[11].ltv)} sub="Average revenue per customer" />
            </div>
            <${Panel} title="Category & seasonal margin" sub="Revenue by category (bars) and overall gross margin (line), by quarter.">
                ${quarterRows.length === 0 ? html`<p className="empty-note">No completed sales in this period.</p>` : html`<div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${300}>
                    <${R.ComposedChart} data=${quarterRows} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                        <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                        <${R.XAxis} dataKey="label" tickLine=${false} axisLine=${false} />
                        <${R.YAxis} yAxisId="m" tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                        <${R.YAxis} yAxisId="p" orientation="right" tickFormatter=${(v) => v + '%'} domain=${[0, 100]} tickLine=${false} axisLine=${false} width=${44} />
                        <${R.Tooltip} formatter=${(v, n) => (n === 'Gross margin' ? pct(v) : money.fmt(v))} />
                        <${R.Legend} />
                        ${a.categories.map((c, i) => html`<${R.Bar} key=${c} yAxisId="m" dataKey=${c} name=${c} fill=${SERIES[i % SERIES.length]} radius=${[4, 4, 0, 0]} />`)}
                        <${R.Line} yAxisId="p" dataKey="margin" name="Gross margin" stroke=${INK} strokeWidth=${2.5} dot=${{ r: 4 }} />
                    <//>
                <//></div>`}
            <//>
            <div className="an-grid two">
                <${Panel} title="Lifetime value & repeat customers" sub="Average cumulative revenue per customer over 12 months, and revenue from first-time vs returning customers." tag=${html`<${DemoTag} />`}>
                    <div className="an-ltv">
                        <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${230}>
                            <${R.AreaChart} data=${d.ltv.curve} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                                <defs><linearGradient id="ltvFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor=${PLUM} stopOpacity="0.35" /><stop offset="100%" stopColor=${PLUM} stopOpacity="0" /></linearGradient></defs>
                                <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                                <${R.XAxis} dataKey="month" tickLine=${false} axisLine=${false} />
                                <${R.YAxis} tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                                <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                                <${R.Area} dataKey="ltv" name="LTV" stroke=${PLUM} strokeWidth=${2.5} fill="url(#ltvFill)" />
                            <//>
                        <//></div>
                        <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${230}>
                            <${R.PieChart}><${R.Pie} data=${split} dataKey="value" nameKey="name" innerRadius=${50} outerRadius=${80} paddingAngle=${3}>
                                <${R.Cell} fill=${PLUM_SOFT} /><${R.Cell} fill=${PLUM} /><//>
                                <${R.Tooltip} formatter=${(v) => pct(v)} /><${R.Legend} verticalAlign="bottom" /><//>
                        <//></div>
                    </div>
                <//>
                <${Panel} title="Cohort retention" sub="Share of each month's new customers who bought again, months 0–12." tag=${html`<${DemoTag} />`}>
                    <div className="table-scroll" tabIndex="0" role="region" aria-label="Cohort retention">
                        <table className="an-cohort"><thead><tr><th>Cohort</th><th className="num">Customers</th>${Array.from({ length: 13 }, (_, m) => html`<th key=${m}>M${m}</th>`)}</tr></thead>
                            <tbody>${d.cohorts.rows.map((r) => html`<tr key=${r.cohort}><th>${r.cohort}</th><td className="num">${r.customers}</td>
                                ${Array.from({ length: 13 }, (_, m) => (m < r.cells.length
                                    ? html`<td key=${m} style=${{ background: heat(r.cells[m]), color: r.cells[m] >= 25 ? '#fff' : INK }}>${Math.round(r.cells[m])}%</td>`
                                    : html`<td key=${m} className="an-cohort-empty"></td>`))}</tr>`)}</tbody></table>
                    </div>
                <//>
            </div>
            <${Panel} title="Inventory shrinkage" sub="Stock written off as damaged, lost or corrected down at a count (stock adjustments that weren't sales), in the period.">
                <div className="an-grid three flush">
                    <div className="an-mini-stats vertical">
                        <div><span>System stock</span><b>${num(s.systemUnits)} units</b><small>${money.fmt(s.systemValue)} at cost</small></div>
                        <div><span>Written off</span><b className="an-neg">${num(s.lostUnits)} units</b><small>${money.fmt(s.lostCost)} cost impact</small></div>
                        <div><span>Found at counts</span><b>${num(s.foundUnits)} units</b><small>Shrinkage rate ${pct(s.rate, 2)}</small></div>
                    </div>
                    <div className="an-span2"><${SortTable} columns=${shrinkCols} rows=${s.rows} csvName="shrinkage" initialSort=${{ key: 'cost', dir: 'desc' }} empty="No stock was written off in this period." /></div>
                </div>
            <//>
        <//>`;
    }

    function TimingView({ d, money }) {
        const t = d.timing;
        const busiest = [...t.dayparts].sort((a, b) => b.orders - a.orders)[0];
        return html`<${Fragment}>
            <div className="metric-grid">
                <${Kpi} featured label="Best day" icon="CalendarDays" value=${t.bestDay || '—'} sub="Most completed orders" />
                <${Kpi} label="Peak hour" icon="Clock" value=${t.bestHour || '—'} sub="Busiest hour of the day" />
                <${Kpi} label="Busiest part of day" icon="Sun" value=${busiest && busiest.orders ? busiest.name : '—'} sub=${busiest && busiest.orders ? num(busiest.orders) + ' orders · ' + money.fmt(busiest.revenue) : 'No sales yet'} />
                <${Kpi} label="Orders" icon="ShoppingBag" value=${num(d.kpis.orders)} sub=${money.fmt(d.kpis.net) + ' net sales'} />
            </div>
            <div className="an-grid wide-left">
                <${Panel} title="Sales by hour" sub="Completed orders (bars) and net sales (line) for each hour of the day.">
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${270}>
                        <${R.ComposedChart} data=${t.hours} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="label" tickLine=${false} axisLine=${false} interval=${2} />
                            <${R.YAxis} yAxisId="o" allowDecimals=${false} tickLine=${false} axisLine=${false} width=${40} />
                            <${R.YAxis} yAxisId="r" orientation="right" tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                            <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                            <${R.Bar} yAxisId="o" dataKey="orders" name="Orders" fill=${PLUM_SOFT} radius=${[4, 4, 0, 0]} />
                            <${R.Line} yAxisId="r" dataKey="revenue" name="Net sales" stroke=${PLUM} strokeWidth=${2.5} dot=${false} />
                        <//>
                    <//></div>
                <//>
                <${Panel} title="Sales by weekday">
                    <div className="an-chart"><${R.ResponsiveContainer} width="100%" height=${270}>
                        <${R.BarChart} data=${t.weekdays} margin=${{ top: 10, right: 10, left: 0, bottom: 0 }}>
                            <${R.CartesianGrid} strokeDasharray="3 3" stroke="#eee8f3" vertical=${false} />
                            <${R.XAxis} dataKey="day" tickLine=${false} axisLine=${false} />
                            <${R.YAxis} tickFormatter=${money.short} tickLine=${false} axisLine=${false} width=${70} />
                            <${R.Tooltip} content=${html`<${ChartTip} money=${money} />`} />
                            <${R.Bar} dataKey="revenue" name="Net sales" radius=${[6, 6, 0, 0]}>
                                ${t.weekdays.map((w, i) => html`<${R.Cell} key=${i} fill=${w.day === t.bestDay ? PLUM : PLUM_SOFT} />`)}
                            <//>
                        <//>
                    <//></div>
                <//>
            </div>
            <${Heatmap} heatmap=${d.heatmap} money=${money} />
            <div className="an-dayparts">${t.dayparts.map((p) => html`<section key=${p.name} className="panel an-daypart">
                <span className="eyebrow">${String(p.hours[0]).padStart(2, '0')}:00–${String(p.hours[1] % 24).padStart(2, '0')}:00</span>
                <h3>${p.name}</h3><p className="an-daypart-figs"><b>${num(p.orders)}</b> orders · ${money.fmt(p.revenue)}</p>
                ${p.top.length ? html`<ol>${p.top.map((x) => html`<li key=${x.name}><span>${x.name}</span><b>${x.units}</b></li>`)}</ol>` : html`<p className="muted">No sales in this part of the day.</p>`}
            </section>`)}</div>
        <//>`;
    }

    // ---------------- app ----------------

    function App() {
        const [view, setView] = useState('daily');
        const [preset, setPreset] = useState('today');
        const [range, setRange] = useState(() => presetRange('today').map(iso));
        const [channel, setChannel] = useState('all');
        const [currency, setCurrency] = useState(() => { try { return localStorage.getItem('an-currency') || 'PHP'; } catch (e) { return 'PHP'; } });
        const [state, setState] = useState({ loading: true, data: null, error: null });
        const [tick, setTick] = useState(0);
        const money = useMemo(() => makeMoney(currency), [currency]);

        useEffect(() => { try { localStorage.setItem('an-currency', currency); } catch (e) { /* private mode */ } }, [currency]);
        useEffect(() => {
            const ctrl = new AbortController();
            setState((s) => ({ ...s, loading: true, error: null }));
            const qs = new URLSearchParams({ from: range[0], to: range[1], channel });
            fetch(CFG.endpoint + '?' + qs, { headers: { Accept: 'application/json' }, signal: ctrl.signal, credentials: 'same-origin' })
                .then((r) => r.json().then((j) => (r.ok ? j : Promise.reject(new Error((j.errors && Object.values(j.errors)[0][0]) || j.message || 'Request failed')))))
                .then((data) => setState({ loading: false, data, error: null }))
                .catch((e) => { if (e.name !== 'AbortError') setState((s) => ({ ...s, loading: false, error: e.message })); });
            return () => ctrl.abort();
        }, [range, channel, tick]);

        const pickView = useCallback((v) => {
            setView(v.key);
            setPreset(v.preset);
            setRange(presetRange(v.preset).map(iso));
        }, []);
        const pickPreset = (key) => { setPreset(key); if (key !== 'custom') setRange(presetRange(key).map(iso)); };
        const setDate = (i, value) => { if (!value) return; setPreset('custom'); setRange((r) => (i === 0 ? [value, value > r[1] ? value : r[1]] : [value < r[0] ? value : r[0], value])); };

        const d = state.data;
        const Body = { daily: DailyView, monthly: MonthlyView, annual: AnnualView, timing: TimingView }[view];
        return html`<div className=${'an-app' + (state.loading ? ' is-loading' : '')}>
            <nav className="an-tabs" aria-label="Report cadence">${VIEWS.map((v) => html`<button key=${v.key} type="button" aria-pressed=${view === v.key} onClick=${() => pickView(v)}>
                <${Icon} name=${v.icon} size=${16} />${v.label}</button>`)}</nav>
            <div className="panel an-filters" role="group" aria-label="Filters">
                <label className="an-field"><span>Date range</span>
                    <select value=${preset} onChange=${(e) => pickPreset(e.target.value)}>${PRESETS.map(([k, l]) => html`<option key=${k} value=${k}>${l}</option>`)}</select></label>
                <label className="an-field"><span>From</span><input type="date" value=${range[0]} max=${CFG.today} onChange=${(e) => setDate(0, e.target.value)} /></label>
                <label className="an-field"><span>To</span><input type="date" value=${range[1]} max=${CFG.today} onChange=${(e) => setDate(1, e.target.value)} /></label>
                <label className="an-field"><span>Sales channel</span>
                    <select value=${channel} onChange=${(e) => setChannel(e.target.value)}>${Object.entries(CFG.channels).map(([k, l]) => html`<option key=${k} value=${k}>${l}</option>`)}</select></label>
                <div className="an-field"><span>Currency</span><div className="an-seg" role="group" aria-label="Currency">
                    <button type="button" aria-pressed=${currency === 'PHP'} onClick=${() => setCurrency('PHP')}>₱ PHP</button>
                    <button type="button" aria-pressed=${currency === 'USD'} onClick=${() => setCurrency('USD')}>$ USD</button></div></div>
                <button type="button" className="btn an-refresh" onClick=${() => setTick(tick + 1)} aria-label="Refresh"><${Icon} name="RefreshCw" size=${15} className=${state.loading ? 'spin' : ''} /></button>
            </div>
            <div className="an-context">
                ${d && html`<span>Comparing ${d.meta.from === d.meta.to ? d.meta.from : d.meta.from + ' → ' + d.meta.to} with ${d.meta.prevFrom === d.meta.prevTo ? d.meta.prevFrom : d.meta.prevFrom + ' → ' + d.meta.prevTo}</span>`}
                ${currency === 'USD' && html`<span>$1 = ₱${CFG.usdRate.toFixed(2)}</span>`}
                <span className="an-legend-note"><${DemoTag} /> marks metrics the POS doesn't record yet</span>
            </div>
            ${state.error && html`<div className="flash bad" role="alert">Couldn't load the dashboard: ${state.error}</div>`}
            ${d ? html`<${Body} d=${d} money=${money} />` : !state.error && html`<div className="panel an-loading" role="status">Loading your dashboard…</div>`}
        </div>`;
    }

    ReactDOM.createRoot(root).render(html`<${App} />`);
})();
