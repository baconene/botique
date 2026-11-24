@extends('layouts.admin')
@section('title', 'Settings')
@section('content')
<div class="page-head"><div><span class="eyebrow">Make it yours</span><h1>Settings & configuration</h1><p class="muted">The details behind a well-run boutique.</p></div></div>
<nav class="section-tabs" aria-label="Settings sections">
@if($canStore)<a href="#store-details">Store details</a><a href="#catalogue">Catalogue lists</a>@endif
@if($canUsers)<a href="#user-types">User types</a><a href="#team">Staff accounts</a>@endif
</nav>
@if($canStore)
<section class="panel section-space" id="store-details">
<div class="section-heading"><div><span class="eyebrow">01 / Your boutique</span><h2>Store details</h2></div><span class="badge">Shared with registers</span></div>
<form method="post" action="{{ route('admin.settings.store') }}">@csrf @method('PUT')
<div class="form-grid">
@foreach(['StoreName'=>'Store name','Address'=>'Address','Phone'=>'Phone','TaxRatePercent'=>'Tax rate (%)','BarcodePrefix'=>'Barcode prefix'] as $key=>$label)
<div class="field"><label for="{{ $key }}">{{ $label }}</label><input id="{{ $key }}" name="{{ $key }}" value="{{ old($key, $settings[$key]) }}" @if($key==='StoreName') required maxlength="100" @elseif($key==='TaxRatePercent') type="number" min="0" max="30" step=".001" @elseif($key==='BarcodePrefix') maxlength="6" @endif></div>
@endforeach
<div class="field full"><label for="ReceiptFooter">Receipt footer</label><textarea id="ReceiptFooter" name="ReceiptFooter" rows="3" maxlength="1000">{{ old('ReceiptFooter', $settings['ReceiptFooter']) }}</textarea></div>
</div><button class="btn accent">Save store details</button>
</form></section>
<section id="catalogue" class="section-space">
<div class="section-heading"><div><span class="eyebrow">02 / Catalogue configuration</span><h2>A place for every piece</h2></div></div>
<p class="muted">One value per line. These lists suggest values in the inventory form; custom values from existing products remain available. Removing a suggestion does not rename or delete products.</p>
<div class="two-col">
@foreach(['category'=>'Category types','brand'=>'Brands','size'=>'Sizes','color'=>'Colors'] as $field=>$label)
<form class="panel" method="post" action="{{ route('admin.settings.references') }}">@csrf @method('PUT')
<input type="hidden" name="field" value="{{ $field }}">
<div class="field"><label for="reference-{{ $field }}">{{ $label }}</label><textarea id="reference-{{ $field }}" name="values" rows="6" maxlength="15000">{{ old('field')===$field ? old('values') : implode("\n", $references[$field]) }}</textarea><small class="muted">Up to 200 values, {{ \App\Services\Pos\ReferenceData::FIELDS[$field] }} characters each.</small></div>
<button class="btn" type="submit">Save {{ strtolower($label) }}</button>
</form>
@endforeach
</div></section>
@endif
@if($canUsers)
<section id="user-types" class="section-space">
<div class="section-heading"><div><span class="eyebrow">03 / Access configuration</span><h2>User types & permissions</h2></div></div>
<p class="muted">User types are shared with the POS. Admin is protected and always has full access. Assigned types cannot be deleted.</p>
<div class="settings-stack">
@foreach($roles as $role)
@php($editingRole = old('_form') === 'role-'.$role->id)
<details class="panel disclosure" @if($editingRole) open @endif>
<summary><strong>{{ $role->name }}</strong><span class="muted">{{ $role->staff_count }} {{ Str::plural('account', $role->staff_count) }} · {{ $role->is_system ? 'Protected' : 'Edit access' }}</span></summary>
@if($role->is_system)<p>Full access to the boutique. This built-in type cannot be changed.</p>
@else
<form method="post" action="{{ route('admin.settings.roles.update', $role) }}">@csrf @method('PUT')
<input type="hidden" name="_form" value="role-{{ $role->id }}">
<div class="field"><label for="role-{{ $role->id }}">Type name</label><input id="role-{{ $role->id }}" name="name" value="{{ $editingRole ? old('name') : $role->name }}" required maxlength="64"></div>
<div class="permission-grid">@foreach($permissions as $key=>$label)<label><input type="checkbox" name="permissions[]" value="{{ $key }}" @checked(in_array($key,$editingRole ? old('permissions', []) : $role->permissionList()))>{{ $label }}</label>@endforeach</div>
<button class="btn accent">Save {{ $role->name }}</button>
</form>
@if($role->staff_count===0)<form method="post" class="delete-type" action="{{ route('admin.settings.roles.delete', $role) }}">@csrf @method('DELETE')<button class="btn small">Delete unused type</button></form>@endif
@endif
</details>
@endforeach
<details class="panel disclosure" @if(old('_form')==='new-role') open @endif><summary><strong>Add a user type</strong><span class="muted">Choose its permissions</span></summary>
<form method="post" action="{{ route('admin.settings.roles.create') }}">@csrf
<input type="hidden" name="_form" value="new-role">
<div class="field"><label for="new-role">Type name</label><input id="new-role" name="name" value="{{ old('_form')==='new-role' ? old('name') : '' }}" required maxlength="64" placeholder="e.g. Boutique supervisor"></div>
<div class="permission-grid">@foreach($permissions as $key=>$label)<label><input type="checkbox" name="permissions[]" value="{{ $key }}" @checked(old('_form')==='new-role' && in_array($key,old('permissions',[])))>{{ $label }}</label>@endforeach</div>
<button class="btn accent">Create user type</button></form>
</details>
</div></section>
<section id="team" class="section-space">
<div class="section-heading"><div><span class="eyebrow">04 / Your team</span><h2>Staff accounts</h2></div><span class="badge">{{ $users->count() }} accounts</span></div>
<div class="settings-stack">
@foreach($users->push(null) as $account)
@php($accountKey = 'staff-'.($account?->id ?? 'new'))
@php($editingAccount = old('_form') === $accountKey)
<details class="panel disclosure" @if($editingAccount) open @endif><summary><strong>{{ $account?->cashierName() ?? 'Add a staff account' }}</strong><span class="muted">{{ $account ? $account->role?->name.' · '.($account->active ? 'Active' : 'Inactive') : 'Set up your next team member' }}</span></summary>
<form method="post" action="{{ $account ? route('admin.settings.staff.update', $account) : route('admin.settings.staff.create') }}">@csrf @if($account) @method('PUT') @endif
<input type="hidden" name="_form" value="{{ $accountKey }}">
<div class="form-grid">
<div class="field"><label for="username-{{ $account?->id ?? 'new' }}">Username</label><input id="username-{{ $account?->id ?? 'new' }}" name="username" value="{{ $editingAccount ? old('username') : $account?->username }}" required minlength="3" maxlength="64" autocomplete="off"></div>
<div class="field"><label for="display-{{ $account?->id ?? 'new' }}">Display name</label><input id="display-{{ $account?->id ?? 'new' }}" name="display_name" value="{{ $editingAccount ? old('display_name') : $account?->display_name }}" maxlength="100"></div>
<div class="field"><label for="type-{{ $account?->id ?? 'new' }}">User type</label><select id="type-{{ $account?->id ?? 'new' }}" name="role_id" required>@foreach($roles as $role)<option value="{{ $role->id }}" @selected((int)($editingAccount ? old('role_id') : ($account?->role_id ?? 3))===$role->id)>{{ $role->name }}</option>@endforeach</select></div>
<div class="field"><label for="active-{{ $account?->id ?? 'new' }}">Status</label><select id="active-{{ $account?->id ?? 'new' }}" name="active"><option value="1" @selected($editingAccount ? old('active') : ($account?->active ?? true))>Active</option><option value="0" @selected($editingAccount ? !old('active') : ($account && !$account->active))>Inactive</option></select></div>
<div class="field full"><label for="password-{{ $account?->id ?? 'new' }}">{{ $account ? 'New password (leave blank to keep)' : 'Password' }}</label><input id="password-{{ $account?->id ?? 'new' }}" type="password" name="password" autocomplete="new-password" minlength="6" maxlength="200" @required(!$account)></div>
</div><button class="btn accent">{{ $account ? 'Save account' : 'Create account' }}</button>
</form></details>
@endforeach
</div></section>
@endif
<section class="panel section-space"><h3>Managed elsewhere</h3><p class="muted">Product prices, stock and reorder levels are managed in Inventory. Currency and timezone are deployment settings. Payment methods follow the POS checkout contract. Scanner and printer preferences stay on each register.</p></section>
@endsection
