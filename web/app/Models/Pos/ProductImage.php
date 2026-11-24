<?php

namespace App\Models\Pos;

use Illuminate\Database\Eloquent\Model;

/** Photo for one POS item (one size/colour). See App\Services\Pos\ProductImages. */
class ProductImage extends Model
{
    protected $table = 'product_images';

    protected $primaryKey = 'product_id';

    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = ['product_id', 'path'];

    public function isUpload(): bool
    {
        return ! preg_match('#^https?://#i', $this->path);
    }

    public function url(): string
    {
        return $this->isUpload() ? route('media.product', basename($this->path)) : $this->path;
    }
}
