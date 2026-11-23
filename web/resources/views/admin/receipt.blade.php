@php($L = \App\Support\Look::class)
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Receipt {{ $sale->code }}</title>
    <link rel="stylesheet" href="{{ asset('css/app.css') }}?v=1">
</head>
<body>
<div class="receipt">
    <h2>{{ $store['StoreName'] }}</h2>
    <div class="c">{{ $store['Address'] }}</div>
    <div class="c">{{ $store['Phone'] }}</div>
    <hr>
    <div class="r"><span>Receipt {{ $sale->code }}</span><span>{{ $sale->sale_date->format('Y-m-d H:i') }}</span></div>
    @if ($sale->cashier)<div>Cashier: {{ $sale->cashier }}</div>@endif
    <hr>
    @foreach ($sale->lines as $l)
        <div>{{ $l->name }}</div>
        <div class="r"><span>&nbsp;&nbsp;{{ $l->quantity }} x {{ $L::money($l->unit_price) }} {{ trim($l->size.' / '.$l->color, ' /') }}</span><span>{{ $L::money($l->unit_price * $l->quantity) }}</span></div>
    @endforeach
    <hr>
    <div class="r"><span>Subtotal</span><span>{{ $L::money($sale->subtotal) }}</span></div>
    @if ((float) $sale->discount_amount > 0)
        <div class="r"><span>Discount ({{ rtrim(rtrim($sale->discount_percent, '0'), '.') }}%)</span><span>-{{ $L::money($sale->discount_amount) }}</span></div>
    @endif
    <div class="r"><span>Tax ({{ rtrim(rtrim($sale->tax_rate, '0'), '.') }}%)</span><span>{{ $L::money($sale->tax) }}</span></div>
    <div class="r" style="font-weight:700; font-size: 14px"><span>TOTAL</span><span>{{ $L::money($sale->total) }}</span></div>
    <br>
    <div class="r"><span>{{ $sale->payment_method }}</span><span>{{ $L::money($sale->amount_tendered) }}</span></div>
    @if ($sale->payment_method === 'Cash')<div class="r"><span>Change</span><span>{{ $L::money($sale->change_due) }}</span></div>@endif
    @if ($sale->refunded)<hr><div class="c"><strong>*** REFUNDED {{ $sale->refund_date?->format('Y-m-d') }} ***</strong></div>@endif
    <hr>
    @foreach (preg_split('/\r?\n/', (string) $store['ReceiptFooter']) as $line)
        @if (trim($line) !== '')<div class="c">{{ $line }}</div>@endif
    @endforeach
</div>
<div class="receipt-actions">
    <button class="btn" onclick="window.print()">Print</button>
    <a class="btn ghost" href="{{ route('admin.pos') }}">Back to POS</a>
</div>
</body>
</html>
