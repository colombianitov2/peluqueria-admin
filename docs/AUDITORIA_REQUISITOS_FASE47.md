# Auditoría de requisitos de Fase 4.7

Fecha de corte: 22 de julio de 2026. Rama: `feat/professional-ui-autosave`. La fase se mantiene en el PR draft #4 y no publica `alpha.2`.

| Requisito | Implementación verificable | Estado |
|---|---|---|
| Tablas bloqueadas y desplazamiento interno | Estilo global `DataGrid`, columnas numéricas fijas, texto ajustable y pruebas sobre todos los XAML | Implementado |
| Única campana de mantenimiento | Inicio elimina recibo, insignia, popup y comandos de obligaciones; conserva popup ancho y acceso a Mantenimiento | Implementado |
| Saldo a favor semanal | Generación idempotente por periodos anclados de siete días y prueba con reloj mutable/reinicio SQLite | Implementado |
| Ganancia neta real | `AdministrationReports.BuildMonthlyInput` usa solo ingresos devengados y salidas reales, sin capital, extraoficiales, estimaciones o planes | Implementado |
| Fondo y participaciones internas | Campo aditivo `FundParticipationBasisPoints`, suma máxima 100 %, fórmula 60/20/10/10 y snapshots históricos | Implementado |
| Inventario simplificado | Pestañas Inventario/Movimientos/Agregar, productos recientes y ausencia de planes en UI/Excel | Implementado |
| Notas | Tabla singleton SQLite, debounce de 500 ms, flush al perder foco/cerrar, reapertura, copia y hoja Excel | Implementado |
| Obligaciones | Definición permanente y pagos vinculados en modos/tablas separados; duplicados por nombre bloqueados y recurrencia sin deriva | Implementado |
| Mantenimiento | Programación/realización y pendientes/historial separados; filtro por equipo, rango y siguiente ocurrencia anclada | Implementado |
| Gráfico circular | Sin etiquetas sobre porciones, colores distintos, tooltip monetario, leyenda inferior desplazable y estado `Sin datos` sin porción ficticia | Implementado |
| Sin limpiar formulario | Ningún XAML contiene `Limpiar formulario`; éxito restablece campos y error conserva entrada | Implementado |
| Migración conservadora | `Phase47SimplificationAndNotes` agrega dos columnas y `Notes`; no elimina datos, snapshots, columnas ni tablas heredadas | Implementado |

## Decisiones sustituidas

Se consideran históricas y no vigentes: recibo de obligaciones, porcentajes individuales directos, planes de reposición funcionales, cierre mensual manual visible, botones `Limpiar formulario`, formulario combinado de mantenimiento y tabla combinada de obligaciones/pagos.

## Evidencia automatizada

La suite exige que ningún `DataGridTextColumn` visible use ancho proporcional, que no existan botones de limpieza, que Inicio no contenga notificaciones de obligaciones, que Excel incluya `Notas` y no `Planes de reposición`, y que los cálculos de dinero y recurrencia coincidan con los ejemplos aprobados. Las rutas de pruebas usan directorios temporales; nunca escriben en el Escritorio ni en la base real.
