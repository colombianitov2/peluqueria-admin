# Auditoría de requisitos — Fase 4.6

Fecha de corte: 2026-07-21.

| Área | Implementación verificable | Evidencia principal |
|---|---|---|
| Identidad visible | Título vacío y cabecera lateral retirada | `MainWindow.xaml`; contrato UI |
| Sillas | Resumen de trabajador y eventos ligados a silla y trabajador | `AdministrationService`; `LocalUseViewModel`; pruebas de asignación, cambio y retiro |
| USD y presupuesto retirado | Constante USD, normalización sin conversión, presupuesto heredado efectivo cero | `ApplicationCurrency`; `GeneralSettings`; pruebas de dominio y migración |
| Autoguardado | Ediciones válidas con debounce; operaciones nuevas o financieras siguen explícitas | ViewModels de Ajustes, Colaboradores, Uso del local, Inventario y Administración |
| Exportación | Un archivo `.xlsx`, carpeta persistente, temporal atómico, sin CSV visible | `ExcelExportService`; pruebas ClosedXML |
| Distribución | Subporcentajes individuales exactos, suma limitada por porcentaje global, snapshots preservados | `CollaboratorDistributionCalculator`; `Collaborator.ProfitShareBasisPoints` |
| Inicio | Avisos compactos y accesibles de mantenimientos y obligaciones | `HomeView`; `MainViewModel` |
| Ventas | Precio editable, total inmediato y límite de existencia | `SalesViewModel`; `AdministrationService`; pruebas 35×1/2/3 |
| Gráficas | Ocho escalas reales; registros sin hora no reciben una hora inventada | `AdministrationViewModel`; teoría con reloj controlado |
| Persistencia | Migración aditiva de ruta, porcentaje individual y costo configurado | `Phase46UsdExportsDistributionInventory` |

Decisiones reemplazadas: moneda configurable, COP, presupuesto mensual opcional, nómina independiente, reparto igualitario y exportación CSV múltiple. Las columnas heredadas necesarias para compatibilidad no se eliminan. La instalación alpha.1 y su base real permanecen fuera del alcance.
