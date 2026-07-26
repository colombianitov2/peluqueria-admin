# Auditoría verificable de requisitos — Fase 4.2

Fecha de corte inicial: 2026-07-19 (America/Bogota)
Rama: `feat/professional-ui-autosave`
HEAD auditado: `18a92b2d0c3f29384abf4264f2e61ed8eb115581`
PR: `#4` (draft)

## Aislamiento y evidencia de datos

La base de revisión original no se abrió en modo escritura ni se usó para pruebas mutables.
Se creó una copia consistente mediante la API de copia de seguridad de SQLite, abriendo el origen
con `mode=ro`:

- origen protegido: `.review-data-phase41-20260719/Data/peluqueria-admin.db`;
- SHA-256 inicial del archivo principal de origen: `54DC104689F9CB34E87FAA017092D030B5CADD0366903677DEB8661951D999C9`;
- copia de auditoría: `.audit-data-phase42-20260719/Data/peluqueria-admin-audit.db`;
- SHA-256 de la copia consistente: `C34DF60F2AC67329E0C7CF587CB2E604E35E2FBC7EC0BC1F6167B2A20A0EF86A`;
- copia mutable para reproducción visual: `.audit-runtime-phase42/Data/peluqueria-admin.db`.

La copia contiene las migraciones hasta `20260719062346_Phase41BusinessModel`, un producto,
cero movimientos de inventario, cero asientos financieros y cuatro borradores internos.

## Reproducción de “Queso rancio”

Resultado: **defecto reproducido**.

1. La copia contiene `Un queso rancio` con `Category = 3`, `DefaultSalePriceMinorUnits = NULL` y
   ningún movimiento inicial. En el enum actual, el valor 3 corresponde a `CustomerCourtesy`
   (`Cortesía para clientes`), por lo que `Product.IsForSale` devuelve `false`.
2. Al abrir la aplicación Release con la copia mutable y entrar en **Ventas**, el selector
   `Producto destinado a venta` aparece vacío y el producto no figura.
3. En `AdministrationViewModel.ConfigureFieldPresentation`, la acción **Agregar producto** carga
   correctamente las seis cadenas en `SecondaryOptions`, pero también establece
   `UseSecondarySelector = true`.
4. En `AdministrationView.xaml`, ese indicador oculta el ComboBox enlazado a `SecondaryOptions`
   y muestra el enlazado a `SecondaryEntityOptions`, colección vacía para esta acción. El alta
   termina interpretando un `SecondaryText` residual en vez de la categoría elegida.

Causa raíz: **enlace equivocado entre `SecondaryOptions`, `SecondaryEntityOptions` y
`UseSecondarySelector`**. La exclusión posterior en Ventas es coherente con la categoría errónea;
el defecto ocurre al capturar/editar el producto en Inventario.

## Matriz inicial de cumplimiento

| Área / requisito | Estado inicial | Evidencia verificable |
|---|---|---|
| Worktree, rama y HEAD exigidos | implementado | `git status -sb`, `git log`; worktree limpio en `18a92b2` antes de empezar. |
| Copia de la base sin modificar el origen | implementado | copia SQLite `mode=ro`, rutas y hashes de la sección anterior. |
| Auditoría previa a la implementación | implementado | este documento y reproducción visual sobre `.audit-runtime-phase42`. |
| Sustituir el término visible heredado por “trabajador” | defectuoso | la búsqueda inicial encontró el término heredado en interfaz, dominio, mensajes, Excel y documentación. |
| Diferenciar trabajadores de colaboradores inversionistas | parcial | el dominio ya usa `LocalUsePerson` y `Collaborator`, pero la UI inicial no ofrecía perfiles separados completos. |
| Tarifa semanal actual tomada de Ajustes | implementado | `EnsureRatesAsync`, `SaveSettingsAndRateAsync` y tarifas históricas `WeeklyRates`. |
| No cobrar al ingreso; cobrar solo cada siete días completos | implementado | `WeeklyChargeCalculator.ExpectedPeriodStarts` exige `entryDate + 7 <= throughDate`; pruebas existentes cubren ingreso y primer periodo. |
| Límites día 6, 7, 14, retiro temprano y reapertura | parcial | hay pruebas de la regla base e idempotencia, pero no existe una única regresión explícita con todos los límites solicitados. |
| Sábado habitual y pagos tardíos en cualquier fecha | parcial | `WeeklyCharge.DueDate` calcula sábado y `LocalUsePayment` admite fecha libre; falta presentación específica en el perfil. |
| Selector Acción de Uso del local con solo dos opciones | defectuoso | `ConfigureModule` expone cinco acciones: alta de silla/persona, pago, asignación y retiro. |
| Tabla separada de trabajadores | pendiente | Uso del local emplea la tabla administrativa genérica `Rows`. |
| Perfil de trabajador por doble clic y retorno a lista | pendiente | no hay manejador `MouseDoubleClick`, estado de perfil ni vista específica. |
| Historial cronológico, filtrado, desplazable y virtualizado del trabajador | pendiente | solo existe actividad general; no hay agregación de cuotas, pagos, sillas y cambios por trabajador. |
| Acciones del perfil: pago, silla, retiro de silla y retiro del local | pendiente | existen operaciones de servicio, pero solo se exponen como acciones genéricas de módulo. |
| Tabla independiente de sillas y CRUD seguro | parcial | entidad, CRUD y asignación exclusiva existen; la visualización está mezclada en `Rows`. |
| Bloqueo de alta sin sillas disponibles con texto exacto | defectuoso | existe bloqueo, pero el mensaje actual dice “No hay sillas suficientes...” y no coincide con el requisito. |
| Una silla por trabajador vigente y un trabajador por silla | implementado | validaciones de `AssignChairAsync`, índice único de EF y pruebas de Fase 4.1. |
| Perfil de colaborador por doble clic | pendiente | solo hay selección de fila e historial financiero parcial debajo de la tabla. |
| Entidad y CRUD de aportes de capital | pendiente | no existe `CollaboratorContribution` ni tabla/migración. |
| Excluir aportes de ventas, ingresos operativos, ganancia y punto de equilibrio | pendiente | no hay aportes modelados. |
| Aportes en copias, restauración, CSV y Excel | pendiente | no existe fuente de datos que exportar. |
| Seis categorías exactas de Inventario | defectuoso | `SpanishText` contiene las seis, pero el ComboBox correcto queda oculto por el enlace erróneo. |
| No mostrar el atributo técnico heredado de unidades | parcial | no aparecía en la vista ni Excel, pero CSV todavía lo exportaba y faltaba documentar compatibilidad técnica. |
| Producto vendible aparece inmediatamente en Ventas | defectuoso | reproducción de Queso rancio; selector de categorías incorrecto y actualización inmediata sin regresión integral. |
| Búsqueda de producto sin distinguir mayúsculas | parcial | ComboBox usa búsqueda WPF y orden alfabético, pero no hay filtro explícito verificable sin distinción de mayúsculas. |
| Mostrar existencia y precio predeterminado | parcial | muestra existencia en `SelectedProductAvailability` y coloca el precio en el campo; falta una presentación conjunta clara y prueba integral. |
| Cantidad vendida escrita por el usuario | implementado | al seleccionar producto se limpia `QuantityText`. |
| Descontar inventario una sola vez | implementado | una venta crea un único `InventoryMovement.Sale` y la transacción valida existencia no negativa. |
| Excluir categorías no vendibles de Ventas | implementado | `PopulateSelectors` filtra por `Product.IsForSale`. |
| Explicar por qué un producto no aparece | pendiente | no hay mensaje visible de clasificación en Ventas. |
| Regresión completa “Queso rancio” con reinicio | pendiente | el escenario solicitado no existe en la suite inicial. |
| Actividad diaria por defecto y filtros existentes | implementado | `ActivityPeriod.Today` es el valor predeterminado; semana, mes, 3/6 meses, año y rango personalizado existen. |
| Separar estado actual de actividad | implementado | la vista tiene `Actividad del periodo` y `Registros y estado actual`; falta aplicar la misma separación a perfiles nuevos. |
| Autoguardado con debounce para ajustes/ediciones | implementado | `SettingsViewModel` y `AdministrationViewModel` conservan debounce y edición automática. |
| Confirmación explícita para dinero, inventario, aportes, cierres y eliminaciones | parcial | ventas/compras/pagos/cierres requieren botón y eliminaciones requieren casilla; aportes aún no existen. |
| Borradores invisibles y recuperación sin textos técnicos | implementado | persistencia de `FormDrafts`, finalización transaccional y único botón `Limpiar formulario`. |
| WAL, FULL, claves foráneas y transacciones | implementado | configuración SQLite y pruebas de infraestructura existentes. |
| Logotipo e iconos exactos | pendiente | el archivo gráfico K&V autorizado no está accesible; no se usa sustituto. |
| Inicio, extraoficiales, precio sugerido, moneda, Excel, gráficos, copias y actualizaciones | implementado | suite consolidada de Fase 4.1 y revisión visual previa; se conservarán como regresión protegida. |
| Flujo de caja eliminado de UI y Excel | implementado | prueba `Phase41UiContractTests` y ausencia de hoja en `ExcelExportTests`. |
| Manual como módulo | implementado | no existe módulo Manual; la documentación debe registrar que se hará tras la aprobación definitiva. |
| Validación completa de Fase 4.2 | pendiente | se ejecutará después de implementar, incluida migración, empaquetado y arranque aislado. |
| Commits, push y actualización del PR #4 | pendiente | se realizará después de superar la validación final. |

## Archivos y zonas relacionadas

- UI y presentación: `AdministrationView.xaml`, `AdministrationViewModel.cs` y modelos de fila/opciones.
- Dominio: `LocalUse`, `Collaborators`, `Inventory` y actividad auditable.
- Aplicación: `AdministrationData`, `AdministrationService`, reportes y localización.
- Infraestructura: contexto/configuraciones EF, migración aditiva, repositorio, CSV y Excel.
- Pruebas: los cuatro proyectos `Domain`, `Application`, `Infrastructure` y `App`.

No se modificarán la instalación alpha.1, su base real, `main`, el tag `v0.1.0-alpha.1`,
releases existentes ni el logotipo mientras el original exacto no esté disponible.

## Matriz final de cumplimiento

Fecha de validación: 2026-07-19 (America/Bogota).

| Área / requisito | Estado final | Evidencia verificable |
|---|---|---|
| Terminología visible “trabajador” | implementado y probado | interfaz, mensajes, Excel y documentación actualizados; no hay coincidencias en `src`, `docs` ni `README.md` del término heredado. |
| Separación trabajador / colaborador | implementado y probado | vistas, ViewModels, perfiles, actividad e historiales independientes; los colaboradores nunca ocupan sillas. |
| Semana exacta y tarifa vigente | implementado y probado | pruebas de dominio para días 0, 6, 7 y 14, retiro temprano, reapertura e idempotencia; tarifas históricas visibles en el perfil. |
| Uso del local dedicado | implementado y probado | selector `Acción` con solo `Añadir silla` y `Añadir trabajador`; tarjetas, actividad, tablas separadas y formulario dedicado. |
| Perfil del trabajador | implementado y probado | doble clic y botón accesible, datos autoguardados, deuda, tarifas, pago explícito, silla, retiro e historial virtualizado/filtrado. |
| Persistencia de selección al recargar perfiles | implementado y probado | identificador estable de perfil y bindings bidireccionales; comprobación interactiva real de apertura y recarga. |
| CRUD seguro de sillas | implementado y probado | alta, edición autoguardada, asignación exclusiva, retiro y eliminación confirmada con protecciones históricas. |
| Mensaje exacto cuando no hay sillas | implementado y probado | servicio y prueba exigen `No hay sillas disponibles. Debes crear un espacio para una silla adicional.`. |
| Perfil del colaborador | implementado y probado | doble clic y botón accesible, datos autoguardados, aportes, cierres, distribuciones e historial virtualizado/filtrado. |
| Aportes de capital | implementado y probado | entidad, migración, alta/edición/eliminación lógica, actividad, CSV, Excel, copia/restauración y validación visual. |
| Aportes fuera del resultado operativo | implementado y probado | pruebas confirman que no alteran ventas, otros ingresos, ganancia, caja operativa, cierre ni punto de equilibrio; se muestran como `Capital / inversión` y `No operativo`. |
| Seis categorías exactas de Inventario | implementado y probado | ComboBox correcto visible; revisión interactiva confirmó las seis opciones requeridas. |
| Atributo técnico de unidades oculto | implementado y probado | eliminado de UI, CSV y documentación funcional; la columna interna se conserva únicamente por compatibilidad de datos. |
| Alta vendible disponible en Ventas | implementado y probado | corrección del selector, filtro inmediato y búsqueda sin distinguir mayúsculas/minúsculas. |
| Existencia, precio y cantidad manual | implementado y probado | detalle visible al seleccionar, precio predeterminado y cantidad siempre capturada por el usuario. |
| Regresión “Un queso rancio” | implementado y probado | prueba de infraestructura crea producto vendible, stock 3, costo 2, precio 4,50, vende 1, registra caja 4,50, deja stock 2 y conserva todo al reabrir. |
| Actividad y filtros | implementado y probado | actividad de hoy por defecto y periodos existentes en módulos y perfiles; estado actual permanece separado. |
| Autoguardado y borradores | implementado y probado | debounce para datos editables, borradores recuperables para operaciones nuevas y confirmación explícita para dinero, inventario, aportes, cierres y eliminaciones. |
| Excel completo | implementado y probado | incluye `Historial trabajadores`, `Aportes colaboradores` e `Historial colaboradores`, con fechas y dinero tipados y aportes no operativos. |
| CSV completo | implementado y probado | seis archivos, incluido aportes de colaboradores; no expone el atributo técnico de unidades. |
| Migración desde copia alpha.1/Fase 4.1 | implementado y probado | migración aditiva `20260719074910_Phase42WorkersAndContributions`, datos previos conservados e `integrity_check=ok`; modelo sin cambios pendientes. |
| Escalas 100 %, 125 % y 150 % | implementado y probado | pruebas de renderizado cubren vistas administrativas, Ajustes, Uso del local y ambos perfiles sin cortes ni superposiciones. |
| Builds y pruebas | implementado y probado | Debug y Release: 0 advertencias, 0 errores; 93/93 pruebas únicas aprobadas en cada configuración. |
| Formato, seguridad y repositorio | implementado y probado | `dotnet format --verify-no-changes`, `git diff --check`, cero paquetes vulnerables, cero secretos detectados y cero binarios rastreados. |
| Empaquetado controlado | implementado con limitaciones declaradas | Velopack 1.2.0 verificó el bootstrap y generó paquete completo, portable e instalador aislados; no se publicaron ni instalaron. Artefactos sin firma, igual que la validación alfa previa. |
| Inicio y Fase 4.1 protegida | implementado y probado | suite completa de regresión, revisión visual de navegación, migración de copia y arranque Release aislado. |
| Módulo Manual | diferido por requisito | no se añadió módulo; la documentación se mantiene como manual externo hasta aprobación definitiva. |
| Logotipo e iconos exactos | bloqueado por recurso faltante | el archivo gráfico K&V autorizado no fue adjuntado ni existe en el worktree; no se generó ni usó sustituto. |

## Resultado de validación

- `dotnet format PeluqueriaAdmin.sln --verify-no-changes --no-restore`: correcto.
- `dotnet build` Debug y Release: correcto, 0 advertencias y 0 errores.
- `dotnet test` Debug y Release: 93/93 pruebas únicas correctas en cada matriz (186 ejecuciones correctas).
- `dotnet list package --vulnerable --include-transitive`: cero vulnerabilidades conocidas en los orígenes consultados.
- `dotnet ef migrations has-pending-model-changes`: sin cambios pendientes.
- migración sobre copia consistente de alpha.1/Fase 4.1: correcta; integridad SQLite `ok`.
- Velopack 1.2.0: paquete aislado generado correctamente, sin publicar, instalar ni modificar releases existentes.
