# Auditoría de requisitos de Fase 4.8

Fecha de corte técnica: 22 de julio de 2026. Rama: `feat/professional-ui-autosave`. La fase permanece en el PR draft #4; no publica `alpha.2` ni modifica la instalación `alpha.1`.

## Diagnóstico

- Los duplicados de Mantenimiento no procedían de filas duplicadas en SQLite. `RefreshAsync` podía solaparse al reconstruir `HistoryAssetOptions`; el cambio del filtro disparaba otro refresco antes de terminar el primero y varias cargas escribían simultáneamente en las colecciones. Se serializó el refresco con `SemaphoreSlim` y se suprimieron eventos internos durante la reconstrucción. No se aplicó `Distinct` como ocultamiento.
- El cálculo heredado usaba cuotas de Uso del local devengadas/cubiertas y snapshots automáticos. Fase 4.8 exige cobros reales, cierre manual y reservas por ocurrencia. `FinancialMonthCalculator` concentra la nueva regla y `AdministrationService` coordina las transacciones.
- La acción visible de retiro del trabajador duplicaba la eliminación lógica y podía crear estados ambiguos. Se retiró de interfaz/comandos; los datos heredados siguen siendo compatibles.

## Matriz de cumplimiento

| Requisito | Implementación | Archivos principales | Pruebas/evidencia | Resultado o limitación real |
|---|---|---|---|---|
| Retiro redundante | Pestaña `Silla`; sin fecha, botón o comando de retiro; eliminación lógica confirmada libera silla | `LocalUseView.xaml`, `LocalUseViewModel.cs`, `AdministrationService.cs` | `LogicalWorkerDeletion_*`, `Phase48UiContractTests` | Implementado; estados retirados heredados siguen legibles |
| Perfil de colaborador | Etiqueta exacta, una tabla cronológica y pago completo automático | `CollaboratorsView.xaml`, `CollaboratorsViewModel.cs` | contratos UI y pruebas de pago completo | Implementado |
| Porcentajes congelados | Participante guarda porcentaje global e individual del cierre, incluso con resultado cero | `MonthlyCloseParticipant.cs`, `CollaboratorDistributionCalculator.cs` | `ZeroResult_Freezes*`, `FrozenParticipantPercentages*` | Implementado |
| Límite interno 100 % | Validación exacta en puntos básicos | `CollaboratorDistributionCalculator.cs`, `AdministrationService.cs` | pruebas 60/20/10/10 y >100 % | Implementado |
| Fórmula financiera única | Cobros − egresos no reservados − reservas − ajustes − cuotas − arrastre | `FinancialMonthCalculator.cs` | escenario 1000/500/100/50/80 y fondo 54 | Implementado |
| Resultado negativo | Fondo, retenido positivo y pagos individuales quedan en cero; faltante local separado | calculador y snapshots | `NegativeResult_ProducesZeroFund*` | Implementado |
| Cuentas por cobrar | Deuda semanal queda fuera de ingresos hasta el pago | calculador | escenario exacto y pruebas mensuales | Implementado |
| Reservas sin doble descuento | Pago consume reserva; solo diferencia real afecta el periodo | `FinancialReserve`, servicios de pago/compra/mantenimiento | pruebas 100→110, 100→80 y compra posterior | Implementado |
| Compromisos arrastrados | Pendiente previo sin reserva reduce el resultado; una reserva activa no se repite | calculador/cierre | cierre-reapertura y candidatos | Implementado |
| Mantenimiento sin costo | Candidato visible con cero bloquea cierre salvo exclusión justificada | calculador/servicio | `MaintenanceWithoutCost_*` | Implementado |
| Préstamos | Entidades, saldo, cuotas y modo dentro de Obligaciones | `Loan.cs`, `ObligationsView*` | financiación excluida, cuota, saldo y UI | Implementado; no separa capital/interés por decisión aprobada |
| Lista mensual | Entidad nueva por `ProductId`, mes, cantidad, costo, reserva y compra vinculada | `MonthlyPurchaseItem.cs`, `InventoryView*` | cantidad×costo, arrastre y consumo | Implementado; no reutiliza planes heredados |
| Cierre mensual | Lista previa, exclusiones, cierre, reapertura y snapshot manual | `AdministrationView*`, `MonthlyClose.cs`, servicio | exclusión, bloqueo, reapertura sin activos duplicados | Implementado |
| Cierre anual | Doce meses obligatorios, snapshot anual y bloqueo de duplicado | `AnnualClose`, servicio y vista | `CloseYear_RequiresTwelveMonths*` | Implementado |
| Gráficos | Obligaciones, préstamos, reservas y ajustes; financiación fuera de ingresos | `AdministrationViewModel.cs` | `ExpenseChart_Includes*` y revisión Release aislada | Implementado y revisado visualmente |
| Movimientos del día | Actividad persistida por fecha, hora local, módulo, operación, importe y estado | `HomeView.xaml`, `MainViewModel.cs`, repositorio | contratos UI, auditoría transaccional y Excel | Implementado; no registra navegación o consultas |
| Ajustes autoguardados | Un evento consolidado si cambian valores; sin guardar la ruta privada | `EfAdministrationRepository.cs` | `SettingsAutosave_RecordsOne*` | Implementado |
| Duplicados de mantenimiento | Puerta de refresco y supresión de eventos internos | `MaintenanceViewModel.cs` | diez refrescos simultáneos, IDs únicos | Implementado en causa raíz |
| Balance anual | Doce filas mensuales y gráfico de ingresos; meses sin cierre en cero | `AdministrationViewModel.cs` | cierre anual y contratos | Implementado |
| Resumen en Ajustes | Sección fija, informativa y calculada por el servicio común | `SettingsView*` | contrato UI y pruebas del calculador | Implementado |
| Tablas | Columnas bloqueadas, anchos fijos y barras internas | `App.xaml` y XAML afectados | contratos globales XAML y revisión amplia/reducida | Implementado; los textos largos permanecen accesibles mediante barras internas |
| Migración | Una migración aditiva con tablas/columnas nuevas y backfill de porcentajes | `20260723014937_Phase48FinancialClosuresReservesLoansInventory*` | alpha.1→actual y Fase 4.7→4.8 | Implementado; sin eliminaciones de esquema |
| SQLite | FKs, WAL, `synchronous=FULL`, timeout y transacciones | persistencia existente y repositorios | `integrity_check`, `foreign_key_check`, durabilidad | Implementado |
| Excel | Hojas nuevas, tipos reales, un XLSX, cero CSV y movimiento seguro del temporal | `ExcelExportService.cs` | `ExcelExportTests` | Implementado |
| Compatibilidad histórica | Columnas/tablas anteriores se conservan; eliminados y borradores se exportan aparte | migración, exportación | migraciones y Excel | Implementado |

## Evidencia automatizada

La suite consolidada contiene **209 pruebas únicas**: 65 Domain, 60 Application, 21 Infrastructure y 63 App. Incluye migración desde alpha.1 y desde Fase 4.7, integridad/foráneas SQLite, libro Excel real, ausencia de CSV, columnas no redimensionables, mantenimiento sin duplicados, presentación de resultados negativos y reglas exactas de dinero/porcentajes.

La validación final ejecutó las 209 pruebas en Debug y nuevamente en Release: **418 ejecuciones aprobadas, 0 fallos y 0 omitidas**.

## Límites que no deben ocultarse

- Windows 10 x64 real, firma de código y una actualización entre dos Releases siguen sin comprobarse.
- La disposición 100 %, 125 % y 150 % queda cubierta por contratos responsivos automatizados; además se inspeccionaron manualmente tamaños amplio y reducido en la segunda pantalla. La automatización visual se detuvo cuando Windows detectó entrada del usuario, por lo que no se volvió a controlar la ventana.
- La aplicación es administrativa interna; no constituye contabilidad oficial ni calcula obligaciones legales.

## Validación final local

- Compilaciones Debug y Release: 0 advertencias y 0 errores.
- Migración de una copia aislada de Fase 4.7 hasta `20260723014937_Phase48FinancialClosuresReservesLoansInventory`: `integrity_check=ok`, cero violaciones foráneas, cuatro cierres de demostración, doce reservas y un préstamo.
- Arranque Release completo sobre esa copia; el diagnóstico recorre inicialización, generación programada, Ajustes e Inicio. Se corrigió la presentación de resultados negativos sin permitir importes negativos en el tipo de dominio `Money`.
- Revisión visual: Inicio, Uso del local, Colaboradores, Inventario y lista mensual, Obligaciones y préstamos, Mantenimiento, Resumen mensual, Balance anual y Ajustes. La fotografía negativa congelada de julio se mantuvo distinta del cálculo dinámico posterior, como exige el cierre histórico.
- NuGet directo/transitivo: cero paquetes vulnerables y cero paquetes en desuso en los orígenes consultados.
- Gitleaks 8.30.1 con `--redact`: 31 commits y árbol de trabajo, cero filtraciones.
- Git: cero binarios, bases, exportaciones o nombres sensibles rastreados; `git diff --check` limpio.
- Velopack 1.2.0: empaquetado aislado `0.1.0-phase48-validation`, sin instalación, firma, etiqueta o publicación.
