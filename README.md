# Campero Desktop - WPF MySQL Inventory Sales System

**Framework:** .NET 8.0 WPF | **Patrón:** MVVM + DI | **DB:** MySQL (MySqlConnector)

## Funcionalidades

### Módulos principales
- **Login** con autenticación por roles (Admin, Almacenero, Vendedor)
- **Dashboard** con resumen de ventas, productos, stock bajo
- **Productos** - CRUD con codigo de barras, precios, stock minimo
- **Kits** - Productos compuestos con componentes y stock posible
- **Categorias** - CRUD con validacion de productos asociados
- **Clientes** - CRUD con CI/NIT, email, telefono
- **Ventas** - Notas de venta transaccionales con edicion y anulacion
- **Inventario** - Movimientos de stock, kardex, control por almacen
- **Almacenes** - CRUD con desactivacion logica
- **Cotizaciones** - Propuestas comerciales con PDF imprimible
- **Contable Beta** - Analisis de ganancias por producto y periodo
- **Negocio** - Configuracion institucional para reportes y recibos
- **Usuarios** - CRUD con roles y contraseñas
- **Reportes** - 6 tipos con exportacion PDF y Excel
- **Proveedores** - CRUD completo
- **Compras** - Ordenes de compra por almacen

### Nuevos modulos
- **Backup y Restauracion** - Copias de seguridad de DB con mysqldump (solo Admin)
- **Auditoria de Cambios** - Historial de todas las acciones con datos antes/despues (solo Admin)

## Mejoras de rendimiento implementadas

- **N+1 queries** eliminadas con validacion de stock por lotes
- **Multi-resultset** en reportes (6 queries → 1 conexion)
- **Multi-row INSERTs** en Kits y Ordenes de Compra
- **Paginacion** en Productos y Ventas
- **Caching** de datos frecuentes (5 min)
- **SemaphoreSlim** para inicializacion thread-safe de esquemas DB
- **IsolationLevel.Serializable** para evitar race conditions en inventario

## Mejoras UI/UX

- Estilos unificados para ComboBox, CheckBox, DatePicker
- Loading indicators (ProgressBar) en 7 vistas
- Keyboard shortcuts (Ctrl+S, N, F, R, Enter, Esc) en 12 vistas
- Colores unificados con StaticResource en 20 vistas
- DangerButtonStyle para botones destructivos
- Sidebar con separadores visuales entre grupos de modulos

## Arquitectura

- **MVVM** con ViewModelBase y ValidatableViewModelBase
- **DI** con Microsoft.Extensions.DependencyInjection
- **Logging** estructurado con Serilog (archivo rotativo + debug)
- **AsyncHelper** para fire-and-forget seguro en UI
- **SilentObservableCollection** para reemplazo de colecciones sin parpadeo

## Ejecución

1. Abrir `CamperoDesktop.csproj` en Visual Studio o Rider
2. Ejecutar `inventario_ventas.sql` en MySQL
3. Configurar connection string en `appsettings.json`
4. `dotnet run` o F5

## Usuarios test

| Usuario | Password | Rol |
|---|---|---|
| admin | admin123 | Admin |
| vendedor | vendedor123 | Vendedor |
| almacen | almacen123 | Almacenero |

## Stack tecnico

- .NET 8.0 WPF
- MySqlConnector (async ADO.NET)
- Serilog (logging)
- QuestPDF (reportes PDF)
- EPPlus (exportacion Excel)
- Microsoft.Extensions.DependencyInjection
