<?php

return [
    // Shown before prices on the storefront, back office and web receipts.
    'currency_symbol' => env('POS_CURRENCY_SYMBOL', '₱'),

    // Storefront hero text (store name and address come from the shared POS settings).
    'tagline' => env('POS_STORE_TAGLINE', 'Effortless pieces for everyday elegance.'),
];
