# Auditoría de requisitos — Fase 4.10

Fecha de corte: 23 de julio de 2026.

## Alcance aprobado

| Área | Estado anterior | Decisión vigente | Evidencia esperada |
|---|---|---|---|
| Agregar al inventario | Formulario genérico con selector de operación | Siempre registra una compra desde una fila mensual pendiente; búsqueda por nombre, categoría o mes | contrato XAML, prueba de filtrado y transacción de compra |
| Importe de compra | Costo y precio podían confundirse | Caja = costo esperado unitario × cantidad real; precio de venta queda separado | pruebas de servicio y cálculo financiero |
| Lista mensual | Exponía Activa, reserva y activar/desactivar | Solo agregar, editar, guardar y eliminar; flags antiguos no gobiernan cálculos | contratos XAML y casos con valores heredados |
| Inicio y punto de equilibrio | La compra mensual podía omitirse o depender de flags | Toda fila no comprada interviene en su mes y desaparece al vincular la compra real | pruebas de Inicio, mes y ausencia de doble conteo |
| Obligaciones | Faltaban Crédito y Semanal | Crédito se desglosa por separado; Semanal avanza siete días | pruebas Domain, Application, App y Excel |
| Pago de obligación | El saldo podía diferir entre pantallas | Una ocurrencia confirmada usa el valor real y queda con saldo cero | pruebas de cálculo, Inicio y exportación |
| Excel | Faltaban datos directos y compatibilidad explícita | Fotografía consistente con tarifas, lista mensual, créditos, recurrencias, saldos, históricos, eliminados y borradores | apertura ClosedXML, hojas, tipos, filtros y totales |
| Manual | Figuraba como pendiente | Sección estática y detallada debajo de Notas | contrato de navegación, XAML válido y contenido mínimo |

## Protección de compatibilidad

- No se elimina ningún valor de una base alpha.1. Los indicadores heredados siguen legibles, aunque dejan de controlar el flujo y los cálculos conforme a la nueva decisión funcional.
- No se requiere migración de esquema: `Credit = 4` y `Weekly = 3` son valores nuevos de enums almacenados como enteros.
- Las columnas y entidades heredadas de inventario permanecen legibles y se identifican como compatibilidad; no controlan compromisos vigentes.
- Notas, borradores, cierres y snapshots históricos conservan su precedencia.
- Las pruebas y exportaciones usan raíces temporales; no acceden a la base real.

## Fuera de alcance

- No se fusiona el PR durante esta corrección.
- No se publica `alpha.2`, no se crea una etiqueta o Release y no se modifica Velopack.
- El logotipo de la empresa se incorporará en una actualización posterior. Esa entrega separada servirá para comprobar el salto por GitHub sin desinstalar ni borrar datos.

## Criterio de cierre

La fase solo puede cerrarse con compilación Debug/Release, suite completa, prueba focalizada de Excel, modelo EF sin cambios pendientes, auditoría de dependencias y secretos, revisión del diff, rama publicada y PR draft actualizado. Cualquier total de pruebas se documenta después de la ejecución consolidada, no por estimación.
