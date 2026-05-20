-- Datos de prueba para Campero Desktop
-- Ejecuta DESPUÉS de inventario_ventas.sql

USE inv_ventas;

-- Proveedores demo
INSERT INTO proveedores (nombre, nit, telefono, email, direccion) VALUES
  ('TechSeguridad SRL', '1012345678', '70012345', 'ventas@techseguridad.com', 'Zona Industrial Este'),
  ('ElectroVisión LTDA', '9876543210', '70098765', 'contacto@electrovision.com', 'Av. Tecnológica 567'),
  ('RedesModernas SAC', '1234567890', '70045678', 'info@redesmodernas.com', 'Sector Norte'),
  ('Iluminación LED Pro', '5556667778', '70011122', 'ventas@ledpro.com', 'Barrio Industrial');

-- Almacenes demo (ya en script original, pero asegurar)
INSERT IGNORE INTO almacenes (nombre, direccion, responsable) VALUES
  ('Almacén Central', 'Sede Principal P1', 'Juan Pérez'),
  ('Sucursal Norte', 'Av. Norte 123', 'Ana López'),
  ('Almacén Sur', 'Zona Sur Km 5', 'Carlos Ramírez');

-- Productos adicionales para stock bajo
INSERT IGNORE INTO productos (id_categoria, codigo, codigo_barras, nombre, precio_compra, precio_venta, stock_minimo) VALUES
  (1, 'CAM-003', '7701000000016', 'Camara PTZ 4MP', 750.00, 1125.00, 1),
  (2, 'TIM-002', '7701000000017', 'Timbre Audio HD', 180.00, 270.00, 1),
  (3, 'RED-002', '7701000000018', 'Router Wifi6', 420.00, 650.00, 1),
  (4, 'LUZ-002', '7701000000019', 'Tira LED 5M', 45.00, 75.00, 2);

-- Stock bajo demo
UPDATE inventario SET cantidad = 1 WHERE id_producto IN (SELECT id_producto FROM productos WHERE stock_minimo > 0 LIMIT 3);

-- Usuarios adicionales
INSERT IGNORE INTO usuarios (nombre, usuario, password_hash, rol) VALUES
  ('Vendedor 2', 'vendedor2', SHA2('v2pass', 256), 'vendedor'),
  ('Almacén 2', 'almacen2', SHA2('a2pass', 256), 'almacen');

-- Notas venta demo 2
-- (Generar con app o manual)

-- Órdenes compra demo
INSERT INTO ordenes_compra (id_proveedor, id_usuario, id_almacen, fecha, estado, total) VALUES
  (1, 1, 1, '2024-09-01', 'recibida', 2250.00),
  (2, 1, 2, '2024-09-05', 'enviada', 1420.00);

INSERT INTO detalle_orden_compra (id_orden, id_producto, cantidad, precio_unitario, subtotal) VALUES
  (1, 1, 5, 450.00, 2250.00),
  (2, 3, 3, 420.00, 1260.00),
  (2, 4, 2, 80.00, 160.00);

-- Movimientos stock from compras
INSERT INTO movimientos_stock (id_producto, id_almacen, id_usuario, tipo, cantidad, motivo, referencia_id) VALUES
  (1, 1, 1, 'entrada', 5, 'Orden compra #1', 1),
  (3, 2, 1, 'entrada', 3, 'Orden compra #2', 2),
  (4, 2, 1, 'entrada', 2, 'Orden compra #2', 2);

COMMIT;

