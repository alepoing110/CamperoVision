# Campero Desktop - Estado del Proyecto

> **Fecha**: Mayo 2026
> **Framework**: .NET 8.0 WPF, MySQL (XAMPP), MVVM con DI
> **Build**: 0 errores, 5 warnings (solo NuGet network)

---

## Fases Completadas

### Phase 0: DB Fixes e Índices
- ✅ Role string `"almacen"` → `"almacenero"` (match DB ENUM)
- ✅ 7 índices de rendimiento creados (`migrations/001_add_performance_indexes.sql`)
- ✅ Connection pooling configurado en `appsettings.json` y `App.config`

### Phase 1: Helpers y Patrones de Colección
- ✅ `DecimalParser.cs` — parsing seguro de decimales
- ✅ `DataReaderExtensions.cs` — `GetNullableInt32()`, `GetStringSafe()`
- ✅ `ReplaceCollection<T>()` en `ViewModelBase`
- ✅ 25+ patrones `Clear()+foreach` reemplazados en 10 ViewModels

### Phase 2: Interfaces y Optimización de Repositorios
- ✅ Interfaces creadas: `ISalesNoteRepository`, `IProductRepository`, `IReportsRepository`
- ✅ Paginación `LIMIT/OFFSET` en Sales e Inventory repos
- ✅ 6 report queries consolidados en 1 multi-resultset `GetAllReportsAsync` (`ReportsBundle`)

### Phase 3: NavigationService y MainViewModel
- ✅ Dictionary-based routing (eliminado 15+ `BuildXxxAsync` methods)
- ✅ Dynamic command generation en `MainViewModel` desde `ModuleConfigs`
- ✅ `IsExecuting` property en `AsyncRelayCommand`

### Phase 4.1: DebounceHelper
- ✅ `DebounceHelper.cs` creado
- ✅ 300ms debounce aplicado a `SearchText` en `SalesNotesViewModel`

### Phase 4.2: Refactor SalesNotesViewModel
- ✅ `SaleItemManager` class extraída para lógica de kits/items
- ✅ Código reducido y optimizado

### Phase 4.3: Módulo de Pagos/Cancelaciones Completo
- ✅ `CajaRepository.RegistrarPagoAsync()` — implementación completa con validación de sesión
- ✅ `GetPagosByNotaAsync()`, `AbrirSesionAsync()`, `CerrarSesionAsync()`, `GetCajasByAlmacenAsync()`
- ✅ Payment registration wired en `RegisterSaleAsync()`
- ✅ `AnularNotaAsync()` con restauración de inventario (reverse stock movements)
- ✅ Botones "Editar nota" y "Anular nota" en Sales History UI
- ✅ `DangerButtonStyle` agregado en `App.xaml`
- ✅ `ShowConfirmation()` y `ShowInputDialog()` en `IDialogService` + `InputDialog` class
- ✅ `CajaRepository` y `SalesNotesViewModel` registrados en DI
- ✅ Modelos `PagoInfo`, `CajaInfo` creados

### Phase 4.4: Dependency Injection Fixes
- ✅ 8 ViewModels corregidos para usar interfaces en lugar de tipos concretos:
  - `KitsViewModel` → `IInventoryRepository`
  - `CategoriesViewModel` → `IProductRepository`
  - `SalesNotesViewModel` → `IInventoryRepository`
  - `SalesBetaViewModel` → `IInventoryRepository`
  - `QuotationViewModel` → `IInventoryRepository`
  - `ReportsViewModel` → `IInventoryRepository`
  - `PurchasesViewModel` → `IInventoryRepository`
  - `SaleNoteEditViewModel` → `ISalesNoteRepository`, `IInventoryRepository`

### Phase 4.5: Roles/Magic Strings Centralizados
- ✅ `Models/UserRoles.cs` — constantes `Admin`, `Almacenero`, `Vendedor`, `Cajero`
- ✅ Helpers: `IsAdmin()`, `IsAlmacenero()`, `IsVendedor()`, `IsCajero()`
- ✅ `UserRoles.All` y `UserRoles.BuilderOptions` arrays
- ✅ `MainViewModel.cs` — 14 arrays de roles usan `UserRoles.*`
- ✅ `SalesNotesViewModel.cs` — `CanEditHistorySale()`, `CanCancelHistorySale()` usan `UserRoles.IsAdmin()`
- ✅ `UsersViewModel.cs` — roles list usa `UserRoles.BuilderOptions`
- ✅ `UserUpsertModel.cs` — default rol usa `UserRoles.Vendedor`

### Phase 4.6: Refactor ViewModels Grandes
- ✅ `QuotationViewModel`: 808 → 626 líneas (-22%)
  - `QuotationItemManager.cs` extraída con lógica de kits/componentes
  - Patrón consistente con `SaleItemManager`

---

## Arquitectura Actual

### Capas
```
Views (XAML) → ViewModels (MVVM) → Repositories → MySQL
                    ↓
            Services (DI)
                    ↓
            Models + Helpers
```

### Dependency Injection (`App.xaml.cs`)
| Tipo | Lifetime |
|---|---|
| `IDialogService` → `MessageBoxDialogService` | Singleton |
| `ISessionService` → `SessionService` | Singleton |
| `INavigationService` → `NavigationService` | Singleton |
| `IWindowService` → `WindowService` | Singleton |
| `BusinessSettingsService` | Singleton |
| `IAuthenticationService` → `AuthenticationService` | Transient |
| `IInventoryRepository` → `InventoryRepository` | Transient |
| `IProductRepository` → `ProductRepository` | Transient |
| `IReportsRepository` → `ReportsRepository` | Transient |
| `ISalesNoteRepository` → `SalesNoteRepository` | Transient |
| Repositorios concretos (Auth, Dashboard, Category, etc.) | Transient |
| ViewModels | Transient |

### Navegación (15 módulos)
| Módulo | ViewModel | Roles |
|---|---|---|
| Dashboard | `DashboardViewModel` | Todos |
| Productos | `ProductsViewModel` | Admin, Almacenero |
| Kits | `KitsViewModel` | Admin, Almacenero |
| Categorias | `CategoriesViewModel` | Admin, Almacenero |
| Clientes | `ClientsViewModel` | Admin, Vendedor |
| Ventas | `SalesNotesViewModel` | Admin, Vendedor |
| Inventario | `InventoryViewModel` | Admin, Almacenero |
| Almacenes | `WarehousesViewModel` | Admin, Almacenero |
| Cotizaciones | `QuotationViewModel` | Admin, Vendedor |
| Contable Beta | `SalesBetaViewModel` | Admin |
| Negocio | `BusinessSettingsViewModel` | Admin |
| Usuarios | `UsersViewModel` | Admin |
| Reportes | `ReportsViewModel` | Todos |
| Proveedores | `ProvidersViewModel` | Todos |
| Compras | `PurchasesViewModel` | Admin, Almacenero |

---

## Archivos Nuevos Creados
| Archivo | Propósito |
|---|---|
| `Helpers/DecimalParser.cs` | Parsing seguro de decimales |
| `Helpers/DataReaderExtensions.cs` | Extensiones para MySqlDataReader |
| `Helpers/DebounceHelper.cs` | Debounce para búsqueda |
| `Models/UserRoles.cs` | Constantes de roles de usuario |
| `QuotationItemManager.cs` | Lógica de items/kits para cotizaciones |
| `migrations/001_add_performance_indexes.sql` | 7 índices de rendimiento |

## Archivos Modificados
| Archivo | Cambios |
|---|---|
| `ViewModels/ViewModelBase.cs` | `ReplaceCollection<T>()`, `OnPropertyChangedPublic()` |
| `ViewModels/MainViewModel.cs` | Dynamic commands, `UserRoles.*` |
| `ViewModels/SalesNotesViewModel.cs` | `SaleItemManager`, `CajaRepository`, `UserRoles`, cancel command |
| `ViewModels/QuotationViewModel.cs` | `QuotationItemManager`, `IInventoryRepository` |
| `ViewModels/KitsViewModel.cs` | `IInventoryRepository` |
| `ViewModels/CategoriesViewModel.cs` | `IProductRepository` |
| `ViewModels/SalesBetaViewModel.cs` | `IInventoryRepository` |
| `ViewModels/ReportsViewModel.cs` | `IInventoryRepository` |
| `ViewModels/PurchasesViewModel.cs` | `IInventoryRepository` |
| `ViewModels/SaleNoteEditViewModel.cs` | `ISalesNoteRepository`, `IInventoryRepository` |
| `ViewModels/UsersViewModel.cs` | `UserRoles.*` |
| `Data/CajaRepository.cs` | Pagos, sesiones, cajas completo |
| `Data/SalesNoteRepository.cs` | `AnularNotaAsync` con restauración inventario |
| `Services/IDialogService.cs` | `ShowConfirmation()`, `ShowInputDialog()` |
| `Services/MessageBoxDialogService.cs` | Implementaciones + `InputDialog` class |
| `Views/SalesRegisterView.xaml` | Botones Editar/Anular en historial |
| `App.xaml` | `DangerButtonStyle` |
| `App.xaml.cs` | DI registrations |
| `Models/UserUpsertModel.cs` | `UserRoles.Vendedor` default |

---

## Base de Datos

### Tablas Principales
- `notas_venta` — ventas con estado (`pendiente`, `completada`, `anulada`)
- `detalle_nota_venta` — items de cada venta
- `pagos` — registros de pago vinculados a notas y sesiones
- `sesiones_caja` — sesiones de caja abiertas/cerradas
- `cajas` — registros de caja por almacén
- `pagos_detalle_electronico` — detalles de pagos electrónicos
- `inventario` — stock por producto/almacén
- `movimientos_stock` — historial de movimientos
- `productos`, `kits`, `kit_detalle` — productos y kits
- `clientes`, `usuarios`, `almacenes` — entidades principales
- `cotizaciones`, `detalle_cotizacion` — cotizaciones
- `ordenes_compra`, `detalle_orden_compra` — órdenes de compra
- `proveedores` — proveedores
- `categorias` — categorías de productos

### Índices de Rendimiento
- `idx_notas_venta_fecha_estado` — `(fecha, estado)`
- `idx_detalle_nota_venta_nota` — `(id_nota)`
- `idx_inventario_producto_almacen` — `(id_producto, id_almacen)`
- `idx_movimientos_stock_referencia` — `(referencia_id, tipo)`
- `idx_pagos_nota` — `(id_nota)`
- `idx_cotizaciones_cliente` — `(id_cliente)`
- `idx_productos_codigo` — `(codigo)`

---

## Helpers Disponibles

### `DecimalParser.cs`
```csharp
DecimalParser.TryParse("12.5") → 12.5m
DecimalParser.TryParse("abc") → 0m (sin excepción)
```

### `DataReaderExtensions.cs`
```csharp
reader.GetNullableInt32("column") → int?
reader.GetStringSafe("column") → string (null-safe)
```

### `DebounceHelper.cs`
```csharp
var debounce = new DebounceHelper(300); // 300ms
debounce.Debounce(async () => await SearchAsync(query));
```

### `UserRoles.cs`
```csharp
UserRoles.Admin        → "admin"
UserRoles.Almacenero   → "almacenero"
UserRoles.Vendedor     → "vendedor"
UserRoles.Cajero       → "cajero"
UserRoles.IsAdmin(role) → bool
UserRoles.All           → string[]
UserRoles.BuilderOptions → string[]
```

---

## Próximos Pasos (Pendientes)

| Prioridad | Tarea | Descripción |
|---|---|---|
| 🔴 Alta | Logging estructurado | `Microsoft.Extensions.Logging` en repos y servicios |
| 🔴 Alta | Refactor `KitsViewModel` | Extraer manager (635 líneas) |
| 🟡 Media | Caching de datos repetitivos | Cache de warehouses, clients, products |
| 🟡 Media | Validación centralizada | Aplicar `DecimalParser` en todos los ViewModels |
| 🟡 Media | Paginación real en UI | Usar `LIMIT/OFFSET` existente en grids |
| 🟡 Media | Manejo centralizado de errores | Error handler global en lugar de try/catch por VM |
| 🟢 Baja | Tests unitarios | Tests para repos y ViewModels |
| 🟢 Baja | Nombres consistentes ES/EN | Unificar naming convention |
| 🟢 Baja | `nameof()` en OnPropertyChanged | Evitar bugs en rename |
| 🟢 Baja | Cancellation tokens | Cancelar operaciones async largas |

---

## Comandos Útiles

```bash
# Build
dotnet build CamperoDesktop.csproj --no-incremental

# Run
dotnet run --project CamperoDesktop.csproj

# Clean
dotnet clean CamperoDesktop.csproj
```

---

## Notas Importantes

1. **App en ejecución bloquea build** — Cerrar CamperoDesktop antes de compilar
2. **NuGet warnings** — `NU1900` por problemas de red, no afectan funcionalidad
3. **Fire-and-forget warning** — `CS4014` en `SalesNotesViewModel.cs:184` (debounce intencional)
4. **DB ENUM roles** — `('admin','vendedor','almacen')` — código usa `almacenero` como corrección
5. **`kardex` table** — existe en DB pero no se usa; `movimientos_stock` es el activo
