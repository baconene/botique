<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use App\Services\Pos\PosSettings;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\View\View;

/**
 * Staff sign-in for the website back office. Same accounts, passwords and roles as the
 * Windows POS (staff / staff_roles tables, PBKDF2 hashes).
 */
class StaffAuthController extends Controller
{
    public function show(PosSettings $settings): View|RedirectResponse
    {
        if (Auth::guard('staff')->check()) {
            return redirect()->route('admin.dashboard');
        }

        return view('auth.login', ['store' => $settings->all()]);
    }

    public function login(Request $request): RedirectResponse
    {
        $v = $request->validate([
            'username' => ['required', 'string', 'max:64'],
            'password' => ['required', 'string', 'max:200'],
        ]);

        $staff = Staff::with('role')->where('username', strtolower(trim($v['username'])))->first();
        if (! PasswordHasher::verify($v['password'], $staff?->password_hash)) {
            return back()->withErrors(['username' => 'Wrong username or password.'])->onlyInput('username');
        }
        if (! $staff->active) {
            return back()->withErrors(['username' => 'This account has been disabled. Ask an admin.'])->onlyInput('username');
        }
        if (count($staff->permissions()) === 0) {
            return back()->withErrors(['username' => 'Your role has no access to the back office.'])->onlyInput('username');
        }

        Auth::guard('staff')->login($staff);
        $request->session()->regenerate();
        $staff->forceFill(['last_login' => now()])->saveQuietly();

        return redirect()->intended(route('admin.dashboard'));
    }

    public function logout(Request $request): RedirectResponse
    {
        Auth::guard('staff')->logout();
        $request->session()->invalidate();
        $request->session()->regenerateToken();

        return redirect()->route('home')->with('status', 'You have signed out.');
    }
}
