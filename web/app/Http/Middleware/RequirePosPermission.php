<?php

namespace App\Http\Middleware;

use App\Services\Pos\Permissions;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * Route middleware "pos.can:perm1,perm2": the signed-in staff member needs ANY of the listed permissions.
 */
class RequirePosPermission
{
    public function handle(Request $request, Closure $next, string ...$permissions): Response
    {
        $staff = $request->user();
        foreach ($permissions as $p) {
            if ($staff->hasPermission($p)) {
                return $next($request);
            }
        }
        $names = implode(' or ', array_map(fn ($p) => Permissions::ALL[$p] ?? $p, $permissions));

        $message = "Your role does not allow this ({$names}).";
        if ($request->expectsJson()) {
            return response()->json(['message' => $message], 403);
        }
        abort(403, $message);
    }
}
