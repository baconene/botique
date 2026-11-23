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

<form class="panel" method="post" action="{{ $editing ? route('admin.inventory.update', $product) : route('admin.inventory.store') }}" style="max-width: 820px">
    @csrf
    @if ($editing) @method('PUT') @endif
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
@endsection
