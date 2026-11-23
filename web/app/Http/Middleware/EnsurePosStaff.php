<?php

namespace App\Http\Middleware;

use App\Models\Pos\Staff;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/** Only signed-in, active POS staff may use the POS API. */
class EnsurePosStaff
{
    public function handle(Request $request, Closure $next): Response
    {
        $staff = $request->user();
        if (! $staff instanceof Staff) {
            return response()->json(['message' => 'Please sign in with a POS staff account.'], 403);
        }
        if (! $staff->active) {
            $staff->currentAccessToken()?->delete();

            return response()->json(['message' => 'This account has been disabled.'], 403);
        }

        return $next($request);
    }
}
