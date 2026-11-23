<?php

namespace App\Http\Middleware;

use App\Models\Pos\Register;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * Only active POS register tokens may use the POS API. Also records when each register was last seen.
 */
class EnsurePosRegister
{
    public function handle(Request $request, Closure $next): Response
    {
        $register = $request->user();
        if (! $register instanceof Register) {
            return response()->json(['message' => 'This token does not belong to a POS register.'], 403);
        }
        if ($register->isRevoked()) {
            return response()->json(['message' => 'This register has been revoked.'], 403);
        }
        if ($register->last_seen_at === null || $register->last_seen_at->lt(now()->subMinute())) {
            $register->forceFill(['last_seen_at' => now()])->saveQuietly();
        }

        return $next($request);
    }
}
