<nav class="section-tabs" aria-label="Report type">
@if(auth('staff')->user()->hasPermission('sales.view'))<a href="{{ route('admin.reports.sales') }}" @if(request()->routeIs('admin.reports.sales')) aria-current="page" @endif>Sales reports</a>@endif
@if(auth('staff')->user()->hasPermission('inventory.view') || auth('staff')->user()->hasPermission('inventory.edit') || auth('staff')->user()->hasPermission('stock.adjust'))<a href="{{ route('admin.reports.inventory') }}" @if(request()->routeIs('admin.reports.inventory')) aria-current="page" @endif>Inventory reports</a>@endif
</nav>
