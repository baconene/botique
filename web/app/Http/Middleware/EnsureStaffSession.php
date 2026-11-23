<?php

namespace App\Http\Middleware;

use App\Models\Pos\Staff;
use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Symfony\Component\HttpFoundation\Response;

/**
 * Back-office pages: the signed-in staff member must still be active and have a role with permissions.
 * (An admin disabling someone ends their web session on the next click.)
 */
class EnsureStaffSession
{
    public function handle(Request $request, Closure $next): Response
    {
        Auth::shouldUse('staff');
        $staff = Auth::guard('staff')->user();
        if (! $staff instanceof Staff || ! $staff->active || count($staff->permissions()) === 0) {
            Auth::guard('staff')->logout();
            $request->session()->invalidate();
            $request->session()->regenerateToken();

            return redirect()->route('login')->withErrors(['username' => 'Your account is disabled or has no access. Ask an admin.']);
        }

        return $next($request);
    }
}
