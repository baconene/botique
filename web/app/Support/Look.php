<?php

namespace App\Support;

/**
 * Presentation helpers for the storefront and back office: color swatches, garment shapes, money.
 * Items without a photo are drawn as tinted garment illustrations.
 */
final class Look
{
    private const COLORS = [
        'black' => '#26221f', 'white' => '#f6f4ef', 'ivory' => '#f1e9d8', 'cream' => '#efe4cc', 'natural' => '#d9c8a6',
        'beige' => '#d8c4a4', 'camel' => '#c19a6b', 'tan' => '#c8a27a', 'brown' => '#7a5236', 'chocolate' => '#5a3a28',
        'sage' => '#9caf88', 'olive' => '#7b7f45', 'green' => '#4f7d5a', 'emerald' => '#2f7a5f', 'mint' => '#a8d5ba',
        'indigo' => '#3f4b7a', 'navy' => '#24304f', 'blue' => '#4a6fa5', 'denim' => '#5b7aa6', 'sky' => '#9cc3e4',
        'red' => '#b23a3a', 'rust' => '#b7552e', 'terracotta' => '#c0643f', 'burgundy' => '#6e2633', 'wine' => '#6b2737',
        'pink' => '#e8a7b8', 'blush' => '#efc6c4', 'rose' => '#d98c98', 'coral' => '#e9806e', 'lilac' => '#b9a6d3',
        'purple' => '#6e4a8e', 'plum' => '#6e3456', 'yellow' => '#e9c46a', 'mustard' => '#c9a227', 'gold' => '#c8a24a',
        'orange' => '#e07a3f', 'grey' => '#9a9a9a', 'gray' => '#9a9a9a', 'charcoal' => '#4a4a4a', 'silver' => '#c4c4c4',
    ];

    public static function hex(?string $color): string
    {
        $c = mb_strtolower(trim((string) $color));
        foreach (self::COLORS as $name => $hex) {
            if ($c === $name || str_contains($c, $name)) {
                return $hex;
            }
        }
        // Unknown color names still get a stable, pleasant tint.
        $h = crc32($c) % 360;

        return "hsl({$h}, 28%, 62%)";
    }

    /** Light colors need dark outlines and text. */
    public static function isLight(?string $color): bool
    {
        $c = mb_strtolower((string) $color);
        foreach (['white', 'ivory', 'cream', 'natural', 'beige', 'mint', 'sky', 'blush', 'silver', 'yellow'] as $light) {
            if (str_contains($c, $light)) {
                return true;
            }
        }

        return false;
    }

    /** Garment silhouette (SVG path data, 200x240 viewBox) for a category. */
    public static function shape(?string $category): string
    {
        $c = mb_strtolower((string) $category);

        return match (true) {
            str_contains($c, 'dress') => 'M78 20 L122 20 L130 48 L118 62 L150 220 L50 220 L82 62 L70 48 Z',
            str_contains($c, 'knit'), str_contains($c, 'sweater'), str_contains($c, 'outer'), str_contains($c, 'jacket'), str_contains($c, 'coat')
                => 'M70 24 L100 34 L130 24 L178 56 L164 104 L146 96 L146 216 L54 216 L54 96 L36 104 L22 56 Z',
            str_contains($c, 'bottom'), str_contains($c, 'jean'), str_contains($c, 'pant'), str_contains($c, 'skirt')
                => 'M62 22 L138 22 L150 220 L110 220 L100 92 L90 220 L50 220 Z',
            str_contains($c, 'accessor'), str_contains($c, 'bag'), str_contains($c, 'tote')
                => 'M52 90 L148 90 L160 210 L40 210 Z M74 90 C74 40 126 40 126 90 L114 90 C114 56 86 56 86 90 Z',
            str_contains($c, 'shoe') => 'M30 160 L96 150 L122 120 L150 124 L170 170 L170 196 L30 196 Z',
            str_contains($c, 'jewel') => 'M100 60 A50 50 0 1 1 99.9 60 Z M100 80 A30 30 0 1 0 100.1 80 Z',
            default => 'M74 28 L100 40 L126 28 L170 58 L152 92 L140 86 L140 214 L60 214 L60 86 L48 92 L30 58 Z',   // tops
        };
    }

    /**
     * Image URL at a given width. Unsplash images are resized and cropped (4:5) by their CDN;
     * uploads and other links are returned unchanged.
     */
    public static function img(?string $url, int $width = 800): ?string
    {
        if (! $url) {
            return null;
        }
        if (str_starts_with($url, 'https://images.unsplash.com/')) {
            $base = strtok($url, '?');
            $h = (int) round($width * 1.25);

            return "{$base}?auto=format&fit=crop&crop=entropy&w={$width}&h={$h}&q=72";
        }

        return $url;
    }

    public static function money(float|int|string|null $amount): string
    {
        return config('pos.currency_symbol').number_format((float) $amount, 2);
    }

    public static function priceRange(float $min, float $max): string
    {
        return $min == $max ? self::money($min) : self::money($min).' – '.self::money($max);
    }
}
