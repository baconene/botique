@extends('layouts.admin')
@section('title', 'Point of sale')

@section('content')
<div class="page-head">
    <div>
        <span class="eyebrow">Web register · {{ auth('staff')->user()->cashierName() }}</span>
        <h1>Point of sale</h1>
    </div>
    <button class="btn ghost small" type="button" id="new-sale">New sale</button>
</div>

<div class="pos" id="pos"
     data-search="{{ route('admin.pos.search') }}"
     data-sale="{{ route('admin.pos.sale') }}"
     data-tax="{{ $store['TaxRatePercent'] }}"
     data-currency="{{ config('pos.currency_symbol') }}"
     data-discount="{{ $canDiscount ? '1' : '0' }}">
    <div>
        <div class="panel">
            <div class="pos-scan">
                <input id="scan" type="search" autocomplete="off" autofocus
                       placeholder="Scan a barcode or type an item name, then press Enter">
                <button class="btn" type="button" id="scan-go">Add</button>
            </div>
            <div class="results" id="results"></div>
            <div class="pos-msg" id="msg">Bluetooth and USB scanners work here: scan into the box above.</div>
        </div>
        <div class="panel" style="margin-top: 16px; padding: 0; overflow-x: auto">
            <table class="data cart">
                <thead><tr><th>Item</th><th>Size / color</th><th class="num">Price</th><th class="num">Qty</th><th class="num">Total</th><th></th></tr></thead>
                <tbody id="cart"><tr><td colspan="6" class="muted" style="text-align:center; padding: 28px">No items yet.</td></tr></tbody>
            </table>
        </div>
    </div>

    <div class="panel summary">
        <div class="row"><span>Subtotal</span><strong id="sub">–</strong></div>
        <div class="row">
            <span>Discount %</span>
            <input id="disc" type="number" min="0" max="100" step="0.5" value="0" style="width: 90px; text-align:right" @disabled(!$canDiscount)
                   title="{{ $canDiscount ? '' : 'Your role does not allow discounts' }}">
        </div>
        <div class="row"><span>Discount</span><span id="discAmt">–</span></div>
        <div class="row"><span>Tax ({{ rtrim(rtrim(number_format((float) $store['TaxRatePercent'], 3), '0'), '.') }}%)</span><span id="tax">–</span></div>
        <div class="row total"><span>Total</span><span id="total">–</span></div>

        <label style="margin-top: 1em">Payment</label>
        <div class="paymethods">
            <button type="button" data-m="Cash" class="on">Cash</button>
            <button type="button" data-m="Card">Card</button>
            <button type="button" data-m="Other">Other</button>
        </div>
        <div id="cash-box">
            <label for="tendered">Cash received</label>
            <input id="tendered" inputmode="decimal" placeholder="Leave blank for exact amount">
            <div class="quick">
                <button class="btn small ghost" type="button" data-q="0">Exact</button>
                <button class="btn small ghost" type="button" data-q="500">{{ config('pos.currency_symbol') }}500</button>
                <button class="btn small ghost" type="button" data-q="1000">{{ config('pos.currency_symbol') }}1,000</button>
            </div>
            <div class="row"><span>Change due</span><strong id="change">–</strong></div>
        </div>
        <button class="btn ok block" type="button" id="complete" style="margin-top: 14px; padding: 1em; font-size: 1.05rem">Complete sale</button>
        <p class="muted" style="font-size:.8rem; margin-top:.8em">The server calculates the final total, tax and stock, exactly like the in-store register.</p>
    </div>
</div>

<div class="modal" id="done">
    <div class="box">
        <span class="eyebrow">Sale complete</span>
        <div class="big" id="done-total">–</div>
        <p id="done-detail" class="muted"></p>
        <div style="display:flex; gap:10px; justify-content:center; margin-top: 16px">
            <a class="btn ghost" id="done-receipt" href="#" target="_blank">Print receipt</a>
            <button class="btn accent" type="button" id="done-new">New sale</button>
        </div>
    </div>
</div>
@endsection

@push('scripts')
<script src="{{ asset('js/pos.js') }}?v=1"></script>
@endpush
