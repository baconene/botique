<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Staff;
use App\Models\Pos\StaffRole;
use App\Services\Pos\PasswordHasher;
use App\Services\Pos\Permissions;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;
use Symfony\Component\HttpKernel\Exception\ConflictHttpException;

/** Users & roles administration (permission users.manage). */
class StaffController extends Controller
{
    public function permissions(): JsonResponse
    {
        return response()->json(['data' => collect(Permissions::ALL)->map(fn ($label, $key) => ['key' => $key, 'label' => $label])->values()]);
    }

    // ---------------- roles ----------------

    public function roles(): JsonResponse
    {
        return response()->json(['data' => StaffRole::orderBy('id')->get()->map->toApi()]);
    }

    public function storeRole(Request $request): JsonResponse
    {
        $v = $this->validateRole($request, null);
        $role = new StaffRole(['name' => $v['name'], 'permissions' => Permissions::toCsv($v['permissions'])]);
        $role->save();

        return response()->json(['data' => $role->toApi()], 201);
    }

    public function updateRole(Request $request, StaffRole $role): JsonResponse
    {
        if ($role->is_system) {
            throw new ConflictHttpException('The Admin role is built in and always has every permission.');
        }
        $v = $this->validateRole($request, $role);
        $role->fill(['name' => $v['name'], 'permissions' => Permissions::toCsv($v['permissions'])])->save();

        return response()->json(['data' => $role->toApi()]);
    }

    public function destroyRole(StaffRole $role): JsonResponse
    {
        if ($role->is_system) {
            throw new ConflictHttpException('The Admin role cannot be deleted.');
        }
        if ($role->staff()->exists()) {
            throw new ConflictHttpException('Move the users in this role to another role first.');
        }
        $role->delete();

        return response()->json(null, 204);
    }

    private function validateRole(Request $request, ?StaffRole $role): array
    {
        return $request->validate([
            'name' => ['required', 'string', 'max:64', Rule::unique('staff_roles', 'name')->ignore($role?->id)],
            'permissions' => ['present', 'array'],
            'permissions.*' => ['string', Rule::in(array_keys(Permissions::ALL))],
        ]);
    }

    // ---------------- users ----------------

    public function index(): JsonResponse
    {
        return response()->json(['data' => Staff::with('role')->orderBy('username')->get()->map->toApi()]);
    }

    public function store(Request $request): JsonResponse
    {
        $v = $this->validateStaff($request, null);
        $staff = new Staff([
            'username' => $v['username'],
            'display_name' => $v['display_name'] ?? '',
            'role_id' => $v['role_id'],
            'active' => $v['active'] ?? true,
        ]);
        $staff->password_hash = PasswordHasher::hash($v['password']);
        $staff->save();

        return response()->json(['data' => $staff->load('role')->toApi()], 201);
    }

    public function update(Request $request, Staff $staff): JsonResponse
    {
        $v = $this->validateStaff($request, $staff);

        DB::transaction(function () use ($v, $staff, $request) {
            $staff->fill(collect($v)->only(['username', 'display_name', 'role_id', 'active'])->all());
            if (array_key_exists('display_name', $v) && $v['display_name'] === null) {
                $staff->display_name = '';
            }
            $this->guardAdmins($staff, $request->user());
            $passwordChanged = ! empty($v['password']);
            if ($passwordChanged) {
                $staff->password_hash = PasswordHasher::hash($v['password']);
            }
            $staff->save();
            // Disabled users, role changes and password resets end that user's other sessions.
            if (! $staff->active || $passwordChanged || $staff->wasChanged('role_id')) {
                $current = $request->user()->currentAccessToken()?->id;
                $staff->tokens()->when($staff->id === $request->user()->id, fn ($q) => $q->where('id', '!=', $current))->delete();
            }
        });

        return response()->json(['data' => $staff->fresh('role')->toApi()]);
    }

    public function destroy(Request $request, Staff $staff): JsonResponse
    {
        if ($staff->id === $request->user()->id) {
            throw new ConflictHttpException('You cannot delete your own account.');
        }
        $staff->active = false;
        $this->guardAdmins($staff, $request->user());
        $staff->tokens()->delete();
        $staff->delete();

        return response()->json(null, 204);
    }

    /** At least one active Admin must always remain, and you cannot lock yourself out. */
    private function guardAdmins(Staff $changed, Staff $actor): void
    {
        if ($changed->id === $actor->id && ! $changed->active) {
            throw new ConflictHttpException('You cannot disable your own account.');
        }
        $wasAdmin = (int) $changed->getOriginal('role_id') === Permissions::ADMIN_ROLE_ID && (bool) $changed->getOriginal('active');
        $staysAdmin = $changed->role_id === Permissions::ADMIN_ROLE_ID && $changed->active;
        if ($wasAdmin && ! $staysAdmin) {
            $others = Staff::where('role_id', Permissions::ADMIN_ROLE_ID)->where('active', true)->where('id', '!=', $changed->id)->count();
            if ($others === 0) {
                throw new ConflictHttpException('There must always be at least one active Admin.');
            }
        }
    }

    private function validateStaff(Request $request, ?Staff $staff): array
    {
        $request->merge(['username' => strtolower(trim((string) $request->input('username', $staff?->username)))]);
        $req = $staff ? 'sometimes' : 'required';

        return $request->validate([
            'username' => [$req, 'string', 'min:3', 'max:64', 'regex:/^[a-z0-9._-]+$/', Rule::unique('staff', 'username')->ignore($staff?->id)],
            'display_name' => ['sometimes', 'nullable', 'string', 'max:100'],
            'password' => [$staff ? 'nullable' : 'required', 'string', 'min:6', 'max:200'],
            'role_id' => [$req, 'integer', 'exists:staff_roles,id'],
            'active' => ['sometimes', 'boolean'],
        ]);
    }
}
