-- ============================================================
-- Migración de índices para optimización de rendimiento
-- Fase 0.2 - Campero Desktop
-- ============================================================
-- Estos índices optimizan las consultas más frecuentes:
--   - Reportes de ventas (filtran por fecha + estado)
--   - Reportes de inventario (filtran por fecha)
--   - Búsqueda de productos (LIKE en nombre y codigo_barras)
--   - Cotizaciones por estado
-- ============================================================

USE importadora_campero;

-- 1. Reportes de ventas: 6 queries filtran WHERE estado='completada' AND fecha >= X
-- Índice compuesto para cubrir el patrón más común
CREATE INDEX IF NOT EXISTS idx_nv_estado_fecha ON notas_venta(estado, fecha);

-- 2. Reportes de inventario: filtra WHERE fecha >= X AND fecha < Y
CREATE INDEX IF NOT EXISTS idx_ms_fecha ON movimientos_stock(fecha);

-- 3. Búsqueda de productos: LIKE '%search%' en nombre
-- (No acelera LIKE con % adelante, pero ayuda para ORDER BY y cobertura)
CREATE INDEX IF NOT EXISTS idx_prod_nombre ON productos(nombre);

-- 4. Búsqueda por código de barras
CREATE INDEX IF NOT EXISTS idx_prod_codigo_barras ON productos(codigo_barras);

-- 5. Cotizaciones por estado (filtro WHERE estado = 'generada'/'convertida')
CREATE INDEX IF NOT EXISTS idx_cot_estado ON cotizaciones(estado);

-- 6. Detalle nota venta por nota (ya tiene FK, pero verificar índice compuesto)
-- Útil para queries que buscan detalles + producto
CREATE INDEX IF NOT EXISTS idx_dnv_nota_producto ON detalle_nota_venta(id_nota, id_producto);

-- 7. Pagos por nota (para validación de anulación de notas)
CREATE INDEX IF NOT EXISTS idx_pagos_nota ON pagos(id_nota);

-- Verificar índices creados
SELECT TABLE_NAME, INDEX_NAME, COLUMN_NAME, SEQ_IN_INDEX
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = 'importadora_campero'
  AND INDEX_NAME LIKE 'idx_%'
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;
