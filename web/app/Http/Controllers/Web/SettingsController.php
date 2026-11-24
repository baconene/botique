<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Api\V1\StaffController;
use App\Http\Controllers\Api\V1\StoreController;
use App\Http\Controllers\Controller;
use App\Models\Pos\Staff;
use App\Models\Pos\StaffRole;
use App\Services\Pos\Permissions;
use App\Services\Pos\PosSettings;
use App\Services\Pos\ReferenceData;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\ValidationException;
use Illuminate\View\View;
use Symfony\Component\HttpKernel\Exception\ConflictHttpException;

class SettingsController extends Controller
{
    public function index(Request $request, PosSettings $settings, ReferenceData $references): View
    {
        $canStore = $request->user()->hasPermission('settings.manage');
        $canUsers = $request->user()->hasPermission('users.manage');

        return view('admin.settings', [
            'canStore' => $canStore, 'canUsers' => $canUsers,
            'settings' => $canStore ? $settings->all() : [],
            'references' => $canStore ? collect(ReferenceData::FIELDS)->map(fn ($max, $field) => $references->options($field))->all() : [],
            'roles' => $canUsers ? StaffRole::withCount('staff')->orderBy('id')->get() : collect(),
            'users' => $canUsers ? Staff::with('role')->orderBy('username')->get() : collect(),
            'permissions' => Permissions::ALL,
        ]);
    }

    public function store(Request $request, StoreController $store): RedirectResponse
    {
        $store->updateSettings($request);

        return back()->with('status', 'Store settings saved.');
    }

    public function references(Request $request, ReferenceData $references): RedirectResponse
    {
        $v = $request->validate(['field' => ['required', 'in:category,brand,size,color'], 'values' => ['nullable', 'string', 'max:15000']]);
        $values = collect(preg_split('/\R/u', $v['values'] ?? ''))->map(fn ($s) => trim($s))->filter(fn ($s) => $s !== '')->unique(fn ($s) => mb_strtolower($s))->values()->all();
        if (count($values) > 200 || collect($values)->contains(fn ($s) => mb_strlen($s) > ReferenceData::FIELDS[$v['field']])) {
            throw ValidationException::withMessages(['values' => 'Use at most 200 values, within the field length limit.']);
        }
        $references->save($v['field'], $values);

        return back()->with('status', 'Reference list saved. Values already used by products remain available.');
    }

    public function role(Request $request, StaffController $staff, ?StaffRole $role = null): RedirectResponse
    {
        $request->merge(['permissions' => $request->input('permissions', [])]);
        try {
            $role ? $staff->updateRole($request, $role) : $staff->storeRole($request);
        } catch (ConflictHttpException $e) {
            throw ValidationException::withMessages(['role' => $e->getMessage()]);
        }

        return back()->with('status', 'User type saved.');
    }

    public function deleteRole(StaffRole $role, StaffController $staff): RedirectResponse
    {
        try {
            $staff->destroyRole($role);
        } catch (ConflictHttpException $e) {
            throw ValidationException::withMessages(['role' => $e->getMessage()]);
        }

        return back()->with('status', 'Unused user type deleted.');
    }

    public function user(Request $request, StaffController $controller, ?Staff $staff = null): RedirectResponse
    {
        try {
            $staff ? $controller->update($request, $staff) : $controller->store($request);
        } catch (ConflictHttpException $e) {
            throw ValidationException::withMessages(['staff' => $e->getMessage()]);
        }

        return back()->with('status', 'Staff account saved.');
    }
}
