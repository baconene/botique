@extends('layouts.admin')
@php($editing = $product !== null)
@php($src = $product ?? $template)
@section('title', $editing ? 'Edit '.$product->name : 'Add item')

@section('content')
<div class="page-head">
    <div>
        <a class="muted" href="{{ route('admin.inventory.index') }}">&larr; Inventory</a>
        <h1>{{ $editing ? 'Edit item' : ($template ? 'Add variant' : 'Add item') }}</h1>
        @if ($template)<p class="muted" style="margin:0">Copied from {{ $template->name }} ({{ trim($template->size.' / '.$template->color, ' /') }}). Change the size or color; a new barcode is assigned.</p>@endif
    </div>
</div>

@php($photo = $src?->image)
<form class="panel" method="post" enctype="multipart/form-data" action="{{ $editing ? route('admin.inventory.update', $product) : route('admin.inventory.store') }}" style="max-width: 820px">
    @csrf
    @if ($editing) @method('PUT') @endif
    @if ($template && $photo)<input type="hidden" name="copy_photo_from" value="{{ $template->id }}">@endif

    <div class="photo-editor">
        <div class="photo-preview" id="photo-preview" data-has="{{ $photo ? '1' : '0' }}">
            @if ($photo)
                <img src="{{ \App\Support\Look::img($photo->url(), 600) }}" alt="{{ $src->name }}" id="photo-img">
            @else
                <img alt="" id="photo-img" hidden>
                <div class="photo-empty" id="photo-empty">
                    @include('partials.art', ['category' => $src->category ?? '', 'color' => $src->color ?? ''])
                    <span>No photo yet</span>
                </div>
            @endif
        </div>
        <div class="photo-controls">
            <label>Photo</label>
            <p class="muted" style="margin:0 0 10px; font-size:.86rem">
                Shown on the online store and in this inventory.
                @if ($template && $photo) The photo of {{ $template->name }} is kept for this variant unless you choose another. @endif
            </p>
            <label class="btn ghost small file-btn">
                {{ $photo ? 'Replace photo…' : 'Upload photo…' }}
                <input type="file" name="photo" id="photo" accept="image/jpeg,image/png,image/webp">
            </label>
            <span class="muted" id="photo-name" style="font-size:.84rem; margin-left:8px">JPG, PNG or WebP, up to 5 MB</span>
            @error('photo')<div class="error">{{ $message }}</div>@enderror

            <label for="photo_url" style="margin-top:12px">…or image link (https)</label>
            <input id="photo_url" name="photo_url" type="url" placeholder="https://…" value="{{ old('photo_url', $editing && $photo && ! $photo->isUpload() ? $photo->path : '') }}">
            @error('photo_url')<div class="error">{{ $message }}</div>@enderror

            @if ($editing && ($sameColor || $sameStyle))
                <label for="photo_scope" style="margin-top:12px">Use the new photo for</label>
                <select id="photo_scope" name="photo_scope">
                    @if ($sameColor)<option value="color" @selected(old('photo_scope', 'color') === 'color')>All {{ $sameColor + 1 }} sizes in {{ $product->color ?: 'this colour' }}</option>@endif
                    @if ($sameStyle)<option value="style" @selected(old('photo_scope') === 'style')>Every size and colour of this style ({{ $sameStyle + 1 }} items)</option>@endif
                    <option value="item" @selected(old('photo_scope') === 'item')>Only this item ({{ trim($product->size.' / '.$product->color, ' /') ?: $product->barcode }})</option>
                </select>
            @else
                <input type="hidden" name="photo_scope" value="item">
            @endif

            @if ($editing && $photo)
                <label style="display:flex; gap:8px; align-items:center; margin-top:12px; font-weight:500">
                    <input type="checkbox" name="remove_photo" value="1" id="remove_photo" style="width:auto"> Remove the photo
                </label>
            @endif
        </div>
    </div>

    <div class="form-grid">
        <div class="field full">
            <label for="name">Item name *</label>
            <input id="name" name="name" required value="{{ old('name', $src->name ?? '') }}">
            @error('name')<div class="error">{{ $message }}</div>@enderror
        </div>
        <div class="field">
            <label for="barcode">Barcode</label>
            <input id="barcode" name="barcode" value="{{ old('barcode', $editing ? $product->barcode : '') }}" @if ($editing) required @endif
                   placeholder="{{ $editing ? '' : 'Leave blank to auto-generate (or scan the tag)' }}">
            @error('barcode')<div class="error">{{ $message }}</div>@enderror
        </div>
        <div class="field">
            <label for="category">Category</label>
            <input id="category" name="category" list="cats" value="{{ old('category', $src->category ?? '') }}">
            <datalist id="cats">@foreach ($categories as $c)<option value="{{ $c }}">@endforeach</datalist>
        </div>
        <div class="field">
            <label for="brand">Brand</label>
            <input id="brand" name="brand" value="{{ old('brand', $src->brand ?? '') }}">
        </div>
        <div class="field">
            <label for="size">Size</label>
            <input id="size" name="size" list="sizes" value="{{ old('size', $editing ? $product->size : '') }}" @if ($template) autofocus @endif>
            <datalist id="sizes">@foreach (['XXS','XS','S','M','L','XL','XXL','0','2','4','6','8','10','12','14','26','27','28','29','30','One Size'] as $s)<option value="{{ $s }}">@endforeach</datalist>
        </div>
        <div class="field">
            <label for="color">Color</label>
            <input id="color" name="color" value="{{ old('color', $src->color ?? '') }}">
        </div>
        <div class="field">
            <label for="price">Retail price *</label>
            <input id="price" name="price" inputmode="decimal" required value="{{ old('price', isset($src) ? number_format((float) $src->price, 2, '.', '') : '') }}">
            @error('price')<div class="error">{{ $message }}</div>@enderror
        </div>
        <div class="field">
            <label for="cost">Cost</label>
            <input id="cost" name="cost" inputmode="decimal" value="{{ old('cost', isset($src) ? number_format((float) $src->cost, 2, '.', '') : '') }}">
            @error('cost')<div class="error">{{ $message }}</div>@enderror
        </div>
        @if ($editing)
            <div class="field">
                <label for="quantity_delta">Stock change (now {{ $product->quantity }} on hand)</label>
                <input id="quantity_delta" name="quantity_delta" type="number" placeholder="e.g. 5 to add, -2 to remove" value="{{ old('quantity_delta') }}">
                @error('quantity_delta')<div class="error">{{ $message }}</div>@enderror
            </div>
        @else
            <div class="field">
                <label for="quantity">Quantity on hand</label>
                <input id="quantity" name="quantity" type="number" value="{{ old('quantity', 0) }}">
                @error('quantity')<div class="error">{{ $message }}</div>@enderror
            </div>
        @endif
        <div class="field">
            <label for="reorder_level">Low-stock alert at</label>
            <input id="reorder_level" name="reorder_level" type="number" min="0" value="{{ old('reorder_level', $src->reorder_level ?? 2) }}">
        </div>
        <div class="field full">
            <label for="notes">Notes</label>
            <textarea id="notes" name="notes" rows="3">{{ old('notes', $editing ? $product->notes : '') }}</textarea>
        </div>
    </div>
    <div style="display:flex; gap:10px">
        <button class="btn accent" type="submit">{{ $editing ? 'Save changes' : 'Add item' }}</button>
        <a class="btn ghost" href="{{ route('admin.inventory.index') }}">Cancel</a>
    </div>
    <p class="muted" style="margin-top: 1em; font-size: .85rem">Stock changes are applied as a change (+/−), so sales made on the registers at the same time are never lost.</p>
</form>
<script>
    // Live preview of the chosen photo or link before saving.
    (function () {
        var img = document.getElementById('photo-img'), empty = document.getElementById('photo-empty');
        var file = document.getElementById('photo'), link = document.getElementById('photo_url');
        var name = document.getElementById('photo-name'), remove = document.getElementById('remove_photo');
        var original = img.getAttribute('src');
        function show(src) {
            if (src) { img.src = src; img.hidden = false; if (empty) empty.hidden = true; }
            else if (original) { img.src = original; img.hidden = false; }
            else { img.hidden = true; if (empty) empty.hidden = false; }
            img.style.opacity = remove && remove.checked && !src ? .35 : 1;
        }
        file.addEventListener('change', function () {
            var f = file.files[0];
            name.textContent = f ? f.name + ' (' + Math.round(f.size / 1024) + ' KB) — saved when you press Save' : 'JPG, PNG or WebP, up to 5 MB';
            show(f ? URL.createObjectURL(f) : (/^https:\/\//.test(link.value) ? link.value : null));
        });
        link.addEventListener('change', function () { if (!file.files[0]) show(/^https:\/\//.test(link.value) ? link.value : null); });
        if (remove) remove.addEventListener('change', function () { show(null); });
    })();
</script>
@endsection
