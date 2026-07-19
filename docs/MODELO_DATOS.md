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
| Ajustes | `Settings` | Única fila con tarifa semanal, porcentaje, reserva opcional, sillas y moneda. |
| Uso del local | `LocalUsePeople`, `WeeklyRates`, `WeeklyCharges`, `LocalUsePayments` | Personas, histórico de tarifas, cuotas de siete días y pagos. |
| Inventario | `Products`, `InventoryMovements`, `MonthlyRestockPlans` | Catálogo mínimo, existencias por movimientos y necesidad opcional mensual. |
| Caja | `FinancialEntries` | Otros ingresos, gastos e imprevistos sin duplicar movimientos originados en otros módulos. |
| Obligaciones | `Obligations`, `ObligationPayments` | Importe esperado, recurrencia y pagos parciales/finales. |
| Mantenimiento | `MaintenanceRecords` | Plan y ejecución con costo estimado o real. |
| Colaboradores | `Collaborators`, `MonthlyCloses`, `MonthlyCloseParticipants`, `DistributionPayments` | Participantes, fotografía del cierre y pagos de distribución. |

## Integridad relevante

- Una cuota semanal es única por persona y fecha de inicio; la generación repetida es idempotente.
- Las altas de personas y obligaciones recurrentes guardan en una sola transacción el registro y los periodos que corresponden hasta la fecha solicitada.
- Los movimientos de inventario determinan la existencia. No se guarda una existencia editable paralela.
- Venta, consumo o corrección se rechazan si producen inventario negativo.
- Un nombre de producto activo es único sin distinguir mayúsculas, minúsculas ni espacios exteriores.
- Un plan de reposición es único por producto y mes.
- Una instancia recurrente de obligación conserva su origen para evitar duplicados.
- Un cierre confirmado es único por mes y guarda ingresos, meta, resultado, porcentaje, fondo, resultado retenido, participantes e importes como fotografía histórica.
- Reabrir sin pagos invalida lógicamente las asignaciones anteriores; con pagos se rechaza. Las asignaciones de cierres reabiertos no son pagables.
- La eliminación lógica conserva trazabilidad y las consultas normales no devuelven el registro eliminado.
- Los padres con dependencias históricas y los registros calculados de cierre/distribución no se eliminan mediante la operación genérica.

## Migraciones

1. `InitialSettings`: crea la configuración general aprobada.
2. `CompleteAdministration`: agrega las tablas operativas sin eliminar ni recrear `Settings`.

La Fase 3.1 no modifica el esquema ni agrega migraciones: refuerza reglas transaccionales y de lectura sobre el modelo existente.

La prueba de integración migra primero hasta `InitialSettings`, guarda una configuración, aplica la migración completa y confirma que la fila anterior permanece. Antes de migrar una base existente con cambios pendientes, el inicializador crea una copia `pre-migration-*`.
