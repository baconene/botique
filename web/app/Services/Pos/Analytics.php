<?php

namespace App\Services\Pos;

use Carbon\CarbonImmutable;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\DB;

/**
 * Builds every figure for the analytics dashboard from the POS tables (sales, sale_lines, products,
 * stock_movements). Metrics the POS does not record (ad spend, web traffic, couriers, customer identity,
 * return reasons, online fulfilment) come from AnalyticsDemo and are flagged with 'demo' => true.
 *
 * Money is in pesos (the POS currency); the dashboard converts to US dollars on screen.
 */
class Analytics
{
    public const CHANNELS = ['all' => 'All channels', 'registers' => 'In-store registers', 'web' => 'Web POS'];

    private const DAYPARTS = [
        'Morning' => [6, 11], 'Lunch' => [11, 14], 'Afternoon' => [14, 18], 'Evening' => [18, 24], 'Late night' => [0, 6],
    ];

    public function __construct(private AnalyticsDemo $demo) {}

    /**
     * @param  array{from: CarbonImmutable, to: CarbonImmutable, channel: string}  $f  'to' is inclusive (a date)
     * @return array<string, mixed>
     */
    public function build(array $f): array
    {
        $from = $f['from']->startOfDay();
        $to = $f['to']->startOfDay()->addDay();          // exclusive end
        $days = max(1, (int) $from->diffInDays($to));
        $prevFrom = $from->subDays($days);
        $channel = array_key_exists($f['channel'], self::CHANNELS) ? $f['channel'] : 'all';

        $sales = $this->sales($from, $to, $channel);
        $prevSales = $this->sales($prevFrom, $from, $channel);
        $lines = $this->lines($sales->pluck('number')->all());
        $products = DB::table('products')->get()->keyBy('id');

        $kpis = $this->kpis($sales, $lines);
        $kpis['prev'] = $this->kpis($prevSales, $this->lines($prevSales->pluck('number')->all()));
        $ads = $this->demo->ads($from, $days, $kpis['net'], $kpis['prev']['net']);

        return [
            'meta' => [
                'from' => $from->toDateString(), 'to' => $to->subDay()->toDateString(), 'days' => $days,
                'prevFrom' => $prevFrom->toDateString(), 'prevTo' => $from->subDay()->toDateString(),
                'channel' => $channel, 'channels' => self::CHANNELS,
                'currency' => ['symbol' => config('pos.currency_symbol'), 'usdRate' => (float) config('pos.usd_rate')],
                'generated' => now()->toIso8601String(),
                'costs' => config('pos.costs'),
            ],
            'kpis' => $kpis,
            'ads' => $ads,
            'devices' => $this->demo->devices($from, $days, $kpis['orders'], $kpis['prev']['orders']),
            'heatmap' => $this->heatmap($sales),
            'lowStock' => $this->lowStock($products),
            'unfulfilled' => $this->demo->unfulfilled($to),
            'waterfall' => $this->waterfall($sales, $lines, $products, $kpis, $ads['spend']),
            'products' => $this->productTable($sales, $lines, $products),
            'returns' => $this->returns($sales, $lines),
            'velocity' => $this->velocity($lines, $products, $sales),
            'unit' => $this->demo->unitEconomics($kpis['aov'], $ads['spend'], $kpis['orders']),
            'rto' => $this->demo->rto($from, $days),
            'annual' => $this->quarterly($sales, $lines, $products),
            'ltv' => $this->demo->ltv($kpis['aov']),
            'cohorts' => $this->demo->cohorts($to),
            'shrinkage' => $this->shrinkage($from, $to, $products),
            'timing' => $this->timing($sales, $lines),
        ];
    }

    private function sales(CarbonImmutable $from, CarbonImmutable $to, string $channel): Collection
    {
        $q = DB::table('sales')->where('sale_date', '>=', $from)->where('sale_date', '<', $to);
        if ($channel === 'web') {
            $q->where('terminal', 'like', 'Web POS%');
        } elseif ($channel === 'registers') {
            $q->where('terminal', 'not like', 'Web POS%');
        }

        return $q->get()->map(function ($s) {
            $s->refunded = (bool) $s->refunded;
            $s->date = CarbonImmutable::parse($s->sale_date);

            return $s;
        })->keyBy('number');
    }

    private function lines(array $numbers): Collection
    {
        if ($numbers === []) {
            return collect();
        }

        return collect(array_chunk($numbers, 500))->flatMap(fn ($chunk) => DB::table('sale_lines')->whereIn('sale_number', $chunk)->get());
    }

    /** @return array<string, float|int> */
    private function kpis(Collection $sales, Collection $lines): array
    {
        $done = $sales->where('refunded', false);
        $gross = (float) $sales->sum('subtotal');
        $cancel = (float) $sales->where('refunded', true)->sum('subtotal');
        $disc = (float) $done->sum('discount_amount');
        $net = round($gross - $cancel - $disc, 2);
        $orders = $done->count();
        $doneNumbers = $done->keys()->flip();

        return [
            'gross' => round($gross, 2), 'cancellations' => round($cancel, 2), 'discounts' => round($disc, 2),
            'net' => $net, 'tax' => round((float) $done->sum('tax'), 2), 'collected' => round((float) $done->sum('total'), 2),
            'orders' => $orders, 'refundedOrders' => $sales->where('refunded', true)->count(),
            'units' => (int) $lines->filter(fn ($l) => isset($doneNumbers[$l->sale_number]))->sum('quantity'),
            'aov' => $orders ? round($net / $orders, 2) : 0.0,
        ];
    }

    /** Completed orders and net sales by weekday (0 = Monday) and hour. */
    private function heatmap(Collection $sales): array
    {
        $counts = array_fill(0, 7, array_fill(0, 24, 0));
        $revenue = array_fill(0, 7, array_fill(0, 24, 0.0));
        foreach ($sales->where('refunded', false) as $s) {
            $d = ($s->date->dayOfWeekIso - 1);
            $h = (int) $s->date->format('G');
            $counts[$d][$h]++;
            $revenue[$d][$h] = round($revenue[$d][$h] + (float) $s->subtotal - (float) $s->discount_amount, 2);
        }

        return ['days' => ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'], 'counts' => $counts, 'revenue' => $revenue];
    }

    private function lowStock(Collection $products): array
    {
        $since = now()->subDays(30);
        $sold = DB::table('sale_lines')->join('sales', 'sales.number', '=', 'sale_lines.sale_number')
            ->where('sales.refunded', false)->where('sales.sale_date', '>=', $since)
            ->selectRaw('sale_lines.product_id, SUM(sale_lines.quantity) as units')->groupBy('sale_lines.product_id')
            ->pluck('units', 'product_id');

        return $products->filter(fn ($p) => $p->quantity <= $p->reorder_level)
            ->map(function ($p) use ($sold) {
                $perDay = ((int) ($sold[$p->id] ?? 0)) / 30;
                $cover = $perDay > 0 ? round(max(0, $p->quantity) / $perDay, 1) : null;
                $status = $p->quantity <= 0 ? 'Out of stock' : (($cover !== null && $cover < 7) || $p->quantity <= 1 ? 'Restock now' : 'Low');

                return [
                    'barcode' => $p->barcode, 'name' => $p->name, 'category' => $p->category, 'size' => $p->size, 'color' => $p->color,
                    'onHand' => (int) $p->quantity, 'reorder' => (int) $p->reorder_level, 'sold30' => (int) ($sold[$p->id] ?? 0),
                    'cover' => $cover, 'status' => $status,
                ];
            })
            ->sortBy([['onHand', 'asc'], ['sold30', 'desc']])->values()->take(60)->all();
    }

    /** Contribution margin: net sales minus real cost of goods and estimated fees, packaging, shipping and ad spend. */
    private function waterfall(Collection $sales, Collection $lines, Collection $products, array $kpis, float $adSpend): array
    {
        $costs = config('pos.costs');
        $done = $sales->where('refunded', false);
        $doneNumbers = $done->keys()->flip();
        $cogs = round((float) $lines->filter(fn ($l) => isset($doneNumbers[$l->sale_number]))
            ->sum(fn ($l) => $l->quantity * (float) ($products[$l->product_id]->cost ?? 0)), 2);
        $fees = 0.0;
        foreach ($done as $s) {
            $base = (float) $s->subtotal - (float) $s->discount_amount;
            $method = strtolower((string) $s->payment_method);
            $fees += $method === 'cash' ? 0 : $base * ($method === 'card' ? $costs['card_fee_percent'] : $costs['other_fee_percent']) / 100;
        }
        $orders = $kpis['orders'];
        $packaging = round($orders * $costs['packaging_per_order'], 2);
        $shipping = round($orders * $costs['shipping_per_order'], 2);
        $profit = round($kpis['net'] - $cogs - $fees - $packaging - $shipping - $adSpend, 2);

        return [
            'steps' => [
                ['label' => 'Net sales', 'amount' => $kpis['net'], 'kind' => 'real', 'type' => 'total'],
                ['label' => 'Cost of goods', 'amount' => -$cogs, 'kind' => 'real', 'type' => 'cost'],
                ['label' => 'Payment fees', 'amount' => -round($fees, 2), 'kind' => 'estimate', 'type' => 'cost'],
                ['label' => 'Packaging', 'amount' => -$packaging, 'kind' => 'estimate', 'type' => 'cost'],
                ['label' => 'Shipping', 'amount' => -$shipping, 'kind' => 'estimate', 'type' => 'cost'],
                ['label' => 'Marketing', 'amount' => -round($adSpend, 2), 'kind' => 'demo', 'type' => 'cost'],
                ['label' => 'Net profit', 'amount' => $profit, 'kind' => 'derived', 'type' => 'total'],
            ],
            'netProfit' => $profit,
            'perOrder' => $orders ? round($profit / $orders, 2) : 0.0,
            'grossMargin' => $kpis['net'] > 0 ? round(($kpis['net'] - $cogs) / $kpis['net'] * 100, 1) : 0.0,
            'orders' => $orders,
        ];
    }

    /** Per-style sales with size breakdown, returns and margin. */
    private function productTable(Collection $sales, Collection $lines, Collection $products): array
    {
        $rows = [];
        foreach ($lines as $l) {
            $sale = $sales[$l->sale_number] ?? null;
            if (! $sale) {
                continue;
            }
            $p = $products[$l->product_id] ?? null;
            $key = mb_strtolower($l->name.'|'.($p->brand ?? ''));
            $r = $rows[$key] ?? ['name' => $l->name, 'brand' => $p->brand ?? '', 'category' => $p->category ?? 'Uncategorized',
                'sizes' => [], 'units' => 0, 'returned' => 0, 'gross' => 0.0, 'net' => 0.0, 'cost' => 0.0];
            $qty = (int) $l->quantity;
            $size = $l->size !== '' ? $l->size : 'One size';
            if ($sale->refunded) {
                $r['returned'] += $qty;
            } else {
                $lineGross = $qty * (float) $l->unit_price;
                $share = (float) $sale->subtotal > 0 ? $lineGross / (float) $sale->subtotal : 0;
                $r['units'] += $qty;
                $r['sizes'][$size] = ($r['sizes'][$size] ?? 0) + $qty;
                $r['gross'] += $lineGross;
                $r['net'] += $lineGross - $share * (float) $sale->discount_amount;
                $r['cost'] += $qty * (float) ($p->cost ?? 0);
            }
            $rows[$key] = $r;
        }

        return collect($rows)->map(function ($r) {
            $r['gross'] = round($r['gross'], 2);
            $r['net'] = round($r['net'], 2);
            $r['cost'] = round($r['cost'], 2);
            $r['returnRate'] = ($r['units'] + $r['returned']) > 0 ? round($r['returned'] / ($r['units'] + $r['returned']) * 100, 1) : 0.0;
            $r['margin'] = $r['net'] > 0 ? round(($r['net'] - $r['cost']) / $r['net'] * 100, 1) : 0.0;
            $r['sizes'] = $this->sortSizes($r['sizes']);

            return $r;
        })->sortByDesc('net')->values()->all();
    }

    /** Return rates by SKU (real); reason codes are sample data because the POS doesn't ask for a reason. */
    private function returns(Collection $sales, Collection $lines): array
    {
        $bySku = [];
        foreach ($lines as $l) {
            $sale = $sales[$l->sale_number] ?? null;
            if (! $sale) {
                continue;
            }
            $k = $l->barcode !== '' ? $l->barcode : $l->product_id;
            $r = $bySku[$k] ?? ['sku' => $k, 'name' => $l->name, 'size' => $l->size, 'color' => $l->color, 'sold' => 0, 'returned' => 0];
            if ($sale->refunded) {
                $r['returned'] += (int) $l->quantity;
            } else {
                $r['sold'] += (int) $l->quantity;
            }
            $bySku[$k] = $r;
        }
        $rows = collect($bySku)->filter(fn ($r) => $r['returned'] > 0)->map(function ($r) {
            $r['rate'] = round($r['returned'] / max(1, $r['sold'] + $r['returned']) * 100, 1);
            $r['reasons'] = $this->demo->returnReasons($r['sku'], $r['returned']);

            return $r;
        })->sortByDesc('returned')->values()->take(15);
        $totals = [];
        foreach ($rows as $r) {
            foreach ($r['reasons'] as $reason => $n) {
                $totals[$reason] = ($totals[$reason] ?? 0) + $n;
            }
        }

        return ['rows' => $rows->all(), 'reasonTotals' => $totals, 'reasonsDemo' => true,
            'returnedUnits' => (int) $rows->sum('returned')];
    }

    /** Sell-through by category in the period, and how long unsold stock has been sitting. */
    private function velocity(Collection $lines, Collection $products, Collection $sales): array
    {
        $doneNumbers = $sales->where('refunded', false)->keys()->flip();
        $soldByProduct = [];
        foreach ($lines as $l) {
            if (isset($doneNumbers[$l->sale_number])) {
                $soldByProduct[$l->product_id] = ($soldByProduct[$l->product_id] ?? 0) + (int) $l->quantity;
            }
        }
        $cats = [];
        foreach ($products as $p) {
            $c = $p->category !== '' ? $p->category : 'Uncategorized';
            $cats[$c] ??= ['category' => $c, 'sold' => 0, 'onHand' => 0];
            $cats[$c]['sold'] += $soldByProduct[$p->id] ?? 0;
            $cats[$c]['onHand'] += max(0, (int) $p->quantity);
        }
        $categories = collect($cats)->map(function ($c) {
            $c['sellThrough'] = ($c['sold'] + $c['onHand']) > 0 ? round($c['sold'] / ($c['sold'] + $c['onHand']) * 100, 1) : 0.0;

            return $c;
        })->sortByDesc('sellThrough')->values()->all();

        $lastSold = DB::table('sale_lines')->join('sales', 'sales.number', '=', 'sale_lines.sale_number')
            ->where('sales.refunded', false)->selectRaw('sale_lines.product_id, MAX(sales.sale_date) as last')
            ->groupBy('sale_lines.product_id')->pluck('last', 'product_id');
        $buckets = ['Active (< 30 days)' => [0, 30], '30–59 days' => [30, 60], '60–89 days' => [60, 90], '90+ days' => [90, PHP_INT_MAX]];
        $aging = [];
        foreach ($buckets as $label => $_) {
            $aging[$label] = ['bucket' => $label, 'items' => 0, 'units' => 0, 'value' => 0.0];
        }
        $now = CarbonImmutable::now();
        foreach ($products as $p) {
            if ($p->quantity <= 0) {
                continue;
            }
            $since = CarbonImmutable::parse($lastSold[$p->id] ?? $p->created);
            $age = (int) $since->diffInDays($now);
            foreach ($buckets as $label => [$lo, $hi]) {
                if ($age >= $lo && $age < $hi) {
                    $aging[$label]['items']++;
                    $aging[$label]['units'] += (int) $p->quantity;
                    $aging[$label]['value'] = round($aging[$label]['value'] + $p->quantity * (float) $p->cost, 2);
                }
            }
        }

        return ['categories' => $categories, 'aging' => array_values($aging)];
    }

    /** Revenue and gross margin by category and quarter within the selected range. */
    private function quarterly(Collection $sales, Collection $lines, Collection $products): array
    {
        $data = [];
        $cats = [];
        foreach ($lines as $l) {
            $s = $sales[$l->sale_number] ?? null;
            if (! $s || $s->refunded) {
                continue;
            }
            $q = 'Q'.$s->date->quarter.' '.$s->date->year;
            $p = $products[$l->product_id] ?? null;
            $c = ($p->category ?? '') !== '' ? $p->category : 'Uncategorized';
            $cats[$c] = true;
            $rev = $l->quantity * (float) $l->unit_price;
            $data[$q][$c]['revenue'] = ($data[$q][$c]['revenue'] ?? 0) + $rev;
            $data[$q][$c]['cost'] = ($data[$q][$c]['cost'] ?? 0) + $l->quantity * (float) ($p->cost ?? 0);
        }
        uksort($data, fn ($a, $b) => strcmp(substr($a, 3).substr($a, 0, 2), substr($b, 3).substr($b, 0, 2)));
        $quarters = [];
        foreach ($data as $label => $byCat) {
            $row = ['label' => $label, 'revenue' => 0.0, 'cost' => 0.0, 'categories' => []];
            foreach ($byCat as $c => $v) {
                $row['categories'][$c] = ['revenue' => round($v['revenue'], 2),
                    'margin' => $v['revenue'] > 0 ? round(($v['revenue'] - $v['cost']) / $v['revenue'] * 100, 1) : 0.0];
                $row['revenue'] += $v['revenue'];
                $row['cost'] += $v['cost'];
            }
            $row['margin'] = $row['revenue'] > 0 ? round(($row['revenue'] - $row['cost']) / $row['revenue'] * 100, 1) : 0.0;
            $row['revenue'] = round($row['revenue'], 2);
            unset($row['cost']);
            $quarters[] = $row;
        }
        $categories = array_keys($cats);
        sort($categories);

        return ['quarters' => $quarters, 'categories' => $categories];
    }

    /** Stock written off (negative adjustments that aren't sales) and corrections found when counting. */
    private function shrinkage(CarbonImmutable $from, CarbonImmutable $to, Collection $products): array
    {
        $moves = DB::table('stock_movements')->where('created', '>=', $from)->where('created', '<', $to)
            ->whereNotIn('reason', ['sale', 'refund', 'new item', 'demo'])->get();
        $byReason = [];
        $found = 0;
        foreach ($moves as $m) {
            if ($m->change_qty >= 0) {
                $found += $m->change_qty;

                continue;
            }
            $reason = $m->reason !== '' ? ucfirst($m->reason) : 'Unspecified';
            $byReason[$reason] ??= ['reason' => $reason, 'units' => 0, 'cost' => 0.0];
            $byReason[$reason]['units'] += -$m->change_qty;
            $byReason[$reason]['cost'] = round($byReason[$reason]['cost'] + -$m->change_qty * (float) ($products[$m->product_id]->cost ?? 0), 2);
        }
        $rows = collect($byReason)->sortByDesc('cost')->values();
        $systemUnits = (int) $products->sum(fn ($p) => max(0, (int) $p->quantity));
        $lost = (int) $rows->sum('units');

        return [
            'rows' => $rows->all(), 'lostUnits' => $lost, 'lostCost' => round((float) $rows->sum('cost'), 2),
            'foundUnits' => (int) $found, 'systemUnits' => $systemUnits,
            'systemValue' => round((float) $products->sum(fn ($p) => max(0, (int) $p->quantity) * (float) $p->cost), 2),
            'rate' => ($systemUnits + $lost) > 0 ? round($lost / ($systemUnits + $lost) * 100, 2) : 0.0,
        ];
    }

    /** When things sell: by hour, by weekday and the best sellers in each part of the day. */
    private function timing(Collection $sales, Collection $lines): array
    {
        $done = $sales->where('refunded', false);
        $hours = [];
        for ($h = 0; $h < 24; $h++) {
            $hours[$h] = ['hour' => $h, 'label' => CarbonImmutable::today()->setTime($h, 0)->format('ga'), 'orders' => 0, 'revenue' => 0.0];
        }
        $weekdays = [];
        foreach (['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as $i => $d) {
            $weekdays[$i] = ['day' => $d, 'orders' => 0, 'revenue' => 0.0];
        }
        $parts = [];
        foreach (self::DAYPARTS as $name => $_) {
            $parts[$name] = ['name' => $name, 'hours' => self::DAYPARTS[$name], 'orders' => 0, 'revenue' => 0.0, 'items' => []];
        }
        foreach ($done as $s) {
            $h = (int) $s->date->format('G');
            $net = (float) $s->subtotal - (float) $s->discount_amount;
            $hours[$h]['orders']++;
            $hours[$h]['revenue'] += $net;
            $weekdays[$s->date->dayOfWeekIso - 1]['orders']++;
            $weekdays[$s->date->dayOfWeekIso - 1]['revenue'] += $net;
            $parts[$this->daypart($h)]['orders']++;
            $parts[$this->daypart($h)]['revenue'] += $net;
        }
        foreach ($lines as $l) {
            $s = $done[$l->sale_number] ?? null;
            if ($s) {
                $part = $this->daypart((int) $s->date->format('G'));
                $parts[$part]['items'][$l->name] = ($parts[$part]['items'][$l->name] ?? 0) + (int) $l->quantity;
            }
        }
        $parts = collect($parts)->map(function ($p) {
            arsort($p['items']);
            $p['top'] = collect($p['items'])->take(3)->map(fn ($u, $n) => ['name' => $n, 'units' => $u])->values()->all();
            unset($p['items']);
            $p['revenue'] = round($p['revenue'], 2);

            return $p;
        })->values()->all();
        $hours = collect($hours)->map(fn ($h) => [...$h, 'revenue' => round($h['revenue'], 2)])->values();
        $weekdays = collect($weekdays)->map(fn ($d) => [...$d, 'revenue' => round($d['revenue'], 2)])->values();
        $bestHour = $hours->sortByDesc('orders')->first();
        $bestDay = $weekdays->sortByDesc('orders')->first();

        return [
            'hours' => $hours->all(), 'weekdays' => $weekdays->all(), 'dayparts' => $parts,
            'bestHour' => $bestHour['orders'] > 0 ? $bestHour['label'] : null,
            'bestDay' => $bestDay['orders'] > 0 ? $bestDay['day'] : null,
        ];
    }

    private function daypart(int $hour): string
    {
        foreach (self::DAYPARTS as $name => [$lo, $hi]) {
            if ($hour >= $lo && $hour < $hi) {
                return $name;
            }
        }

        return 'Late night';
    }

    /** @param  array<string, int>  $sizes */
    private function sortSizes(array $sizes): array
    {
        $order = ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL'];
        uksort($sizes, function ($a, $b) use ($order) {
            $ia = array_search(strtoupper($a), $order, true);
            $ib = array_search(strtoupper($b), $order, true);
            if ($ia !== false && $ib !== false) {
                return $ia <=> $ib;
            }
            if (is_numeric($a) && is_numeric($b)) {
                return (float) $a <=> (float) $b;
            }

            return ($ia === false) <=> ($ib === false) ?: strcmp($a, $b);
        });

        return $sizes;
    }
}
