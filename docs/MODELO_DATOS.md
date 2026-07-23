# Modelo de datos

## Convenciones

- Identificadores `Guid` generados por la aplicación.
- Importes como unidades menores enteras (`long`) mediante `Money`; nunca `float` ni `double`.
- Cantidades de inventario como `decimal` con hasta tres decimales.
- Fechas del negocio como `DateOnly`; auditoría técnica en UTC.
- Entidades operativas con `CreatedUtc`, `UpdatedUtc` y `DeletedUtc`. Los filtros globales excluyen eliminados lógicamente.
- Claves foráneas, índices únicos y transacciones protegen operaciones relacionadas.

## Tablas principales

| Área | Tablas | Responsabilidad |
|---|---|---|
| Ajustes | `Settings`, `UnofficialExpenses` | Tarifa semanal, porcentaje, reserva, moneda y gastos extraoficiales separados. `TotalChairs` se conserva solo para migrar bases antiguas. |
| Uso del local | `Chairs`, `LocalUsePeople`, `WeeklyRates`, `WeeklyCharges`, `LocalUsePayments` | Sillas individuales y opcionales, trabajadores, histórico de tarifas, cuotas de periodos completos y pagos positivos incluidos anticipos. `WorkerAccountBalance` calcula deuda, crédito y proyección sin persistir ni cambiar el esquema. |
| Inventario | `Products`, `InventoryMovements`, `MonthlyPurchaseItems`, `MonthlyRestockPlans` | Catálogo y existencias por movimientos. La lista mensual se vincula por `ProductId` y puede asociar una compra real; `MonthlyRestockPlans` permanece obsoleto solo para compatibilidad histórica. |
| Caja | `FinancialEntries` | Otros ingresos, gastos e imprevistos sin duplicar movimientos originados en otros módulos. |
| Obligaciones | `Obligations`, `ObligationPayments`, `Loans`, `LoanPayments` | Importe esperado, recurrencia y pagos; préstamos con saldo, frecuencia, próximo vencimiento y cuotas. |
| Mantenimiento | `MaintenanceRecords` | Plan y ejecución con costo estimado o real. |
| Colaboradores | `Collaborators`, `CollaboratorContributions`, `MonthlyCloses`, `MonthlyCloseParticipants`, `DistributionPayments` | Inversionistas, aportes no operativos, fotografía financiera del cierre, porcentajes global/individual congelados y pagos completos de distribución. |
| Cierres financieros | `FinancialReserves`, `FinancialCloseExclusions`, `AnnualCloses` | Reservas por ocurrencia, exclusiones justificadas y snapshot anual; nunca reemplazan la operación fuente. |
| Actividad | `ActivityRecords` | Registro no editable de altas, ediciones, pagos, ventas, compras, asignaciones, cierres y eliminaciones. |
| Notas | `Notes` | Una única nota libre (`Id = 1`) con contenido y fecha UTC de última actualización. |

## Integridad relevante

- Una cuota semanal es única por persona y fecha de inicio; solo existe tras siete días completos, vence el primer sábado posterior o igual al final y la generación repetida es idempotente.
- `Chairs.AssignedPersonId` es único: una silla no admite dos trabajadores y un trabajador no admite dos sillas.
- `CollaboratorContributions` referencia a `Collaborators`, conserva fecha, valor y descripción, y se elimina solo lógicamente.
- `Products.UnitOfMeasure` permanece únicamente como columna heredada para leer bases alpha.1; la aplicación escribe un valor técnico neutro y no lo muestra ni lo exporta.
- Las altas de personas y obligaciones recurrentes guardan en una sola transacción el registro y los periodos que corresponden hasta la fecha solicitada.
- Los movimientos de inventario determinan la existencia. No se guarda una existencia editable paralela.
- Venta, consumo o corrección se rechazan si producen inventario negativo.
- Un nombre de producto activo es único sin distinguir mayúsculas, minúsculas ni espacios exteriores.
- Los planes heredados no se crean desde la interfaz ni intervienen en resultados o Excel.
- Una instancia recurrente de obligación conserva su origen para evitar duplicados.
- Un cierre confirmado es único por mes y guarda ingresos, meta, resultado, porcentaje, fondo, resultado retenido, participantes e importes como fotografía histórica.
- Fase 4.8 amplía el snapshot con cuentas por cobrar/pagar, egresos pagados, reservas, ajustes, préstamos, financiación, compromisos arrastrados, punto de equilibrio y faltante.
- Una reserva conserva origen/tipo/identificador, mes, valor esperado y, al consumirse, valor real y fecha. Los índices no impiden una nueva reserva histórica después de reabrir; el servicio garantiza una sola reserva activa por ocurrencia.
- Una exclusión exige motivo, no elimina el compromiso y es única lógicamente por mes y ocurrencia activa.
- `MonthlyPurchaseItems` usa claves foráneas restrictivas hacia producto y compra real. Cantidad y costo deben ser positivos; la compra vinculada queda congelada.
- Un préstamo no es ingreso operativo. Cada pago reduce el saldo y avanza el próximo vencimiento; el pago y el cambio del préstamo se guardan juntos.
- Reabrir sin pagos invalida lógicamente las asignaciones anteriores; con pagos se rechaza. Las asignaciones de cierres reabiertos no son pagables.
- La eliminación lógica conserva trazabilidad y las consultas normales no devuelven el registro eliminado.
- Los padres con dependencias históricas y los registros calculados de cierre/distribución no se eliminan mediante la operación genérica.

## Migraciones

1. `InitialSettings`: crea la configuración general aprobada.
2. `CompleteAdministration`: agrega las tablas operativas sin eliminar ni recrear `Settings`.
3. `PersistentFormDrafts`: conserva silenciosamente formularios incompletos ante cierres bruscos.
4. `Phase41BusinessModel`: agrega sillas, actividad, gastos extraoficiales, descripciones y precio predeterminado; convierte `TotalChairs` en `Silla 1`, `Silla 2`, etc. de forma idempotente.
5. `Phase42WorkersAndContributions`: agrega aportes de colaboradores mediante una tabla aditiva y conserva todas las tablas de Fase 4.1.
6. `Phase43MaintenanceRecurrence`: agrega recurrencia anclada y ocurrencias de mantenimiento.
7. `Phase46UsdExportsDistributionInventory`: agrega ruta de exportación, porcentaje directo heredado y costo de producto sin reinterpretar datos.
8. `Phase47SimplificationAndNotes`: agrega `Collaborators.FundParticipationBasisPoints`, `Obligations.IsSettled` y la tabla singleton `Notes`; conserva columnas y tablas anteriores.
9. `Phase48FinancialClosuresReservesLoansInventory`: migración aditiva única que amplía snapshots, congela porcentajes históricos y agrega reservas, exclusiones, lista mensual, préstamos/cuotas y cierres anuales. No elimina tablas o columnas heredadas.

La prueba de integración migra una base alpha.1, conserva su configuración y tablas, convierte el conteo histórico de sillas y valida las entidades nuevas. Antes de migrar una base existente con cambios pendientes, el inicializador crea una copia `pre-migration-*`.
