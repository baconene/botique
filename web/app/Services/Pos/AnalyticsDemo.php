<?php

namespace App\Services\Pos;

use Carbon\CarbonImmutable;

/**
 * Sample figures for dashboard metrics the POS does not record yet: ad spend, website traffic by device,
 * online-order fulfilment, courier COD returns, return reason codes and customer identity (LTV, cohorts).
 * Values are repeatable for the same dates (seeded), realistic for a Philippine apparel shop, and every
 * block carries 'demo' => true so the dashboard can label it clearly.
 */
class AnalyticsDemo
{
    private const FIRST = ['Andrea', 'Bea', 'Carla', 'Dani', 'Ella', 'Faye', 'Gia', 'Hana', 'Isa', 'Jam', 'Kim', 'Lia', 'Mika', 'Nina', 'Pia', 'Rica', 'Sam', 'Tin'];

    private const LAST = ['Santos', 'Reyes', 'Cruz', 'Bautista', 'Garcia', 'Mendoza', 'Torres', 'Flores', 'Ramos', 'Aquino', 'Lim', 'Tan'];

    private function seed(string $key): void
    {
        mt_srand(crc32($key));
    }

    private function between(float $min, float $max): float
    {
        return $min + (mt_rand() / mt_getrandmax()) * ($max - $min);
    }

    /** Daily ad spend per platform; blended ROAS uses the real net sales. */
    public function ads(CarbonImmutable $from, int $days, float $net, float $prevNet): array
    {
        $split = ['Meta' => 0.52, 'TikTok' => 0.31, 'Google' => 0.17];
        $spend = $this->spend($from, $days, $net);
        $prevSpend = $this->spend($from->subDays($days), $days, $prevNet);
        $platforms = [];
        $this->seed('ads-roas'.$from->toDateString());
        foreach ($split as $name => $share) {
            $s = round($spend * $share, 2);
            $roas = round($this->between(2.1, 4.6), 2);
            $platforms[] = ['name' => $name, 'spend' => $s, 'revenue' => round($s * $roas, 2), 'roas' => $roas];
        }

        return [
            'demo' => true, 'spend' => $spend, 'prevSpend' => $prevSpend, 'platforms' => $platforms,
            'blendedRoas' => $spend > 0 ? round($net / $spend, 2) : 0.0,
            'prevRoas' => $prevSpend > 0 ? round($prevNet / $prevSpend, 2) : 0.0,
        ];
    }

    /** Sample spend sized to the shop: about 10–16 % of real net sales plus a small always-on budget. */
    private function spend(CarbonImmutable $from, int $days, float $net): float
    {
        $this->seed('spend'.$from->toDateString().$days);
        $ratio = $this->between(0.10, 0.16);
        $baseline = $days * 150 * $this->between(0.8, 1.2);

        return round(max(0, $net) * $ratio + $baseline, 2);
    }

    /** Sessions and conversion by device (mobile ≈ 75 % of traffic). Flags a conversion drop of 20 %+. */
    public function devices(CarbonImmutable $from, int $days, int $orders, int $prevOrders): array
    {
        $profile = ['Mobile' => [0.75, 1.7], 'Desktop' => [0.19, 2.9], 'Tablet' => [0.06, 2.2]];
        $rows = [];
        foreach ($profile as $device => [$share, $cvr]) {
            $this->seed('dev'.$device.$from->toDateString().$days);
            $sessions = (int) round($days * $this->between(820, 1100) * $share);
            $rate = round($cvr * $this->between(0.72, 1.12), 2);
            $this->seed('dev-prev'.$device.$from->toDateString().$days);
            $prevRate = round($cvr * $this->between(0.9, 1.1), 2);
            $rows[] = [
                'device' => $device, 'sessions' => $sessions, 'share' => round($share * 100, 1),
                'cvr' => $rate, 'prevCvr' => $prevRate, 'orders' => (int) round($sessions * $rate / 100),
                'drop' => $rate < $prevRate * 0.8,
            ];
        }

        return ['demo' => true, 'rows' => $rows];
    }

    /** Online orders not yet shipped, with their age. */
    public function unfulfilled(CarbonImmutable $asOf): array
    {
        $this->seed('unfulfilled'.$asOf->toDateString());
        $couriers = ['J&T Express', 'LBC', 'Ninja Van', 'Flash Express'];
        $regions = ['Metro Manila', 'Luzon', 'Visayas', 'Mindanao'];
        $rows = [];
        $n = mt_rand(6, 10);
        for ($i = 0; $i < $n; $i++) {
            $age = (int) round($this->between(3, 76));
            $rows[] = [
                'order' => 'ON-'.mt_rand(24000, 26999),
                'customer' => self::FIRST[mt_rand(0, count(self::FIRST) - 1)].' '.self::LAST[mt_rand(0, count(self::LAST) - 1)][0].'.',
                'items' => mt_rand(1, 4), 'ageHours' => $age,
                'bucket' => $age >= 48 ? '48h+' : ($age >= 24 ? '24–48h' : '< 24h'),
                'courier' => $couriers[mt_rand(0, 3)], 'region' => $regions[mt_rand(0, 3)],
                'payment' => mt_rand(0, 9) < 6 ? 'COD' : 'Prepaid',
                'amount' => round($this->between(690, 4800), 2),
            ];
        }
        usort($rows, fn ($a, $b) => $b['ageHours'] <=> $a['ageHours']);

        return ['demo' => true, 'rows' => $rows];
    }

    /** CAC from sample ad spend and new customers; AOV is real. */
    public function unitEconomics(float $aov, float $spend, int $orders): array
    {
        $newCustomers = max(1, (int) round(max(1, $orders) * 0.62));
        $cac = round($spend / $newCustomers, 2);

        return ['aov' => $aov, 'cac' => $cac, 'cacDemo' => true, 'newCustomers' => $newCustomers,
            'ratio' => $cac > 0 ? round($aov / $cac, 2) : 0.0];
    }

    /** Cash-on-delivery return-to-origin rate by courier and region. */
    public function rto(CarbonImmutable $from, int $days): array
    {
        $this->seed('rto'.$from->toDateString().$days);
        $couriers = [];
        foreach (['J&T Express' => 11, 'LBC' => 8, 'Ninja Van' => 13, 'Flash Express' => 16] as $name => $base) {
            $orders = (int) round($days * $this->between(4, 9));
            $rate = round($base * $this->between(0.8, 1.25), 1);
            $couriers[] = ['name' => $name, 'orders' => $orders, 'rto' => (int) round($orders * $rate / 100), 'rate' => $rate, 'high' => $rate >= 15];
        }
        $regions = [];
        foreach (['Metro Manila' => 7.5, 'Luzon' => 11.5, 'Visayas' => 15.5, 'Mindanao' => 19] as $name => $base) {
            $orders = (int) round($days * $this->between(3, 10));
            $rate = round($base * $this->between(0.85, 1.2), 1);
            $regions[] = ['name' => $name, 'orders' => $orders, 'rto' => (int) round($orders * $rate / 100), 'rate' => $rate, 'high' => $rate >= 15];
        }

        return ['demo' => true, 'couriers' => $couriers, 'regions' => $regions];
    }

    /** @return array<string, int> reason => units (sums to $units) */
    public function returnReasons(string $sku, int $units): array
    {
        $this->seed('reasons'.$sku);
        $reasons = ['Too small' => $this->between(2, 5), 'Too large' => $this->between(1, 3), 'Fabric / quality' => $this->between(0.5, 2),
            'Changed mind' => $this->between(0.5, 2.5), 'Not as pictured' => $this->between(0.2, 1.2)];
        $sum = array_sum($reasons);
        $out = [];
        $left = $units;
        foreach ($reasons as $r => $w) {
            $n = (int) floor($units * $w / $sum);
            $out[$r] = $n;
            $left -= $n;
        }
        arsort($reasons);
        foreach (array_keys($reasons) as $r) {
            if ($left <= 0) {
                break;
            }
            $out[$r]++;
            $left--;
        }

        return array_filter($out);
    }

    /** 12-month lifetime value curve and first-time vs returning split. */
    public function ltv(float $aov): array
    {
        $this->seed('ltv');
        $base = $aov > 0 ? $aov : 1650;
        $curve = [];
        $value = $base;
        $repeat = 0.34;
        for ($m = 1; $m <= 12; $m++) {
            $curve[] = ['month' => 'M'.$m, 'ltv' => round($value, 2)];
            $value += $base * $repeat;
            $repeat *= $this->between(0.78, 0.86);
        }

        return ['demo' => true, 'curve' => $curve, 'repeatRate' => round($this->between(24, 33), 1),
            'firstTimeShare' => round($this->between(58, 68), 1)];
    }

    /** Repeat-purchase retention by monthly acquisition cohort (month 0 = 100 %). */
    public function cohorts(CarbonImmutable $asOf): array
    {
        $rows = [];
        for ($c = 11; $c >= 0; $c--) {
            $month = $asOf->subDay()->startOfMonth()->subMonths($c);
            $this->seed('cohort'.$month->format('Y-m'));
            $cells = [100.0];
            $rate = $this->between(19, 31);
            for ($m = 1; $m <= $c && $m <= 12; $m++) {
                $cells[] = round($rate, 1);
                $rate *= $this->between(0.8, 0.93);
            }
            $rows[] = ['cohort' => $month->format('M Y'), 'customers' => mt_rand(80, 260), 'cells' => $cells];
        }

        return ['demo' => true, 'rows' => $rows];
    }
}
