<?php

return [
    // Shown before prices on the storefront, back office and web receipts.
    'currency_symbol' => env('POS_CURRENCY_SYMBOL', '₱'),

    // Storefront hero text (store name and address come from the shared POS settings).
    'tagline' => env('POS_STORE_TAGLINE', 'Effortless pieces for everyday elegance.'),

    // Analytics dashboard. Amounts are stored in pesos; the dashboard can also show US dollars at this rate.
    'usd_rate' => (float) env('POS_USD_RATE', 58.0),

    // Cost estimates for the contribution-margin breakdown (the POS doesn't record these per order).
    'costs' => [
        'card_fee_percent' => (float) env('POS_CARD_FEE_PERCENT', 3.5),     // card / e-wallet gateway fee
        'other_fee_percent' => (float) env('POS_OTHER_FEE_PERCENT', 2.5),   // "Other" payment methods
        'packaging_per_order' => (float) env('POS_PACKAGING_PER_ORDER', 25),
        'shipping_per_order' => (float) env('POS_SHIPPING_PER_ORDER', 0),   // 0 for walk-in sales
    ],
];
