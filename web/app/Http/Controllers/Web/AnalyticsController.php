<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Services\Pos\Analytics;
use Carbon\CarbonImmutable;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\ValidationException;
use Illuminate\View\View;

/** Analytics dashboard: Daily operations, Monthly profitability, Annual strategy and Sales timing. */
class AnalyticsController extends Controller
{
    public function index(): View
    {
        return view('admin.analytics', [
            'channels' => Analytics::CHANNELS,
            'usdRate' => (float) config('pos.usd_rate'),
        ]);
    }

    /** All dashboard figures for a date range (inclusive) and sales channel, as JSON. */
    public function data(Request $request, Analytics $analytics): JsonResponse
    {
        $v = $request->validate([
            'from' => ['nullable', 'date_format:Y-m-d'],
            'to' => ['nullable', 'date_format:Y-m-d'],
            'channel' => ['nullable', 'in:'.implode(',', array_keys(Analytics::CHANNELS))],
        ]);
        $to = CarbonImmutable::parse($v['to'] ?? today()->toDateString());
        $from = CarbonImmutable::parse($v['from'] ?? $to->toDateString());
        if ($to->lt($from) || $from->diffInDays($to) > 730) {
            throw ValidationException::withMessages(['to' => 'Choose an end date after the start, within two years.']);
        }

        return response()->json($analytics->build(['from' => $from, 'to' => $to, 'channel' => $v['channel'] ?? 'all']));
    }
}
