<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

/**
 * Staff sign-in for POS registers. The register sends username + password; on success it gets the
 * user's role and permissions plus a session token that expires (config sanctum.expiration).
 * No register tokens need to be created by hand.
 */
class AuthController extends Controller
{
    public function login(Request $request): JsonResponse
    {
        $v = $request->validate([
            'username' => ['required', 'string', 'max:64'],
            'password' => ['required', 'string', 'max:200'],
            'device' => ['nullable', 'string', 'max:40'],
        ]);

        $staff = Staff::with('role')->where('username', strtolower(trim($v['username'])))->first();
        if (! PasswordHasher::verify($v['password'], $staff?->password_hash)) {
            return response()->json(['message' => 'Wrong username or password.'], 401);
        }
        if (! $staff->active) {
            return response()->json(['message' => 'This account has been disabled. Ask an admin.'], 403);
        }
        if (count($staff->permissions()) === 0) {
            return response()->json(['message' => 'Your role has no permissions in the POS.'], 403);
        }

        $staff->forceFill(['last_login' => now()])->saveQuietly();
        $minutes = config('sanctum.expiration');
        $expires = $minutes ? now()->addMinutes((int) $minutes) : null;
        $token = $staff->createToken(trim($v['device'] ?? '') ?: 'POS', ['*'], $expires)->plainTextToken;

        return response()->json([
            'token' => $token,
            'expires_at' => $expires?->format('Y-m-d\TH:i:sP'),
            'user' => $staff->toApi(),
        ]);
    }

    public function me(Request $request): JsonResponse
    {
        return response()->json(['data' => $request->user()->load('role')->toApi()]);
    }

    /** Ends this session (the token stops working immediately). */
    public function logout(Request $request): JsonResponse
    {
        $request->user()->currentAccessToken()?->delete();

        return response()->json(null, 204);
    }
}
