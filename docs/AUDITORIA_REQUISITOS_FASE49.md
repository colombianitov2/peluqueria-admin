# Auditoría de requisitos — Fase 4.9

Fecha de corte: 2026-07-23. Rama: `feat/professional-ui-autosave`. Base: `6e3812d`.

| Requisito | Causa encontrada | Implementación | Archivos principales | Pruebas | Resultado |
|---|---|---|---|---|---|
| Movimientos exactos y consulta local | Etiquetas compartidas y filtro apoyado en fecha persistida sin conversión uniforme | Acciones inequívocas y `DailyActivityQuery` sobre `OccurredUtc` con zona local, limpieza y orden descendente | `AdministrationService`, `EfAdministrationRepository`, `DailyActivityQuery`, `MainViewModel` | 01–03 | Aprobado |
| Cobro semanal y anticipos | El crédito se proyectaba, pero no quedaba consumido por cada sábado generado | Vencimiento sábado explícito, aplicación cronológica de pagos, tarifa histórica y proyección separada de cobro/pago requerido | `WeeklyCharge`, `WeeklyChargeCalculator`, `LocalUseViewModel` | 04–07 | Aprobado |
| Historial de aportes | El historial se reconstruía desde aportes activos | Eventos inmutables de creación, edición, eliminación y snapshot migrado; total vigente separado | `CollaboratorContributionEvent`, `AdministrationService`, `CollaboratorsViewModel` | 08–12 | Aprobado |
| Lista mensual independiente | `ProductId` era obligatorio y el nombre procedía del inventario | Nombre/categoría propios, producto opcional, edición/eliminación lógica y vínculo atómico | `MonthlyPurchaseItem`, `InventoryViewModel`, `InventoryView` | 13–18 | Aprobado |
| Préstamos exactos | El modelo heredado solo guardaba saldo/cuota y avanzaba una frecuencia | Dos métodos exclusivos, cálculo decimal en centavos, calendario mensual, última cuota residual y pago por cuota | `LoanCalculator`, `Loan`, `LoanInstallment`, `ObligationsViewModel` | 19–28 | Aprobado |
| Balance anual | La vista reutilizaba filtros genéricos y dependía de snapshots mensuales | Consulta anual exclusiva, 12 meses, mezcla snapshot/live sin duplicar, cierre anual y arrastres separados | `AnnualFinancialCalculator`, `AnnualClose`, `AnnualCarryover`, `AdministrationView` | 29–37 | Aprobado |
| Resumen y gastos extraoficiales | Resumen duplicado en Ajustes y filtro temporal impropio | Resumen solo en Resumen mensual; gastos persistentes con edición explícita | `SettingsViewModel`, `SettingsView`, `AdministrationView` | 38–41 | Aprobado |
| Persistencia y migración | Faltaban estructuras para eventos, cuotas y arrastres | Una migración EF Core aditiva con backfill compatible F48 | `20260723142402_Phase49HistoriesLoansAndAnnualBalance` | 42–45 | Aprobado |
| Excel y refresco | El libro no conocía las nuevas estructuras y algunas colecciones podían retener presentación anterior | Snapshot consistente ampliado, nuevas hojas/campos y colecciones repobladas desde cero | `ExcelExportService`, view models | 46–47 | Aprobado |

La suite consolidada contiene 256 pruebas únicas. La validación final de compilación, Release, formato, seguridad, migraciones sobre copias y revisión visual queda registrada en el PR draft #4 y en el informe final de la tarea.
