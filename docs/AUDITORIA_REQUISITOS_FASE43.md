# Auditoría verificable de requisitos — Fase 4.3

Fecha de corte inicial: 2026-07-19 (America/Bogota)
Rama: `feat/professional-ui-autosave`
HEAD auditado: `89eaec7588eb41fd13c99df3a91beaf78c33baa2`
PR: `#4` (draft)

## Aislamiento y evidencia de datos

La reproducción no modificó la base temporal de Fase 4.2. Se abrió el origen con SQLite
en `mode=ro` y se creó una copia consistente mediante la API de copia de seguridad:

- origen protegido: `.visual-data-phase42-20260719/Data/peluqueria-admin.db`;
- SHA-256 del archivo principal del origen: `2C88E0CDD414C5D35B5FF81FABC666E6BE2C47267C0B7AD5A81ACE7F381BFE25`;
- copia de auditoría: `.audit-data-phase43-20260719/Data/peluqueria-admin-audit.db`;
- SHA-256 de la copia consistente: `079899795014110385A2B82C1CEC52784700232C979D15EFFF3E13E66DF591D3`;
- copia mutable de reproducción: `.audit-runtime-phase43-20260719/Data/peluqueria-admin.db`;
- integridad inicial de la copia: `pragma integrity_check = ok`;
- última migración inicial: `20260719074910_Phase42WorkersAndContributions`.

La copia contiene un trabajador, una silla, un colaborador, un aporte, dos productos vendibles,
tres movimientos de inventario y diecinueve registros técnicos de actividad. Los productos
`queso rancio` y `pepino salado` están activos y tienen precio predeterminado.

## Reproducción, causa inicial y solución prevista

| Defecto o requisito | Reproducción y causa inicial | Solución prevista | Evidencia final |
|---|---|---|---|
| Retiro con fecha de hoy continúa “Vigente” | `Trabajador revisión` tiene `ExitDate=2026-07-19`, pero la tabla muestra `Vigente`. `LocalUsePerson.IsCurrentOn` usa `ExitDate >= date`; `Collaborator.IsCurrentOn` repite la semántica inclusiva. | Tratar `ExitDate` como primer día fuera: vigente solo si `date < ExitDate`; preservar el cálculo de periodos completos y cubrir límites con pruebas. | La copia migrada muestra `Trabajador revisión` como `Retirado`; las pruebas de dominio cubren el límite de fecha y la regla de siete días continúa verde. |
| Eliminar trabajador lógicamente con historia | El perfil solo permite retirar; `AdministrationService.DeleteAsync` bloquea un trabajador con cuotas/pagos. | Operación transaccional específica que desasigne la silla, marque eliminado, registre evento y preserve dependencias/historial/exportaciones. | `DeleteLocalUsePersonAsync` se ejecuta en transacción; pruebas verifican liberación de silla, conservación de cuotas/pagos y exclusión, y Excel conserva el nombre histórico. |
| Eliminar colaborador con historia | El servicio bloquea la eliminación cuando existen aportes o participaciones. | Operación específica de eliminación lógica, sin borrar aportes, cierres ni distribuciones y excluyéndolo de cierres futuros. | La prueba de aplicación elimina un colaborador con dependencias, conserva aportes/distribuciones y lo excluye de cierres posteriores; el perfil muestra confirmación y acción explícitas. |
| Silla sin trabajador dice “Disponible” | En la tabla, `Worker` y `State` muestran `Disponible`; no existe perfil de silla. | Sustituir por `Vacía`, crear perfil con historial útil y conservar `Ocupada` cuando corresponda. | Revisión visual: tabla y perfil muestran exactamente `Vacía`; el perfil abre por botón/doble clic, permite autoguardado y presenta historial cronológico. |
| Eliminar silla ocupada | El borrado genérico no ofrece confirmación de desasignación ni una transacción de dominio explícita. | Desasignar y marcar eliminada en una única transacción, conservando historia y dejando al trabajador `Sin silla`. | La teoría `LogicalChairDeletion_WorksForEmptyAndOccupiedChair` cubre ambos casos y verifica eliminación lógica, desasignación y conservación; la advertencia visible se comprobó en el perfil. |
| Ventas aparece sin productos | Con búsqueda vacía el ComboBox muestra ambos productos. Al escribir `sincoincidencia` en el TextBox separado, salir y volver, el filtro residual permanece y el ComboBox queda vacío. `ProductSearchText` vive en el ViewModel compartido y no se limpia al entrar. | Un único ComboBox buscable con filtro integrado, normalización de mayúsculas y tildes, desplegable estable, mensaje sin coincidencias y limpieza al entrar. | Revisión interactiva con `queso rancio` y `pepino salado`: vacío muestra ambos, `queso`/`PÉPINO` filtran sin distinguir tildes o caso, seleccionar carga precio/existencia y volver a entrar limpia el texto. Hay pruebas explícitas del filtro y del stock/precio de venta. |
| Etiqueta de precio recortada | `Precio de venta por unidad o paquete` se corta visualmente porque la columna de importe usa `Width=175` y el `TextBlock` no ajusta línea. | Dar ancho suficiente y `TextWrapping`, con renderizado a 100 %, 125 %, 150 % y resolución pequeña. | La etiqueta completa se verificó visualmente y `ResponsiveLayoutTests` renderiza todas las vistas especializadas en equivalentes de 100 %, 125 % y 150 %. |
| Filas aparentes duplicadas en Inventario | `PopulateRows` agrega una fila por `Product` y otra por cada `InventoryMovement` a la misma colección `Rows`; la copia muestra dos productos y tres movimientos mezclados. | Vista y ViewModel dedicados: una tabla de estado por producto, otra de movimientos filtrados y otra de planes si aplica. | La revisión muestra exactamente dos filas actuales para dos productos; movimientos y planes están en pestañas independientes. El historial separa entradas, salidas, costo unitario y valor total y aplica el periodo elegido. |
| Bitácora técnica visible | Uso del local, Colaboradores y módulos genéricos muestran `Actividad del periodo`, columna `Actividad` y acciones `Alta`; duplican los registros administrativos. | Conservar `ActivityRecords` solo internamente y en exportación; retirar esos bloques de UI y usar `Periodo a mostrar` sobre registros reales. | Revisión visual de los módulos y contratos XAML: no quedan bloques/columnas `Actividad` ni estados técnicos `Alta`; `ActivityRecords` sigue en persistencia y exportación. |
| Tablas simples contienen columnas genéricas | Gastos muestra además una bitácora técnica y la tabla genérica incluye `Estado`; Ventas usa `Operación/Estado` indirectamente. | Filas/columnas específicas por módulo: fecha, nombre/producto, valores reales y descripción. | Otros ingresos, Gastos e Imprevistos muestran Fecha/Nombre/Valor/Descripción; Ventas muestra Fecha/Producto/Cantidad/Precio unitario/Total/Descripción. |
| Mantenimiento no almacena recurrencia | El formulario solo contiene equipo, tipo, fecha, costo y descripción; `MaintenanceRecord` no tiene frecuencia ni vínculo entre ocurrencias. | Migración aditiva, regla de próxima fecha sin deriva, programación/ocurrencias separadas, idempotencia y eliminación lógica de futuras. | La migración `20260719212257_Phase43MaintenanceRecurrence` añade frecuencia, ancla, serie e intervalo. Pruebas cubren todas las frecuencias, personalización válida, 31/01→28/02→31/03, idempotencia, costo único, detención y migración heredada como `Una sola vez`. |
| Gráficos en notación científica | Resumen mensual con valores grandes muestra `2e+10`, `8e+10` y `6e+10`; los `LinearAxis` no definen `LabelFormatter`. | Formateador cultural español explícito, números completos, menos marcas y formato explícito de tooltip. | Pruebas exigen `70.000.000` y `80.000.000.000`; los tres gráficos se revisaron sin notación científica y con etiquetas completas sin superposición. |
| Logotipo exacto | El archivo K&V exacto no está disponible en el worktree ni fue adjuntado. | Mantener bloqueado y no usar sustituto. | bloqueado por recurso faltante |

## Regresiones protegidas

Se conservarán autoguardado y borradores, cobro después de siete días completos, perfiles,
aportes no operativos, Inventario → Ventas, gastos extraoficiales, precio sugerido, Inicio,
Excel, USD/COP, copias/restauración y actualización por GitHub.

No se modificaron la instalación alpha.1, su base real, `main`, tags ni Releases. Toda la
implementación permaneció en la rama y el worktree autorizados.

## Validación final

- `dotnet format PeluqueriaAdmin.sln --verify-no-changes --no-restore`: correcto. La primera ejecución detectó CRLF/BOM en la migración generada; se normalizó únicamente ese archivo con `dotnet format` y la verificación posterior quedó limpia.
- Compilación Debug y Release: correctas, 0 advertencias y 0 errores.
- Pruebas: 116 únicas (46 Domain, 33 Application, 14 Infrastructure y 23 App), ejecutadas completas tanto en Debug como en Release: 232/232 ejecuciones correctas.
- NuGet directo y transitivo: ningún paquete vulnerable conocido en los orígenes consultados.
- EF Core: `has-pending-model-changes` informa que no hay cambios pendientes.
- Copia migrada de Fase 4.2: última migración `20260719212257_Phase43MaintenanceRecurrence`; columnas de recurrencia presentes; `pragma integrity_check = ok`.
- Excel: las pruebas conservan datos históricos/eliminados y los campos nuevos de mantenimiento sin alterar los contratos anteriores.
- `git diff --check`, escaneo de secretos y binarios: limpios antes del commit.
- Velopack 1.2.0: paquete autocontenido e instalador generados de forma aislada en `artifacts/phase43-validation-final2-20260719`; no se instalaron, firmaron ni publicaron.
- Revisión visual: Inicio, Uso del local/perfil de silla, Colaboradores, Ventas, Inventario, Otros ingresos, Gastos, Imprevistos, Obligaciones, Mantenimiento, Nómina, Resumen mensual, Balance anual y Ajustes; render responsivo automatizado en 100 %, 125 % y 150 %.

## Límite conocido

El logotipo K&V exacto sigue bloqueado porque el recurso no fue entregado. No se añadió un
sustituto. La compilación local de validación permanece sin firma, igual que la alfa anterior;
no se publicó ni instaló.
