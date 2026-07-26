# Auditoría verificable de requisitos — Fase 4.4

Fecha de corte inicial: 2026-07-19 (America/Bogota)

Rama y worktree: `feat/professional-ui-autosave` / `wt-professional-ui-autosave`

HEAD inicial: `2398919`

PR: `#4` (draft)

## Aislamiento

La instalación `0.1.0-alpha.1` y su base real no se abrieron ni modificaron. La reproducción partió de una copia consistente de `.review-data-phase43-20260719`:

- copia inmutable de auditoría: `.phase44-audit-20260719/Data/peluqueria-admin.db`;
- SHA-256 de la copia inmutable: `1BC24DEF3CB1C6AFAE67D9595AEC99AC36771A5FC292031D1CB32EC0CDE85234`;
- copia mutable de reproducción: `.phase44-reproduction-20260719/Data/peluqueria-admin.db`;
- integridad inicial: `pragma integrity_check = ok`;
- última migración: `20260719212257_Phase43MaintenanceRecurrence`;
- no se requiere migración nueva: la cuenta del trabajador es una proyección calculada sobre cuotas, pagos y tarifas existentes.

## Reproducción y causa raíz

| Requisito | Evidencia inicial | Causa raíz | Corrección |
|---|---|---|---|
| Selectores de silla independientes | Alta y perfil mostraban el mismo estado de selección. | Ambos enlazaban `AvailableChairOptions`; el perfil vaciaba y repoblaba esa colección compartida. | Colecciones y selecciones separadas, preservadas por identificador. |
| Alta sin silla | El alta exigía una silla y fallaba cuando no había disponibilidad. | `SaveActionAsync` rechazaba `null` y llamaba siempre al alta con silla. | Silla inicial opcional; marcador `No hay sillas vacías` sin bloquear el guardado. |
| Pago anticipado | Con deuda cero o importe superior, el servicio rechazaba el pago. | `LocalUsePayment.Create`, el servicio y `CalculateDebt` imponían `pagado <= deuda`. | Todo importe positivo se registra; el exceso se expresa como crédito y se proyecta por ciclos semanales. |
| Perfil fijo e historial desplazable | La cabecera completa estaba dentro de un `ScrollViewer` de altura limitada. | La estructura visual envolvía datos, pagos, silla y banda de historial en el mismo desplazamiento. | Cabecera fija, banda propia de historial y desplazamiento únicamente en el `DataGrid` virtualizado. |
| Historial legible | La banda quedaba pegada a controles y los eventos antiguos aparecían primero. | Márgenes mínimos y orden ascendente. | Separación visual, periodo a la derecha y orden descendente estable. |
| Acción residual | Se veía `Limpiar formulario`. | Botón heredado de la fase anterior. | Se retiró solo de la interfaz; el borrador y la limpieza posterior a un alta válida continúan internos. |

## Invariantes protegidas

- Periodos completos de siete días anclados a la fecha de ingreso.
- Tarifas históricas inmutables y tarifa vigente solo para periodos futuros.
- Cuenta y crédito independientes por trabajador.
- Asignación, cambio y retiro de silla en una sola transacción.
- Retirar silla no retira al trabajador; retirar trabajador detiene cuotas futuras y conserva crédito.
- Sin cambios en Inicio, otros módulos, esquema SQLite, instalación alpha.1, tags o Releases.

## Evidencia final consolidada

### Compilación, formato y seguridad

- Compilación Debug: correcta, sin advertencias ni errores.
- Compilación Release: correcta, sin advertencias ni errores.
- `dotnet format --verify-no-changes --no-restore`: correcto.
- Paquetes NuGet directos y transitivos: sin vulnerabilidades conocidas reportadas.
- Modelo EF Core: sin cambios pendientes; no se añadió migración.
- XML de la vista WPF y `git diff --check`: correctos.

### Pruebas automatizadas

| Proyecto | Debug | Release |
|---|---:|---:|
| Domain.Tests | 53 | 53 |
| Application.Tests | 34 | 34 |
| Infrastructure.Tests | 15 | 15 |
| App.Tests | 27 | 27 |
| **Total** | **129** | **129** |

Total consolidado: **129 pruebas únicas y 258 ejecuciones satisfactorias** entre ambas configuraciones. Las pruebas de interfaz renderizan los tres paneles del perfil en espacios lógicos equivalentes a escalas de 100 %, 125 % y 150 %, incluida la altura reducida.

### Base temporal de revisión

- raíz aislada: `.phase44-review-final-20260720`;
- integridad final: `pragma integrity_check = ok`;
- última migración: `20260719212257_Phase43MaintenanceRecurrence`;
- trabajadores: 3; sillas: 2; sillas asignadas: 1;
- trabajador de pago: un pago, deuda de USD 12, crédito de USD 0, próxima cuota el 22 de julio de 2026 y sin silla asignada;
- historial del trabajador: 29 eventos, ordenado del más reciente al más antiguo y con desplazamiento visible.

### Revisión visual y operativa

La aplicación Release de revisión se ejecutó con prioridad baja y datos exclusivamente temporales. La ventana se situó en el segundo monitor y se verificó que no hubiera superposiciones, cortes ni controles vacíos en el perfil. Quedó abierta en **Registrar pago**, con cuenta visible e historial largo desplazable. La instalación alpha.1, su base real, otros módulos, tags y Releases no se modificaron.
